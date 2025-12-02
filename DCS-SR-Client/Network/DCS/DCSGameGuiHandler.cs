using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class DCSGameGuiHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private UdpClient _dcsGameGuiUdpListener;
    private volatile bool _stop;

    public DCSGameGuiHandler()
    {
    }

    public void Start()
    {
        _clientStateSingleton.LastPostionCoalitionSent = 0;

        Task.Factory.StartNew(() =>
        {
            while (!_stop)
                try
                {
                    var localEp = new IPEndPoint(IPAddress.Any,
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingGameGUIUDP));

                    _dcsGameGuiUdpListener = new UdpClient(localEp);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex,
                        $"Unable to bind to the DCS GameGUI Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingGameGUIUDP)}");
                    Thread.Sleep(500);
                }

            //    var count = 0;
            while (!_stop)
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = _dcsGameGuiUdpListener.Receive(ref groupEp);

                    var updatedPlayerInfo =
                        JsonSerializer.Deserialize<DCSPlayerSideInfo>(Encoding.UTF8.GetString(
                            bytes, 0, bytes.Length), new JsonSerializerOptions()
                            {
                                IncludeFields = true,
                            });

                    if (updatedPlayerInfo != null)
                    {
                        Console.WriteLine(@"PlayerData was valid");

                        var currentInfo = _clientStateSingleton.PlayerCoaltionLocationMetadata;

                        var changed = !updatedPlayerInfo.Equals(currentInfo);

                        //copy the bits we need  - leave position
                        currentInfo.name = updatedPlayerInfo.name;
                        currentInfo.side = updatedPlayerInfo.side;
                        currentInfo.seat = updatedPlayerInfo.seat;

                        _clientStateSingleton.LastSeenName = currentInfo.name;

                        //this will clear any stale positions if nothing is currently connected
                        _clientStateSingleton.ClearPositionsIfExpired();

                        //TCPClient will automatically not send if its not actually changed
                        EventBus.Instance.PublishOnCurrentThreadAsync(new UnitUpdateMessage()
                        {
                            FullUpdate = false,
                            UnitUpdate = new SRClientBase()
                            {
                                ClientGuid = _clientStateSingleton.ShortGUID,
                                Coalition = _clientStateSingleton.PlayerCoaltionLocationMetadata.side,
                                LatLngPosition = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition,
                                Seat = _clientStateSingleton.PlayerCoaltionLocationMetadata.seat,
                                Name = _clientStateSingleton.LastSeenName,
                                AllowRecord = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowRecording)
                            }
                        });

                        _clientStateSingleton.DcsGameGuiLastReceived = DateTime.Now.Ticks;
                    }

                    DCSTimeUpdate newTimeUpdate = JsonSerializer.Deserialize<DCSTimeUpdate>(Encoding.UTF8.GetString(
                        bytes, 0, bytes.Length), new JsonSerializerOptions() { IncludeFields = true, });

                    if (newTimeUpdate != null && !DCSTimeUpdate.IsZero(newTimeUpdate))
                    {
                        var currentMissionTime = DCSTimeUpdate.Iso8691Builder(newTimeUpdate);
                        var startMissionTime = newTimeUpdate.GetStartMissionTime();

                        Debug.WriteLine("Mission Start Time: " + startMissionTime?.ToString(CultureInfo.InvariantCulture));
                        Debug.WriteLine("Mission Current Time: " + currentMissionTime?.ToString(CultureInfo.InvariantCulture));

                        if (currentMissionTime.HasValue && startMissionTime.HasValue)
                        {
                            EventBus.Instance.PublishOnCurrentThreadAsync(
                                new DCSMissionTimeUpdateMessage(currentMissionTime.Value, startMissionTime.Value)
                            );
                        }
                    }
                }
                catch (SocketException e)
                {
                    // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                    if (!_stop) Logger.Error(e, "SocketException Handling DCS GameGUI Message");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Handling DCS GameGUI Message");
                }

            try
            {
                _dcsGameGuiUdpListener.Close();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception stoping DCS listener ");
            }
        });
    }

    public void Stop()
    {
        _stop = true;
        try
        {
            _dcsGameGuiUdpListener?.Close();
        }
        catch (Exception)
        {
            // ignored
        }

        _clientStateSingleton.DcsGameGuiLastReceived = -1;
    }
}