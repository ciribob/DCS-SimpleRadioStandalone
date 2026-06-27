using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.ATCAWACS.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.ATCAWACS;

public class ATCAWACSSyncHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly long
        UPDATE_SYNC_RATE =
            5 * 1000 * 10000; //There are 10,000 ticks in a millisecond, or 10 million ticks in a second. Update every 5 seconds

    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly ClientStateSingleton _clientStateSingleton;
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly string _guid;

    private readonly double _heightOffset;
    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private long _lastSent;
    private UdpClient _ATCAWACSPositionListener;
    private volatile bool _stop;

    public ATCAWACSSyncHandler(string guid)
    {
        _guid = guid;
        _clientStateSingleton = ClientStateSingleton.Instance;

        _heightOffset = _globalSettings.GetClientSetting(GlobalSettingsKeys.ATCAWACSHeightOffset).DoubleValue;
    }

    public void Start()
    {
        Task.Run(async Task () =>
        {
            while (!_stop)
                try
                {
                    var localEp = new IPEndPoint(IPAddress.Any,
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.ATCAWACSIncomingUDP));
                    _ATCAWACSPositionListener = new UdpClient(localEp);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex,
                        $"Unable to bind to the ATCAWACS Export Listener Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.ATCAWACSIncomingUDP)}");
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }

            while (!_stop)
                try
                {
                    var result = await _ATCAWACSPositionListener.ReceiveAsync();
                    var bytes = result.Buffer;

                    var ATCAWACSPositionWrapper = JsonSerializer.Deserialize<ATCAWACSMessageWrapper>(
                        Encoding.UTF8.GetString(bytes, 0, bytes.Length),
                            new JsonSerializerOptions() { IncludeFields = true }
                        );

                    if (ATCAWACSPositionWrapper != null)
                    {
                        if (ATCAWACSPositionWrapper.los != null)
                            HandleLOSResponse(ATCAWACSPositionWrapper.los);
                        else if (ATCAWACSPositionWrapper.controller != null)
                            await HandleATCAWACSUpdateAsync(ATCAWACSPositionWrapper.controller);
                    }
                }
                catch (SocketException e)
                {
                    if (!_stop) Logger.Error(e, "SocketException Handling ATCAWACS UDP Message");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Handling ATCAWACS UDP Message");
                }

            try
            {
                _ATCAWACSPositionListener.Close();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception stoping ATCAWACS UDP listener");
            }
        });

        StartATCAWACSLOSSender();
    }

    private async Task HandleATCAWACSUpdateAsync(ATCAWACSMessageWrapper.ATCAWACSPosition controller)
    {
        _clientStateSingleton.ATCAWACSLastReceived = DateTime.Now.Ticks;

        // Check if the external tool provided a display name in the JSON payload
        if (!string.IsNullOrEmpty(controller.displayName))
        {
            _clientStateSingleton.ATCAWACSToolName = controller.displayName;
        }

        //only send update if position and line of sight are enabled
        var shouldUpdate = _serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ||
                           _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);

        if (_clientStateSingleton.ShouldUseATCAWACSPosition())
        {
            _clientStateSingleton.UpdatePlayerPosition(new LatLngPosition
                { lat = controller.latitude, lng = controller.longitude, alt = controller.altitude + _heightOffset });
            var diff = DateTime.Now.Ticks - _lastSent;

            if (diff > UPDATE_SYNC_RATE &&
                shouldUpdate) // There are 10,000 ticks in a millisecond, or 10 million ticks in a second. Update ever 5 seconds
            {
                _lastSent = DateTime.Now.Ticks;
              
                await EventBus.Instance.PublishOnCurrentThreadAsync(new UnitUpdateMessage()
                {
                    FullUpdate = false,
                    UnitUpdate = new SRClientBase()
                    {
                        ClientGuid = _clientStateSingleton.ShortGUID,
                        Coalition = _clientStateSingleton.PlayerCoaltionLocationMetadata.side,
                        LatLngPosition = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition,
                        Seat = _clientStateSingleton.PlayerCoaltionLocationMetadata.seat,
                        Name = _clientStateSingleton.LastSeenName,
                        AllowRecord = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowRecording),
                        DISEntityId = _globalSettings.GetClientSettingInt(GlobalSettingsKeys.DISEntityID)
                    }
                });
            }
        }
    }

    private void HandleLOSResponse(ATCAWACSMessageWrapper.ATCAWACSLineOfSightResponse response)
    {
        SRClientBase client;

        if (_clients.TryGetValue(response.clientId, out client))
            //1 is total loss so if see is false
            //0 is full line of sight so see is true
            client.LineOfSightLoss = response.see ? 0 : 1;
    }

    private void StartATCAWACSLOSSender()
    {
        var _udpSocket = new UdpClient();
        var _host = new IPEndPoint(IPAddress.Loopback,
            _globalSettings.GetNetworkSetting(GlobalSettingsKeys.ATCAWACSOutgoingUDP));


        Task.Run(async Task () =>
        {
            using (_udpSocket)
            {
                while (!_stop)
                {
                    try
                    {
                        if (_clientStateSingleton.IsATCAWACSConnected)
                        {
                            //Chunk client list into blocks of 10 to stay below 8000 ish UDP socket limit
                            var clientsList = GenerateDcsLosCheckRequests();

                            if (clientsList.Count > 0)
                                foreach (var losRequest in clientsList)
                                {
                                    var byteData =
                                        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(losRequest, new JsonSerializerOptions() { IncludeFields = true, PropertyNameCaseInsensitive = true }) + "\n");

                                    _udpSocket.Send(byteData, byteData.Length, _host);

                                    //every 50ms - Wait for the queue
                                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                                }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending ATCAWACS LOS Request Message");
                    }

                    //every 2s - Wait for the queue
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                try
                {
                    _udpSocket.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stoping ATCAWACS LOS Sender ");
                }
            }
        });
    }

    private List<ATCAWACSLineOfSightRequest> GenerateDcsLosCheckRequests()
    {
        var clients = _clients.Values.ToList();

        var requests = new List<ATCAWACSLineOfSightRequest>();

        if (_clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition != null
            && _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.IsValid()
            && _clientStateSingleton.ShouldUseATCAWACSPosition()
            && _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED))
            foreach (var client in clients)
                //only check if its worth it
                if (client.LatLngPosition != null
                    && client.LatLngPosition.IsValid()
                    && client.ClientGuid != _guid
                   )
                    requests.Add(new ATCAWACSLineOfSightRequest
                    {
                        lat1 = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.lat,
                        long1 = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.lng,
                        alt1 = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.alt,

                        lat2 = client.LatLngPosition.lat,
                        long2 = client.LatLngPosition.lng,
                        alt2 = client.LatLngPosition.alt,

                        clientId = client.ClientGuid
                    });

        return requests;
    }

    public void Stop()
    {
        _stop = true;
        try
        {
            _ATCAWACSPositionListener?.Close();
        }
        catch (Exception)
        {
            //IGNORE
        }
    }
}