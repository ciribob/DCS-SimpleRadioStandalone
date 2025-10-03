using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class DCSRadioSyncHandler : IHandle<EAMConnectedMessage>, IHandle<EAMDisconnectMessage>,
    IHandle<TCPClientStatusMessage>
{
    public static readonly string AWACS_RADIOS_FILE = "awacs-radios.json";
    public static readonly string AWACS_RADIOS_CUSTOM_FILE = "awacs-radios-custom.json";
    
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly int RADIO_UPDATE_PING_INTERVAL = 60; //send update regardless of change every X seconds
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private UdpClient _dcsRadioUpdateSender;

    private UdpClient _dcsUdpListener;

    private long _identStart;

    private volatile bool _stop;

    private volatile bool _stopExternalAWACSMode;

    public DCSRadioSyncHandler()
    {
    }

    private string PresetsFolder => _globalSettings.GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;

    public Task HandleAsync(EAMConnectedMessage message, CancellationToken cancellationToken)
    {
        StartExternalAWACSModeLoop(message.ClientCoalition);
        return Task.CompletedTask;
    }

    public Task HandleAsync(EAMDisconnectMessage message, CancellationToken cancellationToken)
    {
        StopExternalAWACSModeLoop();
        return Task.CompletedTask;
    }

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        if (!message.Connected)
        {
            StopExternalAWACSModeLoop();
        }

        return Task.CompletedTask;
    }

    public void Start()
    {
        EventBus.Instance.SubscribeOnUIThread(this);
        
        //reset last sent
        _clientStateSingleton.LastSent = 0;

        Task.Factory.StartNew(() =>
        {
            while (!_stop)
                try
                {
                    var localEp = new IPEndPoint(IPAddress.Any,
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingUDP));
                    _dcsUdpListener = new UdpClient(localEp);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex,
                        $"Unable to bind to the DCS Export Listener Socket Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingUDP)}");
                    Thread.Sleep(500);
                }

            while (!_stop)
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = _dcsUdpListener.Receive(ref groupEp);

                    var str = Encoding.UTF8.GetString(
                        bytes, 0, bytes.Length).Trim();

                    var message =
                        JsonSerializer.Deserialize<DCSPlayerRadioInfo>(str, new JsonSerializerOptions() { IncludeFields = true });

                    Logger.Debug($"Recevied Message from DCS {str}");

                    if (!string.IsNullOrWhiteSpace(message.name) && message.name != "Unknown" &&
                        message.name != _clientStateSingleton.LastSeenName)
                        _clientStateSingleton.LastSeenName = message.name;

                    _clientStateSingleton.DcsExportLastReceived = DateTime.Now.Ticks;
                    
                    //Ignore DCS if we're in EAM mode
                    if (!_clientStateSingleton.ExternalAWACSModelSelected)
                    {
                        //sync with others
                        //Radio info is marked as Stale for FC3 aircraft after every frequency change
                        ProcessRadioInfo(message);
                    }
                }
                catch (SocketException e)
                {
                    // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                    if (!_stop) Logger.Error(e, "SocketException Handling DCS Message");
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Handling DCS Message");
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

        var diff = new TimeSpan(DateTime.Now.Ticks - _clientStateSingleton.LastSent);

        if (update
            || _clientStateSingleton.LastSent < 1
            || diff.TotalSeconds > RADIO_UPDATE_PING_INTERVAL)
        {
            Logger.Debug("Sending Radio Info To Server - Update");
            _clientStateSingleton.LastSent = DateTime.Now.Ticks;

            //TODO do this through the singleton so its not a mess
            //Full Update send over TCP
            EventBus.Instance.PublishOnCurrentThreadAsync(new UnitUpdateMessage()
            {
                FullUpdate = true,
                UnitUpdate = new SRClientBase()
                {
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo.ConvertToRadioBase(),
                    ClientGuid = _clientStateSingleton.ShortGUID,
                    Coalition = _clientStateSingleton.PlayerCoaltionLocationMetadata.side,
                    LatLngPosition = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition,
                    Seat = _clientStateSingleton.PlayerCoaltionLocationMetadata.seat,
                    Name = _clientStateSingleton.LastSeenName,
                    AllowRecord = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowRecording)
                }
            });
        }
    }

    //send updated radio info back to DCS for ingame GUI
    private void SendRadioUpdateToDCS()
    {
        if (_dcsRadioUpdateSender == null) _dcsRadioUpdateSender = new UdpClient();

        try
        {
            var tunedClients = new int[11];

            if (_clientStateSingleton.IsConnected
                && _clientStateSingleton.DcsPlayerRadioInfo != null
                && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                for (var i = 0; i < tunedClients.Length; i++)
                {
                    var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];

                    if (clientRadio.modulation != Modulation.DISABLED)
                        tunedClients[i] =
                            _clientStateSingleton.ClientsOnFreq(clientRadio.freq, clientRadio.modulation);
                }

            //get currently transmitting or receiving
            var combinedState = new CombinedRadioState
            {
                RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                RadioSendingState = _clientStateSingleton.RadioSendingState,
                RadioReceivingState = _clientStateSingleton.RadioReceivingState,
                ClientCountConnected = _clients.Total,
                TunedClients = tunedClients
            };

            var message = JsonSerializer.Serialize(combinedState, new JsonSerializerOptions()
            {

                //   NullValueHandling = NullValueHandling.Ignore,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                {
                    Modifiers = { JsonDCSPropertiesResolver.StripDCSIgnored },
                },
                IncludeFields = true,
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
            //override the server side setting
            expansion = !_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .DisableExpansionRadios);

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
        playerRadioInfo.ambient.vol =
            message.ambient
                .vol; // (float)(Math.Round(playerRadioInfo.ambient.vol / 10f, MidpointRounding.AwayFromZero) * 10.0f);

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
            _clientStateSingleton.UpdatePlayerPosition(message.latLng);

        var overrideFreqAndVol = false;

        var newAircraft = playerRadioInfo.unitId != message.unitId || playerRadioInfo.seat != message.seat ||
                          playerRadioInfo.unit != message.unit ||
                          !playerRadioInfo.IsCurrent();

        playerRadioInfo.unit = message.unit;
        overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;

        //save unit id
        playerRadioInfo.unitId = message.unitId;
        playerRadioInfo.seat = message.seat;


        if (newAircraft)
        {
            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoSelectSettingsProfile))
            {
                //TODO handle profile selection when switching aircraft
                //  _newAircraftCallback(message.unit, message.seat);
                //send message to UI thread on event bus and switch the profile
                EventBus.Instance.PublishOnUIThreadAsync(new NewUnitEnteredMessage()
                {
                    Unit = message.unit,
                    Seat = message.seat
                });
            }


            playerRadioInfo.iff = message.iff;
        }

        if (overrideFreqAndVol) playerRadioInfo.selected = message.selected;

        if (playerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            playerRadioInfo.selected = message.selected;

        var simul = false;


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
                clientRadio.modulation = Modulation.DISABLED;
                clientRadio.name = "No Radio";
                clientRadio.rtMode = DCSRadio.RetransmitMode.DISABLED;
                clientRadio.retransmit = false;

                clientRadio.freqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
                clientRadio.volMode = DCSRadio.VolumeMode.COCKPIT;
                clientRadio.rxOnly = false;

                continue;
            }

            var updateRadio = message.radios[i];


            if ((updateRadio.expansion && !expansion) ||
                updateRadio.modulation == Modulation.DISABLED)
            {
                //expansion radio, not allowed
                clientRadio.freq = 1;
                clientRadio.freqMin = 1;
                clientRadio.freqMax = 1;
                clientRadio.secFreq = 0;
                clientRadio.retransmit = false;
                clientRadio.modulation = Modulation.DISABLED;
                clientRadio.name = "No Radio";
                clientRadio.rtMode = DCSRadio.RetransmitMode.DISABLED;
                clientRadio.retransmit = false;

                clientRadio.freqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
                clientRadio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
                clientRadio.volMode = DCSRadio.VolumeMode.COCKPIT;
                clientRadio.rxOnly = false;
            }
            else
            {
                //update common parts
                clientRadio.freqMin = updateRadio.freqMin;
                clientRadio.freqMax = updateRadio.freqMax;

                if (playerRadioInfo.simultaneousTransmissionControl ==
                    DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
                    clientRadio.simul = updateRadio.simul;

                if (updateRadio.simul) simul = true;

                clientRadio.name = updateRadio.name;
                clientRadio.model = updateRadio.model;

                clientRadio.modulation = updateRadio.modulation;

                //update modes
                clientRadio.freqMode = updateRadio.freqMode;
                clientRadio.guardFreqMode = updateRadio.guardFreqMode;
                clientRadio.rtMode = updateRadio.rtMode;
                clientRadio.rxOnly = updateRadio.rxOnly;

                if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION))
                    clientRadio.encMode = updateRadio.encMode;
                else
                    clientRadio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;

                clientRadio.volMode = updateRadio.volMode;

                if (updateRadio.freqMode == DCSRadio.FreqMode.COCKPIT || overrideFreqAndVol)
                {
                    clientRadio.freq = updateRadio.freq;

                    if (newAircraft && updateRadio.guardFreqMode == DCSRadio.FreqMode.OVERLAY)
                    {
                        //default guard to off
                        clientRadio.secFreq = 0;
                    }
                    else
                    {
                        if (clientRadio.secFreq != 0 && updateRadio.guardFreqMode == DCSRadio.FreqMode.OVERLAY)
                            //put back
                            clientRadio.secFreq = updateRadio.secFreq;
                        else if (clientRadio.secFreq == 0 &&
                                 updateRadio.guardFreqMode == DCSRadio.FreqMode.OVERLAY)
                            clientRadio.secFreq = 0;
                        else
                            clientRadio.secFreq = updateRadio.secFreq;
                    }

                    clientRadio.channel = updateRadio.channel;
                }
                else
                {
                    if (clientRadio.secFreq != 0)
                        //put back
                        clientRadio.secFreq = updateRadio.secFreq;

                    //check we're not over a limit
                    if (clientRadio.freq > clientRadio.freqMax)
                        clientRadio.freq = clientRadio.freqMax;
                    else if (clientRadio.freq < clientRadio.freqMin) clientRadio.freq = clientRadio.freqMin;
                }

                //reset encryption
                if (overrideFreqAndVol)
                {
                    clientRadio.enc = false;
                    clientRadio.encKey = 0;
                }

                //Handle Encryption
                if (updateRadio.encMode == DCSRadio.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                {
                    if (clientRadio.encKey == 0) clientRadio.encKey = 1;
                }
                else if (clientRadio.encMode ==
                         DCSRadio.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                {
                    clientRadio.enc = updateRadio.enc;

                    if (clientRadio.encKey == 0) clientRadio.encKey = 1;
                }
                else if (clientRadio.encMode == DCSRadio.EncryptionMode.ENCRYPTION_FULL)
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
                if (updateRadio.volMode == DCSRadio.VolumeMode.COCKPIT || overrideFreqAndVol)
                    clientRadio.volume = updateRadio.volume;

                //handle Retransmit mode
                if (updateRadio.rtMode == DCSRadio.RetransmitMode.COCKPIT)
                {
                    clientRadio.rtMode = updateRadio.rtMode;
                    clientRadio.retransmit = updateRadio.retransmit;
                }
                else if (updateRadio.rtMode == DCSRadio.RetransmitMode.DISABLED)
                {
                    clientRadio.rtMode = updateRadio.rtMode;
                    clientRadio.retransmit = false;
                }

                //handle Channels load for radios
                if (newAircraft && i > 0)
                {
                    if (clientRadio.freqMode == DCSRadio.FreqMode.OVERLAY)
                    {
                        var channelModel = _clientStateSingleton.FixedChannels[i - 1];
                        channelModel.Max = clientRadio.freqMax;
                        channelModel.Min = clientRadio.freqMin;
                        channelModel.Reload();
                        var preselectedChannel = clientRadio.channel;

                        clientRadio.channel = -1; //reset channel

                        if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                                .AutoSelectPresetChannel))
                            RadioHelper.RadioChannelUp(i);
                        else if (_clientStateSingleton.ExternalAWACSModeConnected)
                            if (preselectedChannel != -1)
                            {
                                // Keep whatever channel they preset in the json when in EAM mode.
                                channelModel.SelectedPresetChannel =
                                    channelModel.PresetChannels[preselectedChannel - 1];
                                RadioHelper.SelectRadioChannel(channelModel.SelectedPresetChannel, i);
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
            playerRadioInfo.simultaneousTransmission = simul;

        //change PTT last
        if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AllowDCSPTT))
            playerRadioInfo.ptt = false;
        else
            playerRadioInfo.ptt = message.ptt;

        //HANDLE IFF/TRANSPONDER UPDATE
        //TODO tidy up the IFF/Transponder handling and this giant function in general as its silly big :(


        if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .AlwaysAllowTransponderOverlay))
            if (message.iff.control != Transponder.IFFControlMode.DISABLED)
            {
                playerRadioInfo.iff.control = Transponder.IFFControlMode.OVERLAY;
                message.iff.control = Transponder.IFFControlMode.OVERLAY;
            }

        if (message.iff.control == Transponder.IFFControlMode.COCKPIT) playerRadioInfo.iff = message.iff;


        //HANDLE MIC IDENT
        if (!playerRadioInfo.ptt && playerRadioInfo.iff.mic > 0 && _clientStateSingleton.RadioSendingState.IsSending)
            if (_clientStateSingleton.RadioSendingState.SendingOn == playerRadioInfo.iff.mic)
                playerRadioInfo.iff.status = Transponder.IFFStatus.IDENT;

        //Handle IDENT only lasting for 40 seconds at most - need to toggle it
        if (playerRadioInfo.iff.status == Transponder.IFFStatus.IDENT)
        {
            if (_identStart == 0) _identStart = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(DateTime.Now.Ticks - _identStart).TotalSeconds > 40)
                playerRadioInfo.iff.status = Transponder.IFFStatus.NORMAL;
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
        EventBus.Instance.Unsubcribe(this);
        StopExternalAWACSModeLoop();
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


    private void StartExternalAWACSModeLoop(int coalition)
    {
        _stopExternalAWACSMode = false;

        DCSRadio[] awacsRadios = null;

        if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .AllowServerEAMRadioPreset))
        {
            awacsRadios = processServerCustomEAMRadio(_serverSettings.CustomEAMRadios);
        }
        
        if (awacsRadios == null)
            awacsRadios = processClientCustomEAMRadio(AWACS_RADIOS_CUSTOM_FILE);

        if (awacsRadios == null)
        {
            awacsRadios = processClientCustomEAMRadio(AWACS_RADIOS_FILE);
        }

        if (awacsRadios == null)
        {
            Logger.Warn("Failed to load AWACS radio file from server, default or custom one");

            awacsRadios = new DCSRadio[Constants.MAX_RADIOS];
            for (var i = 0; i < Constants.MAX_RADIOS; i++)
                awacsRadios[i] = new DCSRadio
                {
                    freq = 1,
                    freqMin = 1,
                    freqMax = 1,
                    secFreq = 0,
                    modulation = Modulation.DISABLED,
                    name = "No Radio",
                    freqMode = DCSRadio.FreqMode.COCKPIT,
                    encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION,
                    volMode = DCSRadio.VolumeMode.COCKPIT
                };
        }

        // Force an immediate update of radio information
        _clientStateSingleton.LastSent = 0;
        _clientStateSingleton.DcsPlayerRadioInfo.LastUpdate = DateTime.Now.Ticks;
        Task.Factory.StartNew(() =>
        {
            _clientStateSingleton.ExternalAWACSModelSelected = true;
            Logger.Debug("Starting external AWACS mode loop");

            _clientStateSingleton.IntercomOffset = 1;
            while (!_stopExternalAWACSMode)
            {
                var unitId = DCSPlayerRadioInfo.UnitIdOffset + _clientStateSingleton.IntercomOffset;

                _clientStateSingleton.PlayerCoaltionLocationMetadata.side = coalition;

                //save
                ProcessRadioInfo(new DCSPlayerRadioInfo
                {
                    LastUpdate = 0,
                    control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS,
                    name = _clientStateSingleton.LastSeenName,
                    ptt = false,
                    radios = awacsRadios,
                    selected = 1,
                    latLng = new LatLngPosition { lat = 0, lng = 0, alt = 0 },
                    simultaneousTransmission = false,
                    simultaneousTransmissionControl = DCSPlayerRadioInfo.SimultaneousTransmissionControl
                        .ENABLED_INTERNAL_SRS_CONTROLS,
                    unit = "EAM",
                    //TODO set this to the same for intercom
                    unitId = (uint)unitId,
                    inAircraft = false
                });

                Thread.Sleep(200);
            }

            var radio = new DCSPlayerRadioInfo();
            radio.Reset();
            ProcessRadioInfo(radio);
            _clientStateSingleton.IntercomOffset = 1;

            Logger.Debug("Stopping external AWACS mode loop");
        });
    }

    public void StopExternalAWACSModeLoop()
    {
        _clientStateSingleton.ExternalAWACSModelSelected = false;
        _stopExternalAWACSMode = true;
    }



    private DCSRadio[] processClientCustomEAMRadio(string radioFile)
    {
        var awacsRadios = Array.Empty<DCSRadio>();
        
        string radioJson;
        var awacsRadiosFile = Path.Combine(PresetsFolder, radioFile);
   
        if (File.Exists(awacsRadiosFile))
            try
            {
                radioJson = File.ReadAllText(awacsRadiosFile);
                awacsRadios = JsonSerializer.Deserialize<DCSRadio[]>(radioJson, new JsonSerializerOptions()
                {
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    IncludeFields = true,
                });

                foreach (var radio in awacsRadios)
                    if (radio.modulation == Modulation.MIDS)
                    {
                        radio.freq = 1030100000.0;
                        radio.freqMin = 1030000000;
                        radio.freqMax = 1060000000;
                        radio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
                        radio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
                        radio.volMode = DCSRadio.VolumeMode.OVERLAY;
                        radio.freqMode = DCSRadio.FreqMode.OVERLAY;
                    }

                return awacsRadios;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex,
                    $"Failed to load {awacsRadiosFile} radio file ");
            }
       
        return null;
    }

    private DCSRadio[] processServerCustomEAMRadio(List<DCSRadioCustom> customRadios)
    {

        try
        {
            if (customRadios == null || customRadios.Count != Constants.MAX_RADIOS)
            {
                return null;
            }
                    
            var dcsRadios = new DCSRadio[Constants.MAX_RADIOS];
            
            //initialise as empty
            for (var i = 0; i < Constants.MAX_RADIOS; i++)
            {
                dcsRadios[i] = new DCSRadio
                {
                    freq = 1,
                    freqMin = 1,
                    freqMax = 1,
                    secFreq = 0,
                    modulation = Modulation.DISABLED,
                    name = "No Radio",
                    freqMode = DCSRadio.FreqMode.COCKPIT,
                    encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION,
                    volMode = DCSRadio.VolumeMode.COCKPIT
                };
            }
            
            int radioIndex = 0;
            foreach (var customRadio in customRadios)
            {
                var radio = dcsRadios[radioIndex];
                
                radio.freq = customRadio.freq;
                radio.freqMin = customRadio.freqMin;
                radio.freqMax = customRadio.freqMax;
                radio.secFreq = customRadio.secFreq;
                radio.modulation = customRadio.modulation;
                radio.name = customRadio.name;
                radio.freqMode = (DCSRadio.FreqMode) customRadio.freqMode;
                radio.encMode = (DCSRadio.EncryptionMode) customRadio.encMode;
                radio.volMode = (DCSRadio.VolumeMode) customRadio.volMode;
                radio.freqMode = (DCSRadio.FreqMode) customRadio.freqMode;
                radio.expansion = customRadio.expansion;
                radio.channel  = customRadio.channel;
                radio.enc = customRadio.enc;
                radio.guardFreqMode  = (DCSRadio.FreqMode)  customRadio.guardFreqMode;
                radio.model = customRadio.model;
                radio.name = customRadio.name;
                radio.volume = 1.0f;
                radio.rtMode = (DCSRadio.RetransmitMode) customRadio.rtMode;
                radio.rxOnly = customRadio.rxOnly;
                radio.simul =  customRadio.simul;
                radio.encKey = customRadio.encKey;
                
                //override if MIDS
                if (radio.modulation == Modulation.MIDS)
                {
                    radio.freq = 1030100000.0;
                    radio.freqMin = 1030000000;
                    radio.freqMax = 1060000000;
                    radio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
                    radio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
                    radio.volMode = DCSRadio.VolumeMode.OVERLAY;
                    radio.freqMode = DCSRadio.FreqMode.OVERLAY;
                }

                radioIndex++;
            }
            
            return dcsRadios;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, $"Unable to process Server Custom Radio {ex.Message}");   
        }
        return null;
    }
}