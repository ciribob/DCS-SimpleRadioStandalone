using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using ConnectedClientsSingleton =
    Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons.ConnectedClientsSingleton;
using RadioReceivingState = Ciribob.DCS.SimpleRadio.Standalone.Common.Models.RadioReceivingState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;

public class UDPClientAudioProcessor : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    //TODO fix this
    // private UDPCommandListener _udpCommandListener;
    // private UDPStateSender _udpStateSender;
    private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;
    private readonly AudioManager _audioManager;
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly string _guid;
    private readonly byte[] _guidAsciiBytes;
    private readonly RadioReceivingState[] _radioReceivingState;
    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
    private CancellationTokenSource _stopFlag;
    private readonly UDPVoiceHandler _udpClient;
    private readonly object lockObj = new();
    private long _firstPTTPress; // to delay start PTT time
    private volatile bool _intercomPtt;
    private long _lastPTTPress; // to handle dodgy PTT - release time

    private long _lastVOXSend;

    private ulong _packetNumber = 1;


    private volatile bool _ptt;
    private bool _stop;

    public UDPClientAudioProcessor(UDPVoiceHandler udpClient, AudioManager audioManager, string guid)
    {
        _udpClient = udpClient;
        _audioManager = audioManager;
        _guid = guid;
        _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

        _radioReceivingState = _clientStateSingleton.RadioReceivingState;
    }

    public void Dispose()
    {
        _ptt = false;
        _stopFlag?.Dispose();
        //TODO fix this
        //_udpCommandListener?.Stop();
    }

    public void Start()
    {
        _ptt = false;
        _packetNumber = 1;

        var decoderThread = new Thread(UdpAudioDecode);
        decoderThread.Start();
        InputDeviceManager.Instance.StartPTTListening(PTTHandler);
        //TODO Fix this - command listener and state sender (to DCS)
        // _udpCommandListener = new UDPCommandListener();
        // _udpCommandListener.Start();
        // _udpStateSender = new UDPStateSender();
        // _udpStateSender.Start();
    }


    private SRClientBase IsClientMetaDataValid(string clientGuid)
    {
        if (_clients.ContainsKey(clientGuid))
        {
            var client = _clients[_guid];

            if (client != null) return client;
        }

        return null;
    }


    private void RetransmitAudio(UDPVoicePacket udpVoicePacket, List<RadioReceivingPriority> radioReceivingPriorities)
    {
        if (udpVoicePacket.Guid == _guid) //|| udpVoicePacket.OriginalClientGuid == _guid
            return;
        //my own transmission - throw away - stops test frequencies
        //Hop count can limit the retransmission too
        var nodeLimit = _serverSettings.RetransmitNodeLimit;

        if (nodeLimit < udpVoicePacket.RetransmissionCount)
            //Reached hop limit - no retransmit
            return;

        //Check if Global
        var globalFrequencies = _serverSettings.GlobalFrequencies;

        // filter radios by ability to hear it AND decryption works
        var retransmitOn = new List<RadioReceivingPriority>();
        //artificially limit some retransmissions - if encryption fails dont retransmit

        //from the subset of receiving radios - find any other radios that have retransmit - and dont retransmit on any with the same frequency
        //to stop loops
        //and ignore global frequencies 
        //and only if we can decrypt it (or no encryption)
        //and not received on Guard
        var receivingWithRetransmit = radioReceivingPriorities.Where(receivingRadio =>
            (receivingRadio.Decryptable || receivingRadio.Encryption == 0)
            && receivingRadio.ReceivingRadio.retransmit
            //check global
            && !globalFrequencies.Any(freq =>
                DCSPlayerRadioInfo.FreqCloseEnough(receivingRadio.ReceivingRadio.freq, freq))
            && !receivingRadio.ReceivingState.IsSecondary
        ).ToList();

        //didnt receive on any radios that we could decrypt
        //stop
        if (receivingWithRetransmit.Count == 0) return;

        //radios able to retransmit
        var radiosWithRetransmit = _clientStateSingleton.DcsPlayerRadioInfo.radios.Where(radio => radio.retransmit);

        //Check we're not retransmitting through a radio we just received on?
        foreach (var receivingRadio in receivingWithRetransmit)
            radiosWithRetransmit = radiosWithRetransmit.Where(radio =>
                !DCSPlayerRadioInfo.FreqCloseEnough(radio.freq, receivingRadio.Frequency));

        var finalList = radiosWithRetransmit.ToList();

        if (finalList.Count == 0)
            //quit
            return;

        //From the remaining list - build up a new outgoing packet
        var frequencies = new double[finalList.Count];
        var encryptions = new byte[finalList.Count];
        var modulations = new byte[finalList.Count];

        for (var i = 0; i < finalList.Count; i++)
        {
            frequencies[i] = finalList[i].freq;
            encryptions[i] = finalList[i].enc ? finalList[i].encKey : (byte)0;
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
            RetransmissionCount = (byte)(udpVoicePacket.RetransmissionCount + 1u)
        };

        try
        {
            _udpClient.Send(relayedPacket);
        }
        catch (Exception)
        {
        }
    }

    private List<int> CurrentlyBlockedRadios()
    {
        var transmitting = new List<int>();
        if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX)) return transmitting;

        if (!_ptt && !_clientStateSingleton.DcsPlayerRadioInfo.ptt) return transmitting;

        //Currently transmitting - PTT must be true - figure out if we can hear on those radios

        var currentRadio =
            _clientStateSingleton.DcsPlayerRadioInfo.radios[_clientStateSingleton.DcsPlayerRadioInfo.selected];

        if (currentRadio.modulation == Modulation.FM
            || currentRadio.modulation == Modulation.SINCGARS
            || currentRadio.modulation == Modulation.AM
            || currentRadio.modulation == Modulation.MIDS
            || currentRadio.modulation == Modulation.HAVEQUICK)
            //only AM and FM block - SATCOM etc dont
            transmitting.Add(_clientStateSingleton.DcsPlayerRadioInfo.selected);


        if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
            // Skip intercom
            for (var i = 1; i < 11; i++)
            {
                var radio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];
                if ((radio.modulation == Modulation.FM || radio.modulation == Modulation.SINCGARS ||
                     radio.modulation == Modulation.AM) && radio.simul &&
                    i != _clientStateSingleton.DcsPlayerRadioInfo.selected)
                    transmitting.Add(i);
            }

        return transmitting;
    }

    private int SortRadioReceivingPriorities(RadioReceivingPriority x, RadioReceivingPriority y)
    {
        var xScore = 0;
        var yScore = 0;

        if (x.ReceivingRadio == null || x.ReceivingState == null) return 1;

        if ((y.ReceivingRadio == null) | (y.ReceivingState == null)) return -1;

        if (x.Decryptable) xScore += 16;

        if (y.Decryptable) yScore += 16;

        if (_clientStateSingleton.DcsPlayerRadioInfo.selected == x.ReceivingState.ReceivedOn) xScore += 8;

        if (_clientStateSingleton.DcsPlayerRadioInfo.selected == y.ReceivingState.ReceivedOn) yScore += 8;

        if (x.ReceivingRadio.volume > 0) xScore += 4;

        if (y.ReceivingRadio.volume > 0) yScore += 4;

        return yScore - xScore;
    }

    //TODO add support for HotMic and Intercom PTT
    //See UDPVoiceHandler in SRS
    private List<DCSRadio> PTTPressed(out int sendingOn, bool voice)
    {
        sendingOn = -1;
        if (_clientStateSingleton.InhibitTX.InhibitTX)
        {
            var time = new TimeSpan(DateTime.Now.Ticks - _clientStateSingleton.InhibitTX.LastReceivedAt);

            //inhibit for up to 5 seconds since the last message from VAICOM
            if (time.TotalSeconds < 5) return new List<DCSRadio>();
        }

        var radioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
        //If its a hot intercom and thats not the currently selected radio
        //this is special logic currently for the gazelle as it has a hot mic, but no way of knowing if you're transmitting from the module itself
        //so we have to figure out what you're transmitting on in SRS
        if ((radioInfo.intercomHotMic
             && radioInfo.selected != 0
             && !_ptt
             && !radioInfo.ptt
                //remove restriction on hotmic
                //   && radioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT
            )
            || _intercomPtt)
            if (radioInfo.radios[0].modulation == Modulation.INTERCOM)
            {
                var intercom = new List<DCSRadio>();
                intercom.Add(radioInfo.radios[0]);
                sendingOn = 0;

                //check if hot mic ONLY activation
                if (radioInfo.intercomHotMic && voice)
                {
                    //only send on hotmic and voice 
                    //voice is always true is voice detection is disabled
                    //now check for lastHotmicVoice
                    _lastVOXSend = DateTime.Now.Ticks;
                    return intercom;
                }

                if (radioInfo.intercomHotMic && !voice)
                {
                    var lastVOXSendDiff = new TimeSpan(DateTime.Now.Ticks - _lastVOXSend);
                    if (lastVOXSendDiff.TotalMilliseconds <
                        _globalSettings.GetClientSettingInt(GlobalSettingsKeys.VOXMinimumTime)) return intercom;

                    //VOX no longer detected
                    return new List<DCSRadio>();
                }

                return intercom;
            }

        var transmittingRadios = new List<DCSRadio>();
        if (_ptt || _clientStateSingleton.DcsPlayerRadioInfo.ptt)
        {
            // Always add currently selected radio (if valid)
            var currentSelected = _clientStateSingleton.DcsPlayerRadioInfo.selected;
            DCSRadio currentlySelectedRadio = null;
            if (currentSelected >= 0
                && currentSelected < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
            {
                currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[currentSelected];

                if (currentlySelectedRadio != null && currentlySelectedRadio.modulation !=
                                                   Modulation.DISABLED
                                                   && (currentlySelectedRadio.freq > 100 ||
                                                       currentlySelectedRadio.modulation ==
                                                       Modulation.INTERCOM)
                                                   && currentlySelectedRadio.rxOnly == false)
                {
                    sendingOn = currentSelected;
                    transmittingRadios.Add(currentlySelectedRadio);
                }
            }

            // Add all radios toggled for simultaneous transmission if the global flag has been set
            if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
            {
                //dont transmit on all if the INTERCOM is selected & AWACS
                if (currentSelected == 0 && currentlySelectedRadio.modulation == Modulation.INTERCOM &&
                    _clientStateSingleton.DcsPlayerRadioInfo.inAircraft == false)
                {
                    //even if simul transmission is enabled - if we're an AWACS we probably dont want this
                    var intercom = new List<DCSRadio>();
                    intercom.Add(radioInfo.radios[0]);
                    sendingOn = 0;
                    return intercom;
                }

                var i = 0;
                foreach (var radioBase in _clientStateSingleton.DcsPlayerRadioInfo.radios)
                {
                    var radio = radioBase;
                    if (radio != null && radio.simul && radio.modulation != Modulation.DISABLED
                        && (radio.freq > 100 || radio.modulation == Modulation.INTERCOM)
                        && radio.rxOnly == false
                        && !transmittingRadios.Contains(radio)
                       ) // Make sure we don't add the selected radio twice
                    {
                        if (sendingOn == -1) sendingOn = i;
                        transmittingRadios.Add(radio);
                    }

                    i++;
                }
            }
        }

        return transmittingRadios;
    }

    public ClientAudio Send(byte[] bytes, int len, bool voice)
    {
        // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
        List<DCSRadio> transmittingRadios;
        //if either PTT is true, a microphone is available && socket connected etc
        var sendingOn = -1;
        if (_udpClient.Ready
            && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent()
            && _audioInputSingleton.MicrophoneAvailable
            && bytes != null
            && (transmittingRadios = PTTPressed(out sendingOn, voice)).Count > 0)
            //can only send if DCS is connected
        {
            try
            {
                uint groupIdOverride = 0;
                if (transmittingRadios.Count > 0)
                {
                    var frequencies = new List<double>(transmittingRadios.Count);
                    var encryptions = new List<byte>(transmittingRadios.Count);
                    var modulations = new List<byte>(transmittingRadios.Count);

                    for (var i = 0; i < transmittingRadios.Count; i++)
                    {
                        var radio = transmittingRadios[i];

                        // Further deduplicate transmitted frequencies if they have the same freq./modulation/encryption (caused by differently named radios)
                        var alreadyIncluded = false;
                        for (var j = 0; j < frequencies.Count; j++)
                            if (frequencies[j] == radio.freq
                                && modulations[j] == (byte)radio.modulation
                                && encryptions[j] == (radio.enc ? radio.encKey : 0))
                            {
                                alreadyIncluded = true;
                                break;
                            }

                        if (alreadyIncluded) continue;

                        if(radio.modulation == Modulation.INTERCOM && radio.IntercomUnitId > 0)
                        {
                            //if we're sending to an intercom, we need to send to the unitId of the intercom
                            groupIdOverride = radio.IntercomUnitId;
                        }
                        
                        frequencies.Add(radio.freq);
                        encryptions.Add(radio.enc ? radio.encKey : (byte)0);
                        modulations.Add((byte)radio.modulation);
                    }

                    //generate packet
                    var udpVoicePacket = new UDPVoicePacket
                    {
                        GuidBytes = _guidAsciiBytes,
                        AudioPart1Bytes = bytes,
                        AudioPart1Length = (ushort)bytes.Length,
                        Frequencies = frequencies.ToArray(),
                        UnitId = _clientStateSingleton.DcsPlayerRadioInfo.unitId,
                        Encryptions = encryptions.ToArray(),
                        Modulations = modulations.ToArray(),
                        PacketNumber = _packetNumber++,
                        OriginalClientGuidBytes = _guidAsciiBytes
                    };

                    //Instructor mode and we've overriden the group ID for intercom
                    if (groupIdOverride > 0)
                    {
                        udpVoicePacket.UnitId = groupIdOverride;
                    }
                    
                    _udpClient.Send(udpVoicePacket);

                    var currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[sendingOn];

                    //not sending or really quickly switched sending
                    if (currentlySelectedRadio != null &&
                        (!_clientStateSingleton.RadioSendingState.IsSending ||
                         _clientStateSingleton.RadioSendingState.SendingOn != sendingOn))
                        _audioManager.PlaySoundEffectStartTransmit(sendingOn,
                            currentlySelectedRadio.enc && currentlySelectedRadio.encKey > 0,
                            currentlySelectedRadio.volume, currentlySelectedRadio.modulation);

                    //set radio overlay state
                    _clientStateSingleton.RadioSendingState = new RadioSendingState
                    {
                        IsSending = true,
                        LastSentAt = DateTime.Now.Ticks,
                        SendingOn = sendingOn
                    };

                    var send = new ClientAudio
                    {
                        Frequency = frequencies[0], Modulation = modulations[0],
                        EncodedAudio = bytes,
                        Encryption = encryptions[0],
                        Volume = 1,
                        Decryptable = true,
                        LineOfSightLoss = 0,
                        RecevingPower = 0,
                        ReceivedRadio = sendingOn,
                        PacketNumber = _packetNumber,
                        ReceiveTime = DateTime.Now.Ticks,
                        OriginalClientGuid = _guid,
                        Ambient = _clientStateSingleton.DcsPlayerRadioInfo.ambient
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
                    var radio = _clientStateSingleton.DcsPlayerRadioInfo.radios[
                        _clientStateSingleton.RadioSendingState.SendingOn];

                    _audioManager.PlaySoundEffectEndTransmit(_clientStateSingleton.RadioSendingState.SendingOn,
                        VolumeConversionHelper.ConvertRadioVolumeSlider(radio.volume), radio.modulation);
                }
            }
        }

        return null;
    }


    private void UdpAudioDecode()
    {
        using (_stopFlag = new CancellationTokenSource())
        {
            try
            {
                while (!_stop)
                    try
                    {
                        var encodedOpusAudio = new byte[0];
                        _udpClient.EncodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);

                        var time = DateTime.Now.Ticks; //should add at the receive instead?

                        if (encodedOpusAudio != null
                            && encodedOpusAudio.Length >=
                            UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                            UDPVoicePacket.FrequencySegmentLength)
                        {
                            //  process
                            // check if we should play audio
                            var myClient = IsClientMetaDataValid(_guid);

                            if (myClient != null && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                            {
                                //Decode bytes
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                                if (udpVoicePacket != null)
                                {
                                    var globalFrequencies = _serverSettings.GlobalFrequencies;

                                    var frequencyCount = udpVoicePacket.Frequencies.Length;

                                    var radioReceivingPriorities =
                                        new List<RadioReceivingPriority>(frequencyCount);
                                    var blockedRadios = CurrentlyBlockedRadios();

                                    var strictEncryption =
                                        _serverSettings.GetSettingAsBool(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION);

                                    // Parse frequencies into receiving radio priority for selection below
                                    for (var i = 0; i < frequencyCount; i++)
                                    {
                                        RadioReceivingState state = null;
                                        bool decryptable;

                                        //Check if Global
                                        var globalFrequency = globalFrequencies.Contains(udpVoicePacket.Frequencies[i]);

                                        if (globalFrequency)
                                            //remove encryption for global
                                            udpVoicePacket.Encryptions[i] = 0;

                                        var radio = _clientStateSingleton.DcsPlayerRadioInfo.CanHearTransmission(
                                            udpVoicePacket.Frequencies[i],
                                            (Modulation)udpVoicePacket.Modulations[i],
                                            udpVoicePacket.Encryptions[i],
                                            strictEncryption,
                                            udpVoicePacket.UnitId,
                                            blockedRadios,
                                            out state,
                                            out decryptable);

                                        var losLoss = 0.0f;
                                        var receivPowerLossPercent = 0.0;

                                        if (radio != null && state != null)
                                            if (
                                                    radio.modulation == Modulation.INTERCOM
                                                    || radio.modulation ==
                                                    Modulation
                                                        .MIDS // IGNORE LOS and Distance for MIDS - we assume a Link16 Network is in place
                                                    || globalFrequency
                                                    || (
                                                        HasLineOfSight(udpVoicePacket, out losLoss)
                                                        && InRange(udpVoicePacket.Guid, udpVoicePacket.Frequencies[i],
                                                            out receivPowerLossPercent)
                                                        && !blockedRadios.Contains(state.ReceivedOn)
                                                    )
                                                )
                                                // This is already done in CanHearTransmission!!
                                                //decryptable =
                                                //    (udpVoicePacket.Encryptions[i] == radio.encKey && radio.enc) ||
                                                //    (!strictEncryption && udpVoicePacket.Encryptions[i] == 0);
                                                radioReceivingPriorities.Add(new RadioReceivingPriority
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

                                    // Sort receiving radios to play audio on correct one
                                    radioReceivingPriorities.Sort(SortRadioReceivingPriorities);

                                    if (radioReceivingPriorities.Count > 0)
                                    {
                                        //ALL GOOD!
                                        //create marker for bytes
                                        for (var i = 0; i < radioReceivingPriorities.Count; i++)
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
                                                Volume = VolumeConversionHelper.ConvertRadioVolumeSlider(destinationRadio
                                                    .ReceivingRadio.volume),
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
                                                OriginalClientGuid = udpVoicePacket.OriginalClientGuid,
                                                IsSecondary = destinationRadio.ReceivingState.IsSecondary
                                            };

                                            var transmitterName = "";
                                            if (_clients.TryGetValue(udpVoicePacket.Guid, out var transmittingClient))
                                            {
                                                if (_serverSettings.GetSettingAsBool(ServerSettingsKeys
                                                        .SHOW_TRANSMITTER_NAME)
                                                    && _globalSettings.GetClientSettingBool(GlobalSettingsKeys
                                                        .ShowTransmitterName))
                                                    transmitterName = transmittingClient.Name;

                                                if (transmittingClient.RadioInfo?.ambient == null)
                                                {
                                                    audio.Ambient = new Ambient();
                                                }
                                                else
                                                {
                                                    audio.Ambient = transmittingClient.RadioInfo.ambient;
                                                }
                                                
                                                if (transmittingClient.Muted)
                                                {
                                                    //skip receiving this audio
                                                   continue;
                                                }

                                            }

                                            var newRadioReceivingState = new RadioReceivingState
                                            {
                                                IsSecondary = destinationRadio.ReceivingState.IsSecondary,
                                                IsSimultaneous = isSimultaneousTransmission,
                                                LastReceivedAt = DateTime.Now.Ticks,
                                                ReceivedOn = destinationRadio.ReceivingState.ReceivedOn,
                                                SentBy = transmitterName
                                            };

                                            _radioReceivingState[audio.ReceivedRadio] = newRadioReceivingState;


                                            //we now WANT to duplicate through multiple pipelines ONLY if AM blocking is on
                                            //this is a nice optimisation to save duplicated audio on servers without that setting 
                                            if (i == 0 || _serverSettings.GetSettingAsBool(ServerSettingsKeys
                                                    .IRL_RADIO_RX_INTERFERENCE))
                                            {
                                                if (_serverSettings.GetSettingAsBool(ServerSettingsKeys
                                                        .RADIO_EFFECT_OVERRIDE))
                                                {
                                                    audio.NoAudioEffects =
                                                        _serverSettings.GlobalFrequencies.Contains(audio.Frequency);
                                                    ;
                                                }

                                                _audioManager.AddClientAudio(audio);
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
                        if (!_stop) Logger.Info(ex, "Failed to decode audio from Packet");
                    }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Stopped DeJitter Buffer");
            }
        }
    }

    private bool HasLineOfSight(UDPVoicePacket udpVoicePacket, out float losLoss)
    {
        losLoss = 0; //0 is NO LOSS
        if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED)) return true;

        //anything below 30 MHz and AM ignore (AM stand-in for actual HF modulations)
        for (var i = 0; i < udpVoicePacket.Frequencies.Length; i++)
            if (udpVoicePacket.Modulations[i] == (int)Modulation.AM
                && udpVoicePacket.Frequencies[i] <= RadioCalculator.HF_FREQUENCY_LOS_IGNORED)
                //assume HF is bouncing off the sky for now
                return true;

        SRClientBase transmittingClient;
        if (_clients.TryGetValue(udpVoicePacket.Guid, out transmittingClient))
        {
            var myLatLng = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition;
            var clientLatLng = transmittingClient.LatLngPosition;
            if (myLatLng == null || clientLatLng == null || !myLatLng.IsValid() || !clientLatLng.IsValid()) return true;

            losLoss = transmittingClient.LineOfSightLoss;
            return transmittingClient.LineOfSightLoss < 1.0f; // 1.0 or greater  is TOTAL loss
        }

        losLoss = 0;
        return false;
    }

    private bool InRange(string transmissingClientGuid, double frequency, out double signalStrength)
    {
        signalStrength = 0;
        if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED)) return true;

        SRClientBase transmittingClient;
        if (_clients.TryGetValue(transmissingClientGuid, out transmittingClient))
        {
            double dist = 0;

            var myLatLng = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition;
            var clientLatLng = transmittingClient.LatLngPosition;
            //No DCS Position - do we have LotATC Position?
            if (myLatLng == null || clientLatLng == null || !myLatLng.IsValid() || !clientLatLng.IsValid()) return true;

            //Calculate with Haversine (distance over ground) + Pythagoras (crow flies distance)
            dist = RadioCalculator.CalculateDistanceHaversine(myLatLng, clientLatLng);

            var max = RadioCalculator.FriisMaximumTransmissionRange(frequency);
            // % loss of signal
            // 0 is no loss 1.0 is full loss
            signalStrength = dist / max;

            return max > dist;
        }

        return false;
    }

    private void PTTHandler(List<InputBindState> pressed)
    {
        var radios = _clientStateSingleton.DcsPlayerRadioInfo;

        var radioSwitchPtt =
            _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
        var radioSwitchPttWhenValid =
            _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
                .RadioSwitchIsPTTOnlyWhenValid);

        //store the current PTT state and radios
        var currentRadioId = radios.selected;
        var currentPtt = _ptt;

        var ptt = false;
        var intercomPtt = false;
        foreach (var inputBindState in pressed)
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
                }
                else if (inputBindState.MainDevice.InputBind == InputBinding.IntercomPTT)
                {
                    intercomPtt = true;
                }
            }

        /**
     * Handle DELAYING PTT START
     */

        if (!ptt)
            //reset
            _firstPTTPress = -1;

        if (_firstPTTPress == -1 && ptt) _firstPTTPress = DateTime.Now.Ticks;

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
            ptt = true;

        /**
         * End Handle PTT HOLD after release
         */


        _intercomPtt = intercomPtt;
        _ptt = ptt;
    }

    public void Stop()
    {
        lock (lockObj)
        {
            _stop = true;
            _stopFlag?.Cancel();
            _clientStateSingleton.RadioSendingState.IsSending = false;
            //TODO fix this
            // _udpCommandListener?.Stop();
            // _udpCommandListener = null;
            // _udpStateSender?.Stop();
            // _udpStateSender = null;
            InputDeviceManager.Instance.StopListening();
        }
    }
}