using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class DCSLineOfSightHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly string _guid;
    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private UdpClient _dcsLOSListener;
    private volatile bool _stop;

    public DCSLineOfSightHandler(string guid)
    {
        _guid = guid;
    }

    public void Start()
    {
        StartDCSLOSBroadcastListener();
        StartDCSLOSSender();
    }

    //used for the result
    private void StartDCSLOSBroadcastListener()
    {
        Task.Factory.StartNew(() =>
        {
            while (!_stop)
                try
                {
                    var localEp = new IPEndPoint(IPAddress.Any,
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSLOSIncomingUDP));
                    _dcsLOSListener = new UdpClient(localEp);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex,
                        $"Unable to bind to the DCS LOS Listner Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSLOSIncomingUDP)}");
                    Thread.Sleep(500);
                }

            //    var count = 0;
            while (!_stop)
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = _dcsLOSListener.Receive(ref groupEp);

                    /*   Logger.Debug(Encoding.UTF8.GetString(
                            bytes, 0, bytes.Length));*/
                    var playerInfo =
                        JsonSerializer.Deserialize<DCSLosCheckResult[]>(Encoding.UTF8.GetString(
                            bytes, 0, bytes.Length), new JsonSerializerOptions() { IncludeFields = true });

                    foreach (var player in playerInfo)
                    {
                        SRClientBase client;

                        if (_clients.TryGetValue(player.id, out client)) client.LineOfSightLoss = player.los;
                        //  Logger.Debug(client.ToString());
                    }
                }
                catch (SocketException e)
                {
                    // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                    if (!_stop) Logger.Error(e, "SocketException Handling DCS Los Result Message");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Handling DCS Los Result Message");
                }

            try
            {
                _dcsLOSListener.Close();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception stoping DCS LOS Result listener ");
            }
        });
    }

    private void StartDCSLOSSender()
    {
        var _udpSocket = new UdpClient();
        var _host = new IPEndPoint(IPAddress.Loopback,
            _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSLOSOutgoingUDP));


        Task.Factory.StartNew(() =>
        {
            using (_udpSocket)
            {
                while (!_stop)
                {
                    try
                    {
                        //Chunk client list into blocks of 10 to stay below 8000 ish UDP socket limit
                        var clientsList = GenerateDcsLosCheckRequests();

                        if (clientsList.Count > 0)
                        {
                            var splitList = clientsList.ChunkBy(10);
                            foreach (var clientSubList in splitList)
                            {
                                // Logger.Info( "Sending LOS Request: "+ JsonConvert.SerializeObject(clientSubList));
                                var byteData =
                                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clientSubList, new JsonSerializerOptions()
                                    {
                                        IncludeFields = true,
                                    }) + "\n");

                                _udpSocket.Send(byteData, byteData.Length, _host);

                                //every 250 - Wait for the queue
                                Thread.Sleep(250);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending DCS LOS Request Message");
                    }

                    //every 300 - Wait for the queue
                    Thread.Sleep(250);
                }

                try
                {
                    _udpSocket.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stoping DCS listener ");
                }
            }
        });
    }

    private List<DCSLosCheckRequest> GenerateDcsLosCheckRequests()
    {
        var clients = _clients.Values.ToList();

        var requests = new List<DCSLosCheckRequest>();

        var playerLocation = ClientStateSingleton.Instance.PlayerCoaltionLocationMetadata;

        if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED)
            && playerLocation != null
            && playerLocation.LngLngPosition != null
            && playerLocation.LngLngPosition.IsValid())
            foreach (var client in clients)
                //only check if its worth it
                if (client.LatLngPosition != null
                    && client.LatLngPosition.IsValid()
                    && client.ClientGuid != _guid
                   )
                {
                    var latLng = client.LatLngPosition;

                    requests.Add(new DCSLosCheckRequest
                    {
                        id = client.ClientGuid,
                        lat = latLng.lat,
                        lng = latLng.lng,
                        alt = latLng.alt
                    });
                }

        return requests;
    }

    internal void Stop()
    {
        _stop = true;
        try
        {
            _dcsLOSListener?.Close();
        }
        catch (Exception)
        {
            // ignored
        }
    }
}