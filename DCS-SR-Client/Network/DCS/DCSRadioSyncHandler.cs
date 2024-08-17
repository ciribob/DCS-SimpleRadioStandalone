using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.AwacsRadioOverlayWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Newtonsoft.Json;
using NLog;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS
{
    public class DCSRadioSyncHandler
    {
        private readonly DCSRadioSyncManager.SendRadioUpdate _radioUpdate;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

        private static readonly int RADIO_UPDATE_PING_INTERVAL = 60; //send update regardless of change every X seconds


        private UdpClient _dcsUdpListener;
        private UdpClient _dcsRadioUpdateSender;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private volatile bool _stop;

        public delegate void NewAircraft(string name, int seat);

        private readonly NewAircraft _newAircraftCallback;

        private long _identStart = 0;

        public DCSRadioSyncHandler(DCSRadioSyncManager.SendRadioUpdate radioUpdate, NewAircraft _newAircraft)
        {
            _radioUpdate = radioUpdate;
            _newAircraftCallback = _newAircraft;
        }

        public void Start()
        {
            //reset last sent
            _clientStateSingleton.LastSent = 0;

            Task.Factory.StartNew(() =>
            {
                while (!_stop)
                {
                    
                    try
                    {
                        var localEp = new IPEndPoint(IPAddress.Any, _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingUDP));
                        _dcsUdpListener = new UdpClient(localEp);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Unable to bind to the DCS Export Listener Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingUDP)}");
                        Thread.Sleep(500);
                    }
                }

                while (!_stop)
                {
                    try
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any,0);
                        var bytes = _dcsUdpListener.Receive(ref groupEp);

                        var str = Encoding.UTF8.GetString(
                            bytes, 0, bytes.Length).Trim();

                        var message =
                            JsonConvert.DeserializeObject<DCSPlayerRadioInfo>(str);

                        Logger.Debug($"Recevied Message from DCS {str}");

                        if (!string.IsNullOrWhiteSpace(message.name) && message.name != "Unknown" && message.name != _clientStateSingleton.LastSeenName)
                        {
                            _clientStateSingleton.LastSeenName = message.name;
                        }

                        _clientStateSingleton.DcsExportLastReceived = DateTime.Now.Ticks;

                        //sync with others
                        //Radio info is marked as Stale for FC3 aircraft after every frequency change

                        ProcessRadioInfo(message);
                    }
                    catch (SocketException e)
                    {
                        // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                        if (!_stop)
                        {
                            Logger.Error(e, "SocketException Handling DCS Message");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Handling DCS Message");
                    }
                }

                try
                {
                    _dcsUdpListener.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stopping DCS listener ");
                }
                
            });
        }


        public void ProcessRadioInfo(DCSPlayerRadioInfo message)
        {
          
            // determine if its changed by comparing old to new
            var update = UpdateRadio(message);

            //send to DCS UI
            SendRadioUpdateToDCS();

            Logger.Debug("Update sent to DCS");

            var diff = new TimeSpan( DateTime.Now.Ticks - _clientStateSingleton.LastSent);

            if (update 
                || _clientStateSingleton.LastSent < 1 
                || diff.TotalSeconds > 60)
            {
                Logger.Debug("Sending Radio Info To Server - Update");
                _clientStateSingleton.LastSent = DateTime.Now.Ticks;
                _radioUpdate();
            }
        }

        //send updated radio info back to DCS for ingame GUI
        private void SendRadioUpdateToDCS()
        {
            if (_dcsRadioUpdateSender == null)
            {
                _dcsRadioUpdateSender = new UdpClient();
            }

            try
            {
                var connectedClientsSingleton = ConnectedClientsSingleton.Instance;
                int[] tunedClients = new int[11];

                if (_clientStateSingleton.IsConnected
                    && _clientStateSingleton.DcsPlayerRadioInfo !=null
                    && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                {

                    for (int i = 0; i < tunedClients.Length; i++)
                    {
                        var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];
                        
                        if (clientRadio.modulation != RadioInformation.Modulation.DISABLED)
                        {
                            tunedClients[i] = connectedClientsSingleton.ClientsOnFreq(clientRadio.freq, clientRadio.modulation);
                        }
                    }
                }
                
                //get currently transmitting or receiving
                var combinedState = new CombinedRadioState()
                {
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                    RadioSendingState = _clientStateSingleton.RadioSendingState,
                    RadioReceivingState = _clientStateSingleton.RadioReceivingState,
                    ClientCountConnected = _clients.Total,
                    TunedClients = tunedClients,
                };

                var message = JsonConvert.SerializeObject(combinedState, new JsonSerializerSettings
                {
                 //   NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new JsonDCSPropertiesResolver(),
                }) + "\n";

                var byteData =
                    Encoding.UTF8.GetBytes(message);

                //Logger.Info("Sending Update over UDP 7080 DCS - 7082 Flight Panels: \n"+message);

                _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.OutgoingDCSUDPInfo))); //send to DCS
                _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys
                            .OutgoingDCSUDPOther))); // send to Flight Control Panels
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending DCS Radio Update Message");
            }
        }

        private bool UpdateRadio(DCSPlayerRadioInfo message)
        {
            var expansion = _serverSettings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION);

            if (expansion)
            {
                //override the server side setting
                expansion = !_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.DisableExpansionRadios);
            }

            var playerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            //copy and compare to look for changes
            var beforeUpdate = playerRadioInfo.DeepClone();

            //update common parts
            playerRadioInfo.name = message.name;
            playerRadioInfo.inAircraft = message.inAircraft;
            playerRadioInfo.intercomHotMic = message.intercomHotMic;
            playerRadioInfo.capabilities = message.capabilities;
            playerRadioInfo.ambient = message.ambient;

            //round volume to nearest 10% to reduce messages?
            //TODO move this out to be like position so its a special small update?
            //TODO check this
            playerRadioInfo.ambient.vol = playerRadioInfo.ambient.vol; // (float)(Math.Round(playerRadioInfo.ambient.vol / 10f, MidpointRounding.AwayFromZero) * 10.0f);

            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls))
            {
                message.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
                playerRadioInfo.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
            }
            else
            {
                playerRadioInfo.control = message.control;
            }

            playerRadioInfo.simultaneousTransmissionControl = message.simultaneousTransmissionControl;
            
            if (!_clientStateSingleton.ShouldUseLotATCPosition())
            {
                _clientStateSingleton.UpdatePlayerPosition(message.latLng);
            } 
            
            
            //save unit id while catching the special exceptions
            var combinedArmsUnitId = DCSPlayerRadioInfo.UnitIdOffsetCombinedArms + (uint)_clientStateSingleton.IntercomOffset;
            
            switch (message.unit)
            {
                case "artillery_commander": 
                    playerRadioInfo.unit = "Tactical cmdr";
                    playerRadioInfo.unitId = combinedArmsUnitId; 
                    break;
                case "forward_observer": 
                    playerRadioInfo.unit = "JTAC/Operator";
                    playerRadioInfo.unitId = combinedArmsUnitId; 
                    break;
                case "instructor": 
                    playerRadioInfo.unit = "Game Master";
                    playerRadioInfo.unitId = combinedArmsUnitId; 
                    break;
                case "observer": 
                    playerRadioInfo.unit = "Observer";
                    playerRadioInfo.unitId = combinedArmsUnitId; 
                    break;
                case "EAM": 
                    playerRadioInfo.unit = "EAM";
                    playerRadioInfo.unitId = combinedArmsUnitId; 
                    break;
                
                case "?": 
                    //Spectators get put into a different bucket from Combined Arms units.
                    playerRadioInfo.unit = "Spectator";
                    playerRadioInfo.unitId = DCSPlayerRadioInfo.UnitIdOffset + (uint)_clientStateSingleton.IntercomOffset;
                    break;
                case "spectator": 
                    //Spectators get put into a different bucket from Combined Arms units.
                    playerRadioInfo.unit = "Spectator";
                    playerRadioInfo.unitId = DCSPlayerRadioInfo.UnitIdOffset + (uint)_clientStateSingleton.IntercomOffset;
                    break;
                
                default:
                    playerRadioInfo.unit = message.unit;
                    playerRadioInfo.unitId = message.unitId;
                    break;
            }
            
            playerRadioInfo.seat = message.seat;
            
            
            var overrideFreqAndVol = false;
            var newAircraft = playerRadioInfo.seat != message.seat || !playerRadioInfo.IsCurrent();

            switch (playerRadioInfo.unitId)
            {
                case uint unitId when unitId >= DCSPlayerRadioInfo.UnitIdOffsetCombinedArms:
                    newAircraft = playerRadioInfo.unitId != combinedArmsUnitId || newAircraft;
                    overrideFreqAndVol = playerRadioInfo.unitId != combinedArmsUnitId;
                    break;
                case uint unitId when unitId < DCSPlayerRadioInfo.UnitIdOffsetCombinedArms
                                      && unitId >= DCSPlayerRadioInfo.UnitIdOffset:
                    var spectatorOffset = DCSPlayerRadioInfo.UnitIdOffset + (uint)_clientStateSingleton.IntercomOffset;
                    newAircraft = playerRadioInfo.unitId != spectatorOffset || newAircraft;
                    overrideFreqAndVol = playerRadioInfo.unitId != spectatorOffset;
                    break;
                
                default:
                    newAircraft = playerRadioInfo.unitId != message.unitId || newAircraft;
                    overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;
                    break;
            }
            
            if (newAircraft)
            {
                if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoSelectSettingsProfile))
                {
                    _newAircraftCallback(message.unit, message.seat);
                }

                playerRadioInfo.iff = message.iff;
            }

            if (overrideFreqAndVol)
            {
                playerRadioInfo.selected = message.selected;
            }

            if (playerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            {
                playerRadioInfo.selected = message.selected;
            }

            bool simul = false;


            //copy over radio names, min + max
            for (var i = 0; i < playerRadioInfo.radios.Length; i++)
            {
                var clientRadio = playerRadioInfo.radios[i];

                //if we have more radios than the message has
                if (i >= message.radios.Length)
                {
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.retransmit = false;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";
                    clientRadio.rtMode = RadioInformation.RetransmitMode.DISABLED;
                    clientRadio.retransmit = false;

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.guardFreqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;
                    clientRadio.rxOnly = false;

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
                    clientRadio.retransmit = false;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";
                    clientRadio.rtMode = RadioInformation.RetransmitMode.DISABLED;
                    clientRadio.retransmit = false;

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.guardFreqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;
                    clientRadio.rxOnly = false;
                }
                else
                {
                    //update common parts
                    clientRadio.freqMin = updateRadio.freqMin;
                    clientRadio.freqMax = updateRadio.freqMax;

                    if (playerRadioInfo.simultaneousTransmissionControl == DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
                    { 
                        clientRadio.simul = updateRadio.simul;
                    }

                    if (updateRadio.simul)
                    {
                        simul = true;
                    }

                    clientRadio.name = updateRadio.name;

                    clientRadio.modulation = updateRadio.modulation;

                    //update modes
                    clientRadio.freqMode = updateRadio.freqMode;
                    clientRadio.guardFreqMode = updateRadio.guardFreqMode;
                    clientRadio.rtMode = updateRadio.rtMode;
                    clientRadio.rxOnly = updateRadio.rxOnly;

                    if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION))
                    {
                        clientRadio.encMode = updateRadio.encMode;
                    }
                    else
                    {
                        clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    }

                    clientRadio.volMode = updateRadio.volMode;

                    if ((updateRadio.freqMode == RadioInformation.FreqMode.COCKPIT) || overrideFreqAndVol)
                    {
                        clientRadio.freq = updateRadio.freq;

                        if (newAircraft && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            //default guard to off
                            clientRadio.secFreq = 0;
                        }
                        else
                        {
                            if (clientRadio.secFreq != 0 && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                            {
                                //put back
                                clientRadio.secFreq = updateRadio.secFreq;
                            }
                            else if (clientRadio.secFreq == 0 && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                            {
                                clientRadio.secFreq = 0;
                            }
                            else
                            {
                                clientRadio.secFreq = updateRadio.secFreq;
                            }

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

                    //handle Retransmit mode
                    if ((updateRadio.rtMode == RadioInformation.RetransmitMode.COCKPIT))
                    {
                        clientRadio.rtMode = updateRadio.rtMode;
                        clientRadio.retransmit = updateRadio.retransmit;
                    }else if (updateRadio.rtMode == RadioInformation.RetransmitMode.DISABLED)
                    {
                        clientRadio.rtMode = updateRadio.rtMode;
                        clientRadio.retransmit = false;
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
                            var preselectedChannel = clientRadio.channel;

                            clientRadio.channel = -1; //reset channel

                            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AutoSelectPresetChannel))
                            {
                                RadioHelper.RadioChannelUp(i);
                            }
                            else if (_clientStateSingleton.ExternalAWACSModeConnected)
                            {
                                if (preselectedChannel != -1)
                                {
                                    // Keep whatever channel they preset in the json when in EAM mode.
                                    channelModel.SelectedPresetChannel = channelModel.PresetChannels[preselectedChannel - 1];
                                    RadioHelper.SelectRadioChannel(channelModel.SelectedPresetChannel, i);
                                }
                                
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


            if (playerRadioInfo.simultaneousTransmissionControl ==
                DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
            {
                playerRadioInfo.simultaneousTransmission = simul;
            }

            //change PTT last
            if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AllowDCSPTT))
            {
                playerRadioInfo.ptt = false;
            }
            else
            {
                playerRadioInfo.ptt = message.ptt;
            }

            //HANDLE IFF/TRANSPONDER UPDATE
            //TODO tidy up the IFF/Transponder handling and this giant function in general as its silly big :(


            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .AlwaysAllowTransponderOverlay))
            {
                if (message.iff.control != Transponder.IFFControlMode.DISABLED)
                {
                    playerRadioInfo.iff.control = Transponder.IFFControlMode.OVERLAY;
                    message.iff.control = Transponder.IFFControlMode.OVERLAY;
                }
            }

            if (message.iff.control == Transponder.IFFControlMode.COCKPIT)
            {
                playerRadioInfo.iff = message.iff;
            }


            //HANDLE MIC IDENT
            if (!playerRadioInfo.ptt && playerRadioInfo.iff.mic >0 && _clientStateSingleton.RadioSendingState.IsSending)
            {
                if (_clientStateSingleton.RadioSendingState.SendingOn == playerRadioInfo.iff.mic)
                {
                    playerRadioInfo.iff.status = Transponder.IFFStatus.IDENT;
                }
            }

            //Handle IDENT only lasting for 40 seconds at most - need to toggle it
            if (playerRadioInfo.iff.status == Transponder.IFFStatus.IDENT)
            {
                if (_identStart == 0)
                {
                    _identStart = DateTime.Now.Ticks;
                }

                if (TimeSpan.FromTicks(DateTime.Now.Ticks -_identStart).TotalSeconds > 40)
                {
                    playerRadioInfo.iff.status = Transponder.IFFStatus.NORMAL;
                }

            }
            else
            {
                _identStart = 0;
            }

            //                }
            //            }

            //update
            playerRadioInfo.LastUpdate = DateTime.Now.Ticks;

            return !beforeUpdate.Equals(playerRadioInfo);
        }

        public void Stop()
        {
            _stop = true;
            try
            {
                _dcsUdpListener?.Close();
            }
            catch (Exception)
            {
            }

            try
            {
                _dcsRadioUpdateSender?.Close();
            }
            catch (Exception)
            {
                //IGNORE
            }

            _clientStateSingleton.DcsExportLastReceived = -1;
        }

    }
}
