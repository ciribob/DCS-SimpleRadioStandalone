using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.AwacsRadioOverlayWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Server;
using Newtonsoft.Json;
using NLog;

/**
Keeps radio information in Sync Between DCS and 

**/

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class RadioDCSSyncServer
    {
        public delegate void ClientSideUpdate();

        public delegate void SendRadioUpdate();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly SendRadioUpdate _clientRadioUpdate;
        private readonly ConcurrentDictionary<string, SRClient> _clients;
        private readonly ClientSideUpdate _clientSideUpdate;
        private readonly string _guid;

        private UdpClient _dcsGameGuiudpListener;

        private UdpClient _dcsLOSListener;
        private UdpClient _dcsUdpListener;
        private UdpClient _dcsRadioUpdateSender;
        private UdpClient _udpCommandListener;

        private volatile bool _stop;

        private ClientStateSingleton _clientStateSingleton;

        public RadioDCSSyncServer(SendRadioUpdate clientRadioUpdate, ClientSideUpdate clientSideUpdate,
            ConcurrentDictionary<string, SRClient> _clients, string guid)
        {
            _clientRadioUpdate = clientRadioUpdate;
            _clientSideUpdate = clientSideUpdate;
            this._clients = _clients;
            _guid = guid;
            _clientStateSingleton = ClientStateSingleton.Instance;
        }

        

        private readonly SettingsStore _settings = SettingsStore.Instance;


        public void Listen()
        {
            DcsListener();
        }

        private void DcsListener()
        {
            StartDcsBroadcastListener();
            StartDcsGameGuiBroadcastListener();
            StartDCSLOSBroadcastListener();
            StartDCSLOSSender();
            StartUDPCommandListener();
        }

        private void StartUDPCommandListener()
        {
            _udpCommandListener = new UdpClient();
            _udpCommandListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpCommandListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            var localEp = new IPEndPoint(IPAddress.Any, _settings.GetNetworkSetting(SettingsKeys.CommandListenerUDP));
            _udpCommandListener.Client.Bind(localEp);

            Task.Factory.StartNew(() =>
            {
                using (_udpCommandListener)
                {
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _settings.GetNetworkSetting(SettingsKeys.CommandListenerUDP));
                            var bytes = _udpCommandListener.Receive(ref groupEp);

                            //Logger.Info("Recevied Message from UDP COMMAND INTERFACE: "+ Encoding.UTF8.GetString(
                            //          bytes, 0, bytes.Length));
                            var message =
                                JsonConvert.DeserializeObject<UDPInterfaceCommand>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            if (message?.Command == UDPInterfaceCommand.UDPCommandType.FREQUENCY)
                            {
                                RadioHelper.UpdateRadioFrequency(message.Frequency, message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.ACTIVE_RADIO)
                            {
                                RadioHelper.SelectRadio(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.TOGGLE_GUARD)
                            {
                                RadioHelper.ToggleGuard(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.CHANNEL_UP)
                            {
                                RadioHelper.RadioChannelUp(message.RadioId);
                            }
                            else if (message?.Command == UDPInterfaceCommand.UDPCommandType.CHANNEL_DOWN)
                            {
                                RadioHelper.RadioChannelDown(message.RadioId);
                            }
                            else
                            {
                                Logger.Error("Unknown UDP Command!");
                            }
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS  Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS  Message");
                        }
                    }

                    try
                    {
                        _udpCommandListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        private void StartDcsBroadcastListener()
        {
            _dcsUdpListener = new UdpClient();
            _dcsUdpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _dcsUdpListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            //   var multicastaddress = IPAddress.Parse("239.255.50.10");
            //      _dcsUdpListener.JoinMulticastGroup(multicastaddress);

            var localEp = new IPEndPoint(IPAddress.Any, _settings.GetNetworkSetting(SettingsKeys.DCSIncomingUDP));
            _dcsUdpListener.Client.Bind(localEp);
            //   activeRadioUdpClient.Client.ReceiveTimeout = 10000;

            //reset last sent
            _clientStateSingleton.LastSent = 0;

            Task.Factory.StartNew(() =>
            {
                using (_dcsUdpListener)
                {
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _settings.GetNetworkSetting(SettingsKeys.DCSIncomingUDP));
                            var bytes = _dcsUdpListener.Receive(ref groupEp);

                            var str = Encoding.UTF8.GetString(
                                bytes, 0, bytes.Length).Trim();
                            
                            var message =
                                JsonConvert.DeserializeObject<DCSPlayerRadioInfo>(str);

                            Logger.Debug($"Recevied Message from DCS {str}");

                            //sync with others
                            //Radio info is marked as Stale for FC3 aircraft after every frequency change

                            var update = UpdateRadio(message);
                            
                            Logger.Debug("Radio Updated");

                            //send to DCS UI
                            SendRadioUpdateToDCS();
                            
                            Logger.Debug("Update sent to DCS");

                            if (update || IsRadioInfoStale(message))
                            {
                                Logger.Debug("Sending Radio Info To Server - Stale");
                                _clientStateSingleton.LastSent = DateTime.Now.Ticks;
                                _clientRadioUpdate();
                            }
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS AutoConnect Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS  Message");
                        }
                    }

                    try
                    {
                        _dcsUdpListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        //send updated radio info back to DCS for ingame GUI
        private void SendRadioUpdateToDCS()
        {
            if (_dcsRadioUpdateSender == null)
            {
                _dcsRadioUpdateSender = new UdpClient
                {
                    ExclusiveAddressUse = false,
                    EnableBroadcast = true
                };
                _dcsRadioUpdateSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                    true);
                _dcsRadioUpdateSender.ExclusiveAddressUse = false;
            }

            try
            {
                //get currently transmitting or receiving
                var combinedState = new CombinedRadioState()
                {
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                    RadioSendingState = TCPVoiceHandler.RadioSendingState,
                    RadioReceivingState = TCPVoiceHandler.RadioReceivingState
                };

                var message = JsonConvert.SerializeObject(combinedState, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }) + "\n";

                var byteData =
                    Encoding.UTF8.GetBytes(message);

                //Logger.Info("Sending Update over UDP 7080 DCS - 7082 Flight Panels: \n"+message);

                _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _settings.GetNetworkSetting(SettingsKeys.OutgoingDCSUDPInfo))); //send to DCS
                _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _settings.GetNetworkSetting(SettingsKeys
                            .OutgoingDCSUDPOther))); // send to Flight Control Panels
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending DCS Radio Update Message");
            }
        }

        private void StartDcsGameGuiBroadcastListener()
        {
            _dcsGameGuiudpListener = new UdpClient();
            _dcsGameGuiudpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            _dcsGameGuiudpListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            //    var multicastaddress = IPAddress.Parse("239.255.50.10");
            //   _dcsGameGuiudpListener.JoinMulticastGroup(multicastaddress);

            var localEp = new IPEndPoint(IPAddress.Any,
                _settings.GetNetworkSetting(SettingsKeys.DCSIncomingGameGUIUDP));
            _dcsGameGuiudpListener.Client.Bind(localEp);
            //   activeRadioUdpClient.Client.ReceiveTimeout = 10000;

            Task.Factory.StartNew(() =>
            {
                using (_dcsGameGuiudpListener)
                {
                    //    var count = 0;
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _settings.GetNetworkSetting(SettingsKeys.DCSIncomingGameGUIUDP));
                            var bytes = _dcsGameGuiudpListener.Receive(ref groupEp);

                            var playerInfo =
                                JsonConvert.DeserializeObject<DCSPlayerSideInfo>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            if (playerInfo != null)
                            {
                                var radioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
                                //update position
                                playerInfo.Position = radioInfo.pos;
                                _clientStateSingleton.DcsPlayerSideInfo = playerInfo;
                                _clientSideUpdate();
                                //     count = 0;
                            }
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS GameGUI Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS GameGUI Message");
                        }
                    }

                    try
                    {
                        _dcsGameGuiudpListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        //used for the result
        private void StartDCSLOSBroadcastListener()
        {
            _dcsLOSListener = new UdpClient();
            _dcsLOSListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            _dcsLOSListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            var localEp = new IPEndPoint(IPAddress.Any, _settings.GetNetworkSetting(SettingsKeys.DCSLOSIncomingUDP));
            _dcsLOSListener.Client.Bind(localEp);

            Task.Factory.StartNew(() =>
            {
                using (_dcsLOSListener)
                {
                    //    var count = 0;
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _settings.GetNetworkSetting(SettingsKeys.DCSLOSIncomingUDP));
                            var bytes = _dcsLOSListener.Receive(ref groupEp);

                            /*   Logger.Debug(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));*/
                            var playerInfo =
                                JsonConvert.DeserializeObject<DCSLosCheckResult[]>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            foreach (var player in playerInfo)
                            {
                                SRClient client;

                                if (_clients.TryGetValue(player.id, out client))
                                {
                                    client.LineOfSightLoss = player.los;

                                    //  Logger.Debug(client.ToString());
                                }
                            }
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS Los Result Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS Los Result Message");
                        }
                    }

                    try
                    {
                        _dcsLOSListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS LOS Result listener ");
                    }
                }
            });
        }

        private void StartDCSLOSSender()
        {
            var _udpSocket = new UdpClient();
            var _host = new IPEndPoint(IPAddress.Loopback, _settings.GetNetworkSetting(SettingsKeys.DCSLOSOutgoingUDP));


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
                                        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(clientSubList) + "\n");

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

            if (ClientSync.ServerSettings[(int) ServerSettingType.LOS_ENABLED])
            {
                foreach (var client in clients)
                {
                    //only check if its worth it
                    if ((client.Position.x != 0) && (client.Position.z != 0) && (client.ClientGuid != _guid))
                    {
                        requests.Add(new DCSLosCheckRequest
                        {
                            id = client.ClientGuid,
                            x = client.Position.x,
                            y = client.Position.y,
                            z = client.Position.z
                        });
                    }
                }
            }
            return requests;
        }

        private bool UpdateRadio(DCSPlayerRadioInfo message)
        {
            var changed = false;


            var expansion = ClientSync.ServerSettings[(int) ServerSettingType.RADIO_EXPANSION];

            var playerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            //update common parts
            playerRadioInfo.name = message.name;
           

            if (_settings.GetClientSetting(SettingsKeys.AlwaysAllowHotasControls).BoolValue)
            {
                playerRadioInfo.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
            }
            else
            {
                playerRadioInfo.control = message.control;
            }

            playerRadioInfo.unit = message.unit;
            playerRadioInfo.pos = message.pos;

            var overrideFreqAndVol = false;

            var newAircraft = playerRadioInfo.unitId != message.unitId || !playerRadioInfo.IsCurrent();

            if (message.unitId >= DCSPlayerRadioInfo.UnitIdOffset &&
                playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset)
            {
                //overriden so leave as is
            }
            else
            {
                overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;
                playerRadioInfo.unitId = message.unitId;
            }


            if (overrideFreqAndVol)
            {
                playerRadioInfo.selected = message.selected;
                changed = true;
            }

            if (message.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            {
                playerRadioInfo.selected = message.selected;
            }


            //copy over radio names, min + max
            for (var i = 0; i < playerRadioInfo.radios.Length; i++)
            {
                var clientRadio = playerRadioInfo.radios[i];

                //if awacs NOT open -  disable radios over 3
                if (i >= message.radios.Length
                    || (RadioOverlayWindow.AwacsActive == false
                        && (i > 3 || i == 0)
                        // disable intercom and all radios over 3 if awacs panel isnt open and we're a spectator given by the UnitId
                        && playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset))
                {
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;

                    continue;
                }

                var updateRadio = message.radios[i];


                if ((updateRadio.expansion && !expansion) ||
                    (updateRadio.modulation == RadioInformation.Modulation.DISABLED))
                {
                    //expansion radio, not allowed
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;
                }
                else
                {
                    //update common parts
                    clientRadio.freqMin = updateRadio.freqMin;
                    clientRadio.freqMax = updateRadio.freqMax;

                    clientRadio.name = updateRadio.name;

                    clientRadio.modulation = updateRadio.modulation;

                    //update modes
                    clientRadio.freqMode = updateRadio.freqMode;
                    clientRadio.encMode = updateRadio.encMode;
                    clientRadio.volMode = updateRadio.volMode;

                    if ((updateRadio.freqMode == RadioInformation.FreqMode.COCKPIT) || overrideFreqAndVol)
                    {
                        if (clientRadio.freq != updateRadio.freq)
                            changed = true;

                        if (clientRadio.secFreq != updateRadio.secFreq)
                            changed = true;

                        clientRadio.freq = updateRadio.freq;

                        //default overlay to off
                        if (updateRadio.freqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            clientRadio.secFreq = 0;
                        }
                        else
                        {
                            clientRadio.secFreq = updateRadio.secFreq;
                        }

                        clientRadio.channel = updateRadio.channel;
                    }
                    else
                    {
                        if (clientRadio.secFreq != 0)
                        {
                            //put back
                            clientRadio.secFreq = updateRadio.secFreq;
                        }

                        //check we're not over a limit
                        if (clientRadio.freq > clientRadio.freqMax)
                        {
                            clientRadio.freq = clientRadio.freqMax;
                        }
                        else if (clientRadio.freq < clientRadio.freqMin)
                        {
                            clientRadio.freq = clientRadio.freqMin;
                        }
                    }

                    //reset encryption
                    if (overrideFreqAndVol)
                    {
                        clientRadio.enc = false;
                        clientRadio.encKey = 0;
                    }

                    //Handle Encryption
                    if (updateRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                    {
                        if (clientRadio.encKey == 0)
                        {
                            clientRadio.encKey = 1;
                        }
                    }
                    else if (clientRadio.encMode ==
                             RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                    {
                        clientRadio.enc = updateRadio.enc;

                        if (clientRadio.encKey == 0)
                        {
                            clientRadio.encKey = 1;
                        }
                    }
                    else if (clientRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_FULL)
                    {
                        clientRadio.enc = updateRadio.enc;
                        clientRadio.encKey = updateRadio.encKey;
                    }
                    else
                    {
                        clientRadio.enc = false;
                        clientRadio.encKey = 0;
                    }

                    //handle volume
                    if ((updateRadio.volMode == RadioInformation.VolumeMode.COCKPIT) || overrideFreqAndVol)
                    {
                        clientRadio.volume = updateRadio.volume;
                    }

                    //handle Channels load for radios
                    if (newAircraft && i > 0)
                    {
                        if (clientRadio.freqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            var channelModel = _clientStateSingleton.FixedChannels[i - 1];
                            channelModel.Max = clientRadio.freqMax;
                            channelModel.Min = clientRadio.freqMin;
                            channelModel.Reload();
                            clientRadio.channel = -1; //reset channel

                            if (_settings.GetClientSetting(SettingsKeys.AutoSelectPresetChannel).BoolValue)
                            {
                                RadioHelper.RadioChannelUp(i);
                            }
                        }
                        else
                        {
                            _clientStateSingleton.FixedChannels[i - 1].Clear();
                            //clear
                        }
                    }
                }
            }

            //change PTT last
            if (!_settings.GetClientSetting(SettingsKeys.AllowDCSPTT).BoolValue)
            {
                playerRadioInfo.ptt =false;
            }
            else
            {
                playerRadioInfo.ptt = message.ptt;
            }
           
            //                }
            //            }

            //update
            playerRadioInfo.LastUpdate = DateTime.Now.Ticks;

            return changed;
        }

        private bool IsRadioInfoStale(DCSPlayerRadioInfo radioUpdate)
        {
            //send update if our metadata is nearly stale (1 tick = 100ns, 50000000 ticks = 5s stale timer)
            if (DateTime.Now.Ticks - _clientStateSingleton.LastSent < 50000000)
            {
                Logger.Debug($"Not Stale - Tick: {DateTime.Now.Ticks} Last sent: {_clientStateSingleton.LastSent} ");
                return false;
            }
            Logger.Debug($"Stale Radio - Tick: {DateTime.Now.Ticks} Last sent: {_clientStateSingleton.LastSent} ");
            return true;
        }

        public void Stop()
        {
            _stop = true;

            try
            {
                _dcsUdpListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsGameGuiudpListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsLOSListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsRadioUpdateSender?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _udpCommandListener?.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}