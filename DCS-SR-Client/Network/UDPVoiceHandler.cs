using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using FragLabs.Audio.Codecs;
using NLog;
using static Ciribob.DCS.SimpleRadio.Standalone.Common.RadioInformation;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    internal class UdpVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IPAddress _address;
        private readonly AudioManager _audioManager;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;

        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly InputDeviceManager _inputManager;
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();
        private readonly int _port;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();

        private readonly int UDP_VOIP_TIMEOUT = 42; // seconds for timeout before redoing VoIP

        private readonly int JITTER_BUFFER = 50; //in milliseconds

        private ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private UdpClient _listener;

        private ulong _packetNumber = 1;

        private volatile bool _ptt;
        private long _lastPTTPress; // to handle dodgy PTT - release time
        private long _firstPTTPress; // to delay start PTT time

        private volatile bool _intercomPtt;

        private volatile bool _ready;

        private IPEndPoint _serverEndpoint;

        private volatile bool _stop;

        private Timer _timer;

        private long _udpLastReceived = 0;
        private DispatcherTimer _updateTimer;

        private RadioReceivingState[] _radioReceivingState;

        public UdpVoiceHandler(string guid, IPAddress address, int port, AudioManager audioManager,
            InputDeviceManager inputManager)
        {
            _radioReceivingState = _clientStateSingleton.RadioReceivingState;

            _audioManager = audioManager;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            _guid = guid;
            _address = address;
            _port = port;

            _serverEndpoint = new IPEndPoint(_address, _port);

            _inputManager = inputManager;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _updateTimer.Tick += UpdateVOIPStatus;
            _updateTimer.Start();

            
        }

        private void UpdateVOIPStatus(object sender, EventArgs e)
        {
            TimeSpan diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

            //ping every 10 so after 40 seconds VoIP UDP issue
            if (diff.TotalSeconds > UDP_VOIP_TIMEOUT)
            {
                _clientStateSingleton.IsVoipConnected = false;
            }
            else
            {
                _clientStateSingleton.IsVoipConnected = true;
            }
        }

        private void AudioEffectCheckTick()
        {


            for (var i = 0; i < _radioReceivingState.Length; i++)
            {
                //Nothing on this radio!
                //play out if nothing after 200ms
                //and Audio hasn't been played already
                var radioState = _radioReceivingState[i];
                if ((radioState != null) && !radioState.PlayedEndOfTransmission && !radioState.IsReceiving)
                {
                    radioState.PlayedEndOfTransmission = true;

                    var radioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

                    if (!radioState.IsSimultaneous)
                    {
                        _audioManager.PlaySoundEffectEndReceive(i, radioInfo.radios[i].volume, radioInfo.radios[i].modulation);
                    }
                }
            }
        }

        public void Listen()
        {
            _udpLastReceived = 0;
            _ready = false;
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }
            // _listener.Connect(_serverEndpoint);

            //start 2 audio processing threads
            var decoderThread = new Thread(UdpAudioDecode);
            decoderThread.Start();

            var settings = GlobalSettingsStore.Instance;
            _inputManager.StartDetectPtt(pressed =>
            {
                var radios = _clientStateSingleton.DcsPlayerRadioInfo;

                var radioSwitchPtt = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
                var radioSwitchPttWhenValid = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTTOnlyWhenValid);

                //store the current PTT state and radios
                var currentRadioId = radios.selected;
                var currentPtt = _ptt;

                var ptt = false;
                var intercomPtt = false;
                foreach (var inputBindState in pressed)
                {
                    if (inputBindState.IsActive)
                    {
                        //radio switch?
                        if ((int)inputBindState.MainDevice.InputBind >= (int)InputBinding.Intercom &&
                            (int)inputBindState.MainDevice.InputBind <= (int)InputBinding.Switch10)
                        {
                            //gives you radio id if you minus 100
                            var radioId = (int)inputBindState.MainDevice.InputBind - 100;

                            if (radioId < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
                            {
                                var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[radioId];

                                if (RadioHelper.SelectRadio(radioId))
                                {
                                    //turn on PTT
                                    if (radioSwitchPttWhenValid || radioSwitchPtt)
                                    {
                                        _lastPTTPress = DateTime.Now.Ticks;
                                        ptt = true;
                                        //Store last release time
                                    }
                                }
                                else
                                {
                                    //turn on PTT even if not valid radio switch
                                    if (radioSwitchPtt)
                                    {
                                        _lastPTTPress = DateTime.Now.Ticks;
                                        ptt = true;
                                    }
                                }

                            }
                        }
                        else if (inputBindState.MainDevice.InputBind == InputBinding.Ptt)
                        {
                            _lastPTTPress = DateTime.Now.Ticks;
                            ptt = true;
                        }else if (inputBindState.MainDevice.InputBind == InputBinding.IntercomPTT)
                        {
                            intercomPtt = true;

                        }
                    }
                }

                /**
             * Handle DELAYING PTT START
             */

                if (!ptt)
                {
                    //reset
                    _firstPTTPress = -1;
                }

                if (_firstPTTPress == -1 && ptt)
                {
                    _firstPTTPress = DateTime.Now.Ticks;
                }

                if (ptt)
                {
                    //should inhibit for a bit
                    var startDiff = new TimeSpan(DateTime.Now.Ticks - _firstPTTPress);

                    var startInhibit = _globalSettings.ProfileSettingsStore
                        .GetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay);

                    if (startDiff.TotalMilliseconds < startInhibit)
                    {
                        _ptt = false;
                        _lastPTTPress = -1;
                        return;
                    }
                }

                /**
                 * End Handle DELAYING PTT START
                 */


                /**
                 * Start Handle PTT HOLD after release
                 */

                //if length is zero - no keybinds or no PTT pressed set to false
                var diff = new TimeSpan(DateTime.Now.Ticks - _lastPTTPress);

                //Release the PTT ONLY if X ms have passed and we didnt switch radios to handle
                //shitty buttons
                var releaseTime = _globalSettings.ProfileSettingsStore
                    .GetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay);

                if (!ptt
                    && releaseTime > 0
                    && diff.TotalMilliseconds <= releaseTime
                    && currentRadioId == radios.selected)
                {
                    ptt = true;
                }

                /**
                 * End Handle PTT HOLD after release
                 */


                _intercomPtt = intercomPtt;
                _ptt = ptt;
            });

            StartTimer();

            StartPing();

            _packetNumber = 1; //reset packet number
            
            while (!_stop)
            {
               if(_ready)
                {
                    try
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any, _port);
                        //   listener.Client.ReceiveTimeout = 3000;

                        var bytes = _listener.Receive(ref groupEp);

                        if (bytes?.Length == 22)
                        {
                            _udpLastReceived = DateTime.Now.Ticks;
                            Logger.Info("Received Ping Back from Server");
                        }
                        else if (bytes?.Length > 22)
                        {
                            _udpLastReceived = DateTime.Now.Ticks;
                            _encodedAudio.Add(bytes);
                        }
                    }
                    catch (Exception e)
                    {
                        //  logger.Error(e, "error listening for UDP Voip");
                    }
                }
            }

            _ready = false;

            //stop UI Refreshing
            _updateTimer.Stop();

            _clientStateSingleton.IsVoipConnected = false;
        }

        public void StartTimer()
        {
            StopTimer();

            // _jitterBuffer.Clear();
            _timer = new Timer(AudioEffectCheckTick, TimeSpan.FromMilliseconds(JITTER_BUFFER));
            _timer.Start();
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                //    _jitterBuffer.Clear();
                _timer.Stop();
                _timer = null;
            }
        }

        public void RequestStop()
        {
            _stop = true;
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }

            _stopFlag.Cancel();
            _pingStop.Cancel();

            _inputManager.StopPtt();

            StopTimer();
        }

        private SRClient IsClientMetaDataValid(string clientGuid)
        {
            if (_clients.ContainsKey(clientGuid))
            {
                var client = _clients[_guid];

                if (client != null)
                {
                    return client;
                }
            }

            return null;
        }

        private void UdpAudioDecode()
        {
            try
            {
                while (!_stop)
                {
                    try
                    {
                        var encodedOpusAudio = new byte[0];
                        _encodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);

                        var time = DateTime.Now.Ticks; //should add at the receive instead?

                        if ((encodedOpusAudio != null)
                            && (encodedOpusAudio.Length >=
                                (UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                                 UDPVoicePacket.FrequencySegmentLength)))
                        {
                            //  process
                            // check if we should play audio

                            var myClient = IsClientMetaDataValid(_guid);

                            if ((myClient != null) && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                            {
                                //Decode bytes
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                                if (udpVoicePacket != null)
                                {
                                    var globalFrequencies = _serverSettings.GlobalFrequencies;

                                    var frequencyCount = udpVoicePacket.Frequencies.Length;

                                    List<RadioReceivingPriority> radioReceivingPriorities =
                                        new List<RadioReceivingPriority>(frequencyCount);
                                    List<int> blockedRadios = CurrentlyBlockedRadios();

                                    var strictEncryption = _serverSettings.GetSettingAsBool(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION);

                                    SRClient client = null;

                                    // Parse frequencies into receiving radio priority for selection below
                                    for (var i = 0; i < frequencyCount; i++)
                                    {
                                        if (client is null)
                                        {
                                            ConnectedClientsSingleton.Instance.TryGetValue(udpVoicePacket.Guid, out client);
                                        }

                                        RadioReceivingState state = null;
                                        bool decryptable;

                                        //Check if Global
                                        bool globalFrequency = globalFrequencies.Contains(udpVoicePacket.Frequencies[i]);

                                        if (globalFrequency)
                                        {
                                            //remove encryption for global
                                            udpVoicePacket.Encryptions[i] = 0;
                                        }

                                        var radio = _clientStateSingleton.DcsPlayerRadioInfo.CanHearTransmission(
                                            udpVoicePacket.Frequencies[i],
                                            (RadioInformation.Modulation) udpVoicePacket.Modulations[i],
                                            udpVoicePacket.Encryptions[i],
                                            strictEncryption,
                                            udpVoicePacket.UnitId,
                                            blockedRadios,
                                            out state,
                                            out decryptable);

                                        float losLoss = 0.0f;
                                        double receivPowerLossPercent = 0.0;

                                        var equippedRadios = DefaultRadioInformation.GetAircraftDefaults()[client.RadioInfo.unit];
                                        var selfRadios = DefaultRadioInformation.GetAircraftDefaults()[_clientStateSingleton.DcsPlayerRadioInfo.unit];
                                        int arrayPos = Array.IndexOf(_clientStateSingleton.DcsPlayerRadioInfo.radios, radio);
                                        var selfReceiving = equippedRadios.Length < arrayPos ? selfRadios[arrayPos] : Radios.Unknown.ToString();
                                        
                                        //Array.IndexOf(_clientStateSingleton.DcsPlayerRadioInfo.radios, radio)
                                        if (radio != null && state != null)
                                        {
                                            if (
                                                radio.modulation == RadioInformation.Modulation.INTERCOM
                                                || radio.modulation == RadioInformation.Modulation.MIDS // IGNORE LOS and Distance for MIDS - we assume a Link16 Network is in place
                                                || globalFrequency
                                                || (
                                                    HasLineOfSight(udpVoicePacket, out losLoss)
                                                    && InRange(udpVoicePacket.Guid, udpVoicePacket.Frequencies[i],
                                                    DefaultRadioInformation.GetRadioDefaults()[selfReceiving.ToString()].Sensitivity,
                                                    udpVoicePacket.TransmittedPower[i],
                                                        out receivPowerLossPercent)
                                                    && !blockedRadios.Contains(state.ReceivedOn)
                                                )
                                            )
                                            {
                                                // This is already done in CanHearTransmission!!
                                                //decryptable =
                                                //    (udpVoicePacket.Encryptions[i] == radio.encKey && radio.enc) ||
                                                //    (!strictEncryption && udpVoicePacket.Encryptions[i] == 0);

                                                radioReceivingPriorities.Add(new RadioReceivingPriority()
                                                {
                                                    Decryptable = decryptable,
                                                    Encryption = udpVoicePacket.Encryptions[i],
                                                    Frequency = udpVoicePacket.Frequencies[i],
                                                    LineOfSightLoss = losLoss,
                                                    Modulation = udpVoicePacket.Modulations[i],
                                                    ReceivingPowerLossPercent = receivPowerLossPercent,
                                                    ReceivingRadio = radio,
                                                    ReceivingState = state
                                                });
                                            }
                                        }
                                    }

                                    // Sort receiving radios to play audio on correct one
                                    radioReceivingPriorities.Sort(SortRadioReceivingPriorities);

                                    if (radioReceivingPriorities.Count > 0)
                                    {
                                        

                                        //ALL GOOD!
                                        //create marker for bytes
                                        for (int i = 0; i < radioReceivingPriorities.Count; i++)
                                        {
                                            var destinationRadio = radioReceivingPriorities[i];
                                            var isSimultaneousTransmission = radioReceivingPriorities.Count > 1 && i > 0;

                                            var audio = new ClientAudio
                                            {
                                                ClientGuid = udpVoicePacket.Guid,
                                                EncodedAudio = udpVoicePacket.AudioPart1Bytes,
                                                //Convert to Shorts!
                                                ReceiveTime = DateTime.Now.Ticks,
                                                Frequency = destinationRadio.Frequency,
                                                Modulation = destinationRadio.Modulation,
                                                Volume = destinationRadio.ReceivingRadio.volume,
                                                ReceivedRadio = destinationRadio.ReceivingState.ReceivedOn,
                                                UnitId = udpVoicePacket.UnitId,
                                                Encryption = destinationRadio.Encryption,
                                                Decryptable = destinationRadio.Decryptable,
                                                // mark if we can decrypt it
                                                RadioReceivingState = destinationRadio.ReceivingState,
                                                RecevingPower =
                                                    destinationRadio
                                                        .ReceivingPowerLossPercent, //loss of 1.0 or greater is total loss
                                                LineOfSightLoss =
                                                    destinationRadio
                                                        .LineOfSightLoss, // Loss of 1.0 or greater is total loss
                                                PacketNumber = udpVoicePacket.PacketNumber,
                                                OriginalClientGuid = udpVoicePacket.OriginalClientGuid
                                            };


                                            //handle effects
                                            var radioState = _radioReceivingState[audio.ReceivedRadio];

                                            if (!isSimultaneousTransmission &&
                                                (radioState == null || radioState.PlayedEndOfTransmission ||
                                                 !radioState.IsReceiving))
                                            {
                                                var audioDecryptable = audio.Decryptable /* || (audio.Encryption == 0) <--- this has already been tested above */;

                                                //mark that we have decrypted encrypted audio for sound effects
                                                if (audioDecryptable && (audio.Encryption > 0))
                                                {
                                                    _audioManager.PlaySoundEffectStartReceive(audio.ReceivedRadio,
                                                        true,
                                                        audio.Volume, (Modulation) audio.Modulation);
                                                }
                                                else
                                                {
                                                    _audioManager.PlaySoundEffectStartReceive(audio.ReceivedRadio,
                                                        false,
                                                        audio.Volume,(Modulation) audio.Modulation);
                                                }
                                            }

                                            var transmitterName = "";
                                            if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.SHOW_TRANSMITTER_NAME)
                                                && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ShowTransmitterName)
                                                && _clients.TryGetValue(udpVoicePacket.Guid, out var transmittingClient))

                                            {
                                                transmitterName = transmittingClient.Name;
                                            }

                                            var newRadioReceivingState =  new RadioReceivingState
                                            {
                                                IsSecondary = destinationRadio.ReceivingState.IsSecondary,
                                                IsSimultaneous = isSimultaneousTransmission,
                                                LastReceviedAt = DateTime.Now.Ticks,
                                                PlayedEndOfTransmission = false,
                                                ReceivedOn = destinationRadio.ReceivingState.ReceivedOn,
                                                SentBy = transmitterName
                                            };

                                            _radioReceivingState[audio.ReceivedRadio] = newRadioReceivingState;

                                            if (i == 0)
                                            {

                                                bool overrideEffects = false;

                                                if (_serverSettings.GetSettingAsBool(ServerSettingsKeys
                                                    .RADIO_EFFECT_OVERRIDE))
                                                {
                                                    overrideEffects = _serverSettings.GlobalFrequencies.Contains(audio.Frequency);
                                                }

                                                _audioManager.AddClientAudio(audio, overrideEffects);
                                            }
                                        }

                                        //handle retransmission
                                        RetransmitAudio(udpVoicePacket, radioReceivingPriorities);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_stop)
                        {
                            Logger.Info(ex, "Failed to decode audio from Packet");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Stopped DeJitter Buffer");
            }
        }

        private void RetransmitAudio(UDPVoicePacket udpVoicePacket, List<RadioReceivingPriority> radioReceivingPriorities)
        {

            if (udpVoicePacket.Guid == _guid )//|| udpVoicePacket.OriginalClientGuid == _guid
            {
                return;
                //my own transmission - throw away - stops test frequencies
            }

            //Hop count can limit the retransmission too
            var nodeLimit = _serverSettings.RetransmitNodeLimit;

            if (nodeLimit < udpVoicePacket.RetransmissionCount)
            {
                //Reached hop limit - no retransmit
                return;
            }

            //Check if Global
            List<double> globalFrequencies = _serverSettings.GlobalFrequencies;

            // filter radios by ability to hear it AND decryption works
            List<RadioReceivingPriority> retransmitOn = new List<RadioReceivingPriority>();
            //artificially limit some retransmissions - if encryption fails dont retransmit

            //from the subset of receiving radios - find any other radios that have retransmit - and dont retransmit on any with the same frequency
            //to stop loops
            //and ignore global frequencies 
            //and only if we can decrypt it (or no encryption)
            //and not received on Guard
            var receivingWithRetransmit = radioReceivingPriorities.Where(receivingRadio => 
                (receivingRadio.Decryptable || (receivingRadio.Encryption == 0)) 
                && receivingRadio.ReceivingRadio.retransmit
                //check global
                && !globalFrequencies.Any(freq => DCSPlayerRadioInfo.FreqCloseEnough(receivingRadio.ReceivingRadio.freq, freq))
                && !receivingRadio.ReceivingState.IsSecondary).ToList();

            //didnt receive on any radios that we could decrypt
            //stop
            if (receivingWithRetransmit.Count == 0)
            {
                return;
            }

            //radios able to retransmit
            var radiosWithRetransmit = _clientStateSingleton.DcsPlayerRadioInfo.radios.Where(radio => radio.retransmit);

            //Check we're not retransmitting through a radio we just received on?
            foreach (var receivingRadio in receivingWithRetransmit)
            {
                radiosWithRetransmit = radiosWithRetransmit.Where(radio => !DCSPlayerRadioInfo.FreqCloseEnough(radio.freq, receivingRadio.Frequency));
            }

            var finalList = radiosWithRetransmit.ToList();

            if (finalList.Count == 0)
            {
                //quit
                return;
            }

            //From the remaining list - build up a new outgoing packet
            var frequencies = new double[finalList.Count];
            var encryptions = new byte[finalList.Count];
            var modulations = new byte[finalList.Count];

            for (int i = 0; i < finalList.Count; i++)
            {
                frequencies[i] = finalList[i].freq;
                encryptions[i] = finalList[i].enc ? (byte)finalList[i].encKey:(byte)0 ;
                modulations[i] = (byte)finalList[i].modulation;
            }

            //generate packet
            var relayedPacket = new UDPVoicePacket
            {
                GuidBytes = _guidAsciiBytes,
                AudioPart1Bytes = udpVoicePacket.AudioPart1Bytes,
                AudioPart1Length = udpVoicePacket.AudioPart1Length,
                Frequencies = frequencies,
                UnitId = _clientStateSingleton.DcsPlayerRadioInfo.unitId,
                Encryptions = encryptions,
                Modulations = modulations,
                PacketNumber = udpVoicePacket.PacketNumber, 
                OriginalClientGuidBytes = udpVoicePacket.OriginalClientGuidBytes,
                RetransmissionCount = (byte)(udpVoicePacket.RetransmissionCount+1u),
            };

            var packet = relayedPacket.EncodePacket();

            try
            {
                _listener.Send(packet, packet.Length,
                    new IPEndPoint(_address, _port));
            }
            catch (Exception)
            {
            }
        }

        private List<int> CurrentlyBlockedRadios()
        {
            List<int> transmitting = new List<int>();
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX))
            {
                return transmitting;
            }

            if (!_ptt && !_clientStateSingleton.DcsPlayerRadioInfo.ptt)
            {
                return transmitting;
            }

            //Currently transmitting - PTT must be true - figure out if we can hear on those radios

            var currentRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[_clientStateSingleton.DcsPlayerRadioInfo.selected];

            if (currentRadio.modulation == RadioInformation.Modulation.FM 
                || currentRadio.modulation == RadioInformation.Modulation.AM 
                || currentRadio.modulation == RadioInformation.Modulation.MIDS 
                || currentRadio.modulation == RadioInformation.Modulation.HAVEQUICK)
            {
                //only AM and FM block - SATCOM etc dont

                transmitting.Add(_clientStateSingleton.DcsPlayerRadioInfo.selected);
            }
 

            if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
            {
                // Skip intercom
                for (int i = 1; i < 11; i++)
                {
                    var radio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];
                    if ( (radio.modulation == RadioInformation.Modulation.FM || radio.modulation == RadioInformation.Modulation.AM )&& radio.simul &&
                        i != _clientStateSingleton.DcsPlayerRadioInfo.selected)
                    {
                        transmitting.Add(i);
                    }
                }
            }

            return transmitting;
        }

        private bool HasLineOfSight(UDPVoicePacket udpVoicePacket, out float losLoss)
        {
            losLoss = 0; //0 is NO LOSS
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED))
            {
                return true;
            }

            //anything below 30 MHz and AM ignore (AM stand-in for actual HF modulations)
            for (int i = 0; i < udpVoicePacket.Frequencies.Length; i++)
            {
                if (udpVoicePacket.Modulations[i] == (int)Modulation.AM 
                    && udpVoicePacket.Frequencies[i] <= RadioCalculator.HF_FREQUENCY_LOS_IGNORED)
                {
                    //assume HF is bouncing off the sky for now
                    return true;
                }
            }

            SRClient transmittingClient;
            if (_clients.TryGetValue(udpVoicePacket.Guid, out transmittingClient))
            {
                var myLatLng= _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition;
                var clientLatLng = transmittingClient.LatLngPosition;
                if (myLatLng == null || clientLatLng == null || !myLatLng.isValid() || !clientLatLng.isValid())
                {
                    return true;
                }
                
                losLoss = transmittingClient.LineOfSightLoss;
                return transmittingClient.LineOfSightLoss < 1.0f; // 1.0 or greater  is TOTAL loss
                
            }

            losLoss = 0;
            return false;
        }

        private bool InRange(string transmissingClientGuid, double frequency, int sensitivity, byte transmissionPower, out double signalStrength)
        {
            signalStrength = 0;
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED))
            {
                return true;
            }

            SRClient transmittingClient;
            if (_clients.TryGetValue(transmissingClientGuid, out transmittingClient))
            {
                double dist = 0;
               
                var myLatLng = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition;
                var clientLatLng = transmittingClient.LatLngPosition;
                //No DCS Position - do we have LotATC Position?
                if (myLatLng == null || clientLatLng == null || !myLatLng.isValid() || !clientLatLng.isValid())
                {
                    return true;
                }
                else
                {
                    //Calculate with Haversine (distance over ground) + Pythagoras (crow flies distance)
                    dist = RadioCalculator.CalculateDistanceHaversine(myLatLng, clientLatLng);
                }

                double max;
                if(!SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.DIFFERING_RADIO_ENABLED))
                {
                    max = RadioCalculator.FriisMaximumTransmissionRange(frequency);
                } 
                else
                {
                    max = RadioCalculator.FriisMaximumTransmissionRange(frequency, sensitivity, transmissionPower);
                }
                
                // % loss of signal
                // 0 is no loss 1.0 is full loss
                signalStrength = (dist / max);

                return max > dist;
            }

            return false;
        }

        private int SortRadioReceivingPriorities(RadioReceivingPriority x, RadioReceivingPriority y)
        {
            int xScore = 0;
            int yScore = 0;

            if (x.ReceivingRadio == null || x.ReceivingState == null)
            {
                return 1;
            }

            if (y.ReceivingRadio == null | y.ReceivingState == null)
            {
                return -1;
            }

            if (x.Decryptable)
            {
                xScore += 16;
            }

            if (y.Decryptable)
            {
                yScore += 16;
            }

            if (_clientStateSingleton.DcsPlayerRadioInfo.selected == x.ReceivingState.ReceivedOn)
            {
                xScore += 8;
            }

            if (_clientStateSingleton.DcsPlayerRadioInfo.selected == y.ReceivingState.ReceivedOn)
            {
                yScore += 8;
            }

            if (x.ReceivingRadio.volume > 0)
            {
                xScore += 4;
            }

            if (y.ReceivingRadio.volume > 0)
            {
                yScore += 4;
            }

            return yScore - xScore;
        }

        private List<RadioInformation> PTTPressed(out int sendingOn)
        {
            sendingOn = -1;
            if (_clientStateSingleton.InhibitTX.InhibitTX)
            {
                TimeSpan time = new TimeSpan(DateTime.Now.Ticks - _clientStateSingleton.InhibitTX.LastReceivedAt);

                //inhibit for up to 5 seconds since the last message from VAICOM
                if (time.TotalSeconds < 5)
                {
                    return new List<RadioInformation>();
                }
            }

            var radioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            //If its a hot intercom and thats not the currently selected radio
            //this is special logic currently for the gazelle as it has a hot mic, but no way of knowing if you're transmitting from the module itself
            //so we have to figure out what you're transmitting on in SRS
            if ((radioInfo.intercomHotMic
                && radioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT
                && radioInfo.selected != 0 && !_ptt && !radioInfo.ptt)
                || _intercomPtt)
            {
                if (radioInfo.radios[0].modulation == RadioInformation.Modulation.INTERCOM)
                {
                    var intercom = new List<RadioInformation>();
                    intercom.Add(radioInfo.radios[0]);
                    sendingOn = 0;
                    return intercom;
                }
            }

            var transmittingRadios = new List<RadioInformation>();
            if (_ptt || _clientStateSingleton.DcsPlayerRadioInfo.ptt)
            {
                // Always add currently selected radio (if valid)
                var currentSelected = _clientStateSingleton.DcsPlayerRadioInfo.selected;
                RadioInformation currentlySelectedRadio = null;
                if (currentSelected >= 0
                    && currentSelected < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
                {
                    currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[currentSelected];

                    if (currentlySelectedRadio != null && currentlySelectedRadio.modulation !=
                                                       RadioInformation.Modulation.DISABLED
                                                       && (currentlySelectedRadio.freq > 100 ||
                                                           currentlySelectedRadio.modulation ==
                                                           RadioInformation.Modulation.INTERCOM))
                    {
                        sendingOn = currentSelected;
                        transmittingRadios.Add(currentlySelectedRadio);
                    }
                }

                // Add all radios toggled for simultaneous transmission if the global flag has been set
                if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
                {
                    //dont transmit on all if the INTERCOM is selected & AWACS
                    if (currentSelected == 0 && currentlySelectedRadio.modulation == Modulation.INTERCOM && _clientStateSingleton.DcsPlayerRadioInfo.inAircraft == false)
                    {
                        //even if simul transmission is enabled - if we're an AWACS we probably dont want this
                        var intercom = new List<RadioInformation>();
                        intercom.Add(radioInfo.radios[0]);
                        sendingOn = 0;
                        return intercom;
                    }

                    var i = 0;
                    foreach (var radio in _clientStateSingleton.DcsPlayerRadioInfo.radios)
                    {
                        if (radio != null && radio.simul && radio.modulation != RadioInformation.Modulation.DISABLED
                            && (radio.freq > 100 || radio.modulation == RadioInformation.Modulation.INTERCOM)
                            && !transmittingRadios.Contains(radio)
                        ) // Make sure we don't add the selected radio twice
                        {
                            if (sendingOn == -1)
                            {
                                sendingOn = i;
                            }
                            transmittingRadios.Add(radio);
                        }

                        i++;
                    }
                }
            }

            return transmittingRadios;
        }

        public ClientAudio Send(byte[] bytes, int len)
        {
            // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
            List<RadioInformation> transmittingRadios;
            //if either PTT is true, a microphone is available && socket connected etc
            var sendingOn = -1;
            if (_ready
                && _listener != null
                && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent()
                && _audioInputSingleton.MicrophoneAvailable
                && (bytes != null)
                && (transmittingRadios = PTTPressed(out sendingOn)).Count >0 )
                //can only send if DCS is connected
            {
                try
                {

                    if (transmittingRadios.Count > 0)
                    {
                        List<double> frequencies = new List<double>(transmittingRadios.Count);
                        List<byte> encryptions = new List<byte>(transmittingRadios.Count);
                        List<byte> modulations = new List<byte>(transmittingRadios.Count);
                        List<byte> transmittingRadio = new List<byte>(transmittingRadios.Count);

                        for (int i = 0; i < transmittingRadios.Count; i++)
                        {
                            var radio = transmittingRadios[i];

                            // Further deduplicate transmitted frequencies if they have the same freq./modulation/encryption (caused by differently named radios)
                            bool alreadyIncluded = false;
                            for (int j = 0; j < frequencies.Count; j++)
                            {
                                if (frequencies[j] == radio.freq
                                    && modulations[j] == (byte) radio.modulation
                                    && encryptions[j] == (radio.enc ? radio.encKey : (byte) 0))
                                {
                                    alreadyIncluded = true;
                                    break;
                                }
                            }

                            if (alreadyIncluded)
                            {
                                continue;
                            }

                            frequencies.Add(radio.freq);

                            var radioDetails = DefaultRadioInformation.GetRadioDefaults();
                            var matchedRadio = DefaultRadioInformation.GetAircraftDefaults()[ClientStateSingleton.Instance.DcsPlayerRadioInfo.unit]
                                .Where(x => x.ToString() == radio.name
                                .Where(c => char.IsLetterOrDigit(c))
                                .ToString())
                                .ToArray();
                            byte radioNum = matchedRadio.Length > 0 ? (byte)radioDetails[matchedRadio[0].ToString()].Power : (byte)radioDetails[Radios.Unknown.ToString()].Power;

                            transmittingRadio.Add(radioNum);
                            encryptions.Add(radio.enc ? radio.encKey : (byte) 0);
                            modulations.Add((byte) radio.modulation);
                        }

                        //generate packet
                        var udpVoicePacket = new UDPVoicePacket
                        {
                            GuidBytes = _guidAsciiBytes,
                            AudioPart1Bytes = bytes,
                            AudioPart1Length = (ushort) bytes.Length,
                            Frequencies = frequencies.ToArray(),
                            UnitId = _clientStateSingleton.DcsPlayerRadioInfo.unitId,
                            Encryptions = encryptions.ToArray(),
                            Modulations = modulations.ToArray(),
                            TransmittedPower = transmittingRadio.ToArray(),
                            PacketNumber = _packetNumber++,
                            OriginalClientGuidBytes = _guidAsciiBytes
                        };

                        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                        _listener.Send(encodedUdpVoicePacket, encodedUdpVoicePacket.Length, new IPEndPoint(_address, _port));

                        var currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[sendingOn];

                        //not sending or really quickly switched sending
                        if (currentlySelectedRadio != null &&
                            (!_clientStateSingleton.RadioSendingState.IsSending || _clientStateSingleton.RadioSendingState.SendingOn != sendingOn))
                        {
                            _audioManager.PlaySoundEffectStartTransmit(sendingOn,
                                currentlySelectedRadio.enc && (currentlySelectedRadio.encKey > 0),
                                currentlySelectedRadio.volume, currentlySelectedRadio.modulation);
                        }

                        //set radio overlay state
                        _clientStateSingleton.RadioSendingState = new RadioSendingState
                        {
                            IsSending = true,
                            LastSentAt = DateTime.Now.Ticks,
                            SendingOn = sendingOn
                        };
                        var send = new ClientAudio()
                        {
                             Frequency = frequencies[0], Modulation = modulations[0],
                             EncodedAudio = bytes,
                             Encryption = 0,
                             Volume = 1,
                             Decryptable = true,
                             LineOfSightLoss = 0,
                             RecevingPower = 0,
                             ReceivedRadio = 1
                        };
                        return send;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Sending Audio Message " + e.Message);
                }
            }
            else
            {
                if (_clientStateSingleton.RadioSendingState.IsSending)
                {
                    _clientStateSingleton.RadioSendingState.IsSending = false;

                    if (_clientStateSingleton.RadioSendingState.SendingOn >= 0)
                    {
                        var radio = _clientStateSingleton.DcsPlayerRadioInfo.radios[_clientStateSingleton.RadioSendingState.SendingOn];

                        _audioManager.PlaySoundEffectEndTransmit(_clientStateSingleton.RadioSendingState.SendingOn, radio.volume, radio.modulation);
                    }
                }
            }

            return null;
        }

        private void StartPing()
        {
            Logger.Info("Pinging Server - Starting");

            byte[] message = _guidAsciiBytes;

            // Force immediate ping once to avoid race condition before starting to listen
            _listener.Send(message, message.Length, _serverEndpoint);

            var thread = new Thread(() =>
            {
                //wait for initial sync - then ping
                if (_pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    return;
                }

                _ready = true;

                while (!_stop)
                {
                    //Logger.Info("Pinging Server");
                    try
                    {
                        if (_listener != null)
                        {
                            _listener.Send(message, message.Length,_serverEndpoint);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                    }

                    //wait for cancel or quit
                    var cancelled = _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));

                    if (cancelled)
                    {
                        return;
                    }

                    TimeSpan diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

                    //reconnect to UDP - port is no good!
                    if (diff.TotalSeconds > UDP_VOIP_TIMEOUT)
                    {
                        Logger.Error("VoIP Timeout - Recreating VoIP Connection");
                        _ready = false;
                        try
                        {
                            _listener?.Close();
                        }catch(Exception ex)
                        { }

                        _listener = null;

                        _udpLastReceived = 0;

                        _listener = new UdpClient();
                        try
                        {
                            _listener.AllowNatTraversal(true);
                        }
                        catch { }

                        try
                        {
                            // Force immediate ping once to avoid race condition before starting to listen
                            _listener.Send(message, message.Length, _serverEndpoint);
                            _ready = true;
                            Logger.Error("VoIP Timeout - Success Recreating VoIP Connection");
                        }
                        catch (Exception e) {
                            Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                        }
                        
                    }
                   
                }
            });
            thread.Start();
        }
    }
}