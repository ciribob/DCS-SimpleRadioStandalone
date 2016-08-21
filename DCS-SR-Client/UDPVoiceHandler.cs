using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Server;
using FragLabs.Audio.Codecs;
using NLog;
using static Ciribob.DCS.SimpleRadio.Standalone.Client.UI.InputDevice;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    internal class UdpVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly object BufferLock = new object();
        private readonly OpusDecoder _decoder;
        private readonly IPAddress _address;
        private readonly AudioManager _audioManager;
        private readonly ConcurrentDictionary<string, SRClient> _clientsList;

        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly InputDeviceManager _inputManager;
        private UdpClient _listener;

        private volatile bool _ptt;
        public static volatile RadioSendingState RadioSendingState = new RadioSendingState();
        public static volatile RadioReceivingState[] RadioReceivingState = new RadioReceivingState[4];

        private volatile bool _stop;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();

        private Cabhishek.Timers.Timer _timer;

        private readonly int JITTER_BUFFER = 80; //in milliseconds

        private static object _lock = new object();

        private List<ClientAudio> _jitterBuffer = new List<ClientAudio>();

        public UdpVoiceHandler(ConcurrentDictionary<string, SRClient> clientsList, string guid, IPAddress address,
            OpusDecoder decoder, AudioManager audioManager, InputDeviceManager inputManager)
        {
            this._decoder = decoder;
            this._audioManager = audioManager;

            this._clientsList = clientsList;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            this._guid = guid;
            this._address = address;

            this._inputManager = inputManager;
        }

        [DllImport("kernel32.dll")]
        private static extern long GetTickCount64();

        private void JitterBufferTick()
        {
            lock (_lock)
            {
                if (_jitterBuffer.Count > 0)
                {
                    //Empty Jitterbuffer
                    foreach (var clientAudio in _jitterBuffer)
                    {
                        var lastState = RadioReceivingState[clientAudio.ReceivedRadio];

                        if (lastState == null || !lastState.IsReceiving() && !IsCurrentlyTransmittingOn(clientAudio.ReceivedRadio))
                        {
                            this._audioManager.PlaySoundEffectStartTransmit(clientAudio.ReceivedRadio, false,
                                clientAudio.Volume);
                        }
                        else
                        {
                            //do nothing
                        }

                        clientAudio.RadioReceivingState.PlayedEndOfTransmission = false;
                        RadioReceivingState[clientAudio.ReceivedRadio] = clientAudio.RadioReceivingState;

                   //     Logger.Debug("Received: " + clientAudio.ReceivedRadio + " - " + (Environment.TickCount - clientAudio.RadioReceivingState.LastReceviedAt));

                        //TODO Reorder list per client to sort out packets sent in the wrong order!
                        _audioManager.AddClientAudio(clientAudio);
                    }
                    _jitterBuffer.Clear();
                }

                //check all radios for playout sound
                for (int i = 0; i < RadioReceivingState.Length; i++)
                {
                    var state = RadioReceivingState[i];
                    if (state != null &&  (!RadioDCSSyncServer.DcsPlayerRadioInfo.IsCurrent() || IsCurrentlyTransmittingOn(i)))
                    {
                        state.PlayedEndOfTransmission = true;
                    }
                    else if (state != null && !state.PlayedEndOfTransmission && !state.IsReceiving())
                    {
                        state.PlayedEndOfTransmission = true;
                        var radio = RadioDCSSyncServer.DcsPlayerRadioInfo.radios[i];


                        this._audioManager.PlaySoundEffectEndTransmit(i, false,
                            radio.volume);

                  //      Logger.Debug("Last Received: " + i + " - " + (Environment.TickCount - state.LastReceviedAt));

                    }
                    else if(state!=null)
                    {
                //        Logger.Debug("Last Received: " + i + " - " + (Environment.TickCount - state.LastReceviedAt));
                    }


                }
            }
            
        }

        public void Listen()
        {
            _listener = new UdpClient();
            _listener.AllowNatTraversal(true);

            //start 2 audio processing threads
            var decoderThread = new Thread(UdpAudioDecode);
            decoderThread.Start();

            var settings = Settings.Instance;
            _inputManager.StartDetectPtt((pressed) =>
            {
                var radios = RadioDCSSyncServer.DcsPlayerRadioInfo;

                //can be overriden by PTT Settings
                var ptt = pressed[(int)InputBinding.ModifierPtt] && pressed[(int) InputBinding.Ptt];

                //check we're allowed to switch radios
                if (radios.radioType != DCSPlayerRadioInfo.AircraftRadioType.FULL_COCKPIT_INTEGRATION)
                {
                    if (pressed[(int) InputBinding.ModifierSwitch1] && pressed[(int) InputBinding.Switch1])
                    {
                        radios.selected = 1;
                    }
                    else if (pressed[(int) InputBinding.ModifierSwitch2] && pressed[(int) InputBinding.Switch2])
                    {
                        radios.selected = 2;
                    }
                    else if (pressed[(int) InputBinding.ModifierSwitch3] && pressed[(int) InputBinding.Switch3])
                    {
                        radios.selected = 3;
                    }
                    else if (pressed[(int) InputBinding.Intercom] && pressed[(int) InputBinding.ModifierIntercom])
                    {
                        radios.selected = 0;
                    }
                }

                var radioSwitchPtt = settings.UserSettings[(int) SettingType.RadioSwitchIsPTT] == "ON";

                if (radioSwitchPtt && ((pressed[(int)InputBinding.ModifierSwitch1] && pressed[(int)InputBinding.Switch1])
                                       || (pressed[(int)InputBinding.ModifierSwitch2] && pressed[(int)InputBinding.Switch2])
                                       || (pressed[(int)InputBinding.ModifierSwitch3] && pressed[(int)InputBinding.Switch3]))
                                       || (pressed[(int)InputBinding.Intercom] && pressed[(int)InputBinding.ModifierIntercom])
                                       )
                {
                    ptt = true;
                }

                //set final PTT AFTER mods
                this._ptt = ptt;
            });

            StartPing();

            StartTimer();

            //set to false so we sent one packet to open up the radio
            //automatically rather than the user having to press Send
            this.hasSentVoicePacket = false;

            while (!_stop)
            {
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, 5010);
                    //   listener.Client.ReceiveTimeout = 3000;

                    var bytes = _listener.Receive(ref groupEp);

                    if (bytes != null && bytes.Length > 36)
                    {
                        _encodedAudio.Add(bytes);
                    }
                    else if (bytes != null && bytes.Length == 15 && bytes[0] == 1 && bytes[14] == 15)
                    {
                        Logger.Info("Received Ping Back from Server");
                    }
                }
                catch (Exception e)
                {
                    //  logger.Error(e, "error listening for UDP Voip");
                }
            }

            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }
        }

        public void StartTimer()
        {
            StopTimer();
            lock (_lock)
            {
                _jitterBuffer.Clear();
                _timer = new Cabhishek.Timers.Timer(JitterBufferTick, TimeSpan.FromMilliseconds(JITTER_BUFFER));
                _timer.Start();

            }
        }

        public void StopTimer()
        {
            lock (_lock)
            {
                if (_timer != null)
                {
                    _jitterBuffer.Clear();
                    _timer.Stop();
                    _timer = null;
                }
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

            _inputManager.StopPtt();

            StopTimer();
        }

        private SRClient IsClientMetaDataValid(string clientGuid)
        {
            if (_clientsList.ContainsKey(clientGuid))
            {
                var client = _clientsList[_guid];

                if (client != null && client.isCurrent())
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

                        var time = GetTickCount64(); //should add at the receive instead?

                        if (encodedOpusAudio != null && encodedOpusAudio.Length > 36)
                        {
                            //  process
                            // check if we should play audio

                            var myClient = IsClientMetaDataValid(_guid);

                            if (myClient != null && RadioDCSSyncServer.DcsPlayerRadioInfo.IsCurrent())
                            {
                                //Decode bytes
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                                // check the radio
                                RadioReceivingState receivingState = null;
                                var receivingRadio = RadioDCSSyncServer.DcsPlayerRadioInfo.CanHearTransmission(udpVoicePacket.Frequency,
                                    udpVoicePacket.Modulation,
                                    udpVoicePacket.UnitId, out receivingState);

                                //Check that we're not transmitting on this radio

                                if (receivingRadio != null && receivingState !=null 
                                        && 
                                        ( receivingRadio.modulation == 2 // INTERCOM Modulation is 2 so if its two dont bother checking LOS and Range
                                            ||
                                            (
                                                HasLineOfSight(udpVoicePacket) 
                                                && 
                                                InRange(udpVoicePacket)
                                                &&
                                                !IsCurrentlyTransmittingOn(receivingState.ReceivedOn)
                                            )
                                        )
                                   )
                                {
                                  //  RadioReceivingState[receivingState.ReceivedOn] = receivingState;

                                    //DECODE audio
                                    int len1;
                                    var decoded = _decoder.Decode(udpVoicePacket.AudioPart1Bytes,
                                        udpVoicePacket.AudioPart1Bytes.Length, out len1);

                                    int len2;
                                    var decoded2 = _decoder.Decode(udpVoicePacket.AudioPart2Bytes,
                                        udpVoicePacket.AudioPart2Bytes.Length, out len2);

                                    if (len1 > 0 && len2 > 0)
                                    {
                                        // for some reason if this is removed then it lags?!
                                        //guess it makes a giant buffer and only uses a little?
                                        var tmp = new byte[len1 +len2];
                                        Buffer.BlockCopy(decoded, 0, tmp, 0, len1);
                                        Buffer.BlockCopy(decoded2, 0, tmp, len1, len2);

                                        //ALL GOOD!
                                        //create marker for bytes
                                        var audio = new ClientAudio
                                        {
                                            ClientGuid = udpVoicePacket.Guid,
                                            PcmAudio = tmp,
                                            ReceiveTime = GetTickCount64(),
                                            Frequency = udpVoicePacket.Frequency,
                                            Modulation = udpVoicePacket.Modulation,
                                            Volume = receivingRadio.volume,
                                            ReceivedRadio = receivingState.ReceivedOn,
                                            UnitId = udpVoicePacket.UnitId,
                                            Encryption = udpVoicePacket.Encryption,
                                            Decryptable = udpVoicePacket.Encryption == receivingRadio.encKey && receivingRadio.enc, // mark if we can decrypt it
                                            RadioReceivingState = receivingState
                                        };

                                        //add to JitterBuffer!
                                        lock (_lock)
                                        {
                                            _jitterBuffer.Add(audio);
                                        }
                                    }
                                }
                            }
                        }
                    

                    }
                    catch (Exception ex)
                    {
                        Logger.Info("Failed Decoding");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Stopped DeJitter Buffer");
            }
        }

        private bool IsCurrentlyTransmittingOn(int radioId)
        {
            //Return based on server settings as well
            if (ClientSync.ServerSettings[(int) ServerSettingType.IRL_RADIO_TX] == null
                || ClientSync.ServerSettings[(int) ServerSettingType.IRL_RADIO_TX] == "OFF")
            {
                
                return false;
            }

            return (
                (_ptt || RadioDCSSyncServer.DcsPlayerRadioInfo.ptt)
                && RadioDCSSyncServer.DcsPlayerRadioInfo.selected == radioId);
        }

        private bool HasLineOfSight(UDPVoicePacket udpVoicePacket)
        {
            if (ClientSync.ServerSettings[(int) ServerSettingType.LOS_ENABLED] == null || ClientSync.ServerSettings[(int)ServerSettingType.LOS_ENABLED] == "OFF")
            {
                return true;
            }

            SRClient transmittingClient;
            if (_clientsList.TryGetValue(udpVoicePacket.Guid, out transmittingClient))
            {
                return transmittingClient.HasLineOfSight;
            }
            return false;

        }

        private bool InRange(UDPVoicePacket udpVoicePacket)
        {

            if (ClientSync.ServerSettings[(int)ServerSettingType.DISTANCE_ENABLED] == null 
                || ClientSync.ServerSettings[(int)ServerSettingType.DISTANCE_ENABLED] == "OFF" 
                 )
            {
                return true;
            }

            SRClient transmittingClient;
            if (_clientsList.TryGetValue(udpVoicePacket.Guid, out transmittingClient))
            {
                var myPosition = RadioDCSSyncServer.DcsPlayerRadioInfo.pos;

                var clientPos = transmittingClient.Position;

                if ((myPosition.x == 0 && myPosition.z == 0) || (clientPos.x == 0 && clientPos.z == 0))
                {
                    //no real position
                    return true;
                }
               
                return RadioCalculator.CanHearTransmission(RadioCalculator.CalculateDistance(myPosition,clientPos),udpVoicePacket.Frequency);
            }
            return false;

        }


        private byte[] part1;
        private byte[] part2;
        private bool hasSentVoicePacket; //used to force sending of first voice packet to establish comms

        public void Send(byte[] bytes, int len)
        {
            if (part1 == null)
            {
                part1 = new byte[len];
                Buffer.BlockCopy(bytes, 0, part1, 0, len);
            }
            else if (part2 == null)
            {
                part2 = new byte[len];
                Buffer.BlockCopy(bytes, 0, part2, 0, len);
            }
            else
            {
                part2 = part1;

                part1 = new byte[len];
                Buffer.BlockCopy(bytes, 0, part1, 0, len);
            }
            
            //if either PTT is true
            if ((_ptt || RadioDCSSyncServer.DcsPlayerRadioInfo.ptt)
                && RadioDCSSyncServer.DcsPlayerRadioInfo.IsCurrent() && part1 != null && part2 != null)
                //can only send if DCS is connected
            {
             
                try
                {
                    var currentSelected = RadioDCSSyncServer.DcsPlayerRadioInfo.selected;
                    //removes race condition by assigning here with the current selected changing
                    if (currentSelected >= 0 
                        && currentSelected < RadioDCSSyncServer.DcsPlayerRadioInfo.radios.Length)
                    {
                        var radio = RadioDCSSyncServer.DcsPlayerRadioInfo.radios[currentSelected];

                        if (radio != null && (radio.frequency > 100 && radio.modulation != 3)
                            || radio.modulation == 2)
                        {
                            //generate packet
                            var udpVoicePacket = new UDPVoicePacket()
                            {
                                GuidBytes = _guidAsciiBytes,
                                AudioPart1Bytes = part1,
                                AudioPart1Length = (ushort) part1.Length,
                                AudioPart2Bytes = part2,
                                AudioPart2Length = (ushort) part2.Length,
                                Frequency = radio.frequency,
                                UnitId = RadioDCSSyncServer.DcsPlayerRadioInfo.unitId,
                                Encryption = radio.enc?radio.encKey:(byte)0,
                                Modulation = radio.modulation
                            }.EncodePacket();

                            //clear audio
                            part1 = null;
                            part2 = null;

                            //no need to auto send packet anymore
                            hasSentVoicePacket = true;

                            var ip = new IPEndPoint(_address, 5010);
                            _listener.Send(udpVoicePacket, udpVoicePacket.Length, ip);

                            //sent more than 180ms ago
                            if (RadioSendingState.LastSentAt < Environment.TickCount - 180)
                            {
                                _audioManager.PlaySoundEffectStartTransmit(currentSelected, radio.enc && radio.encKey >0, radio.volume);
                            }

                            //set radio overlay state
                            RadioSendingState = new RadioSendingState()
                            {
                                IsSending = true,
                                LastSentAt = Environment.TickCount,
                                SendingOn = currentSelected
                            };
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e,"Exception Sending Audio Message " + e.Message);
                }
            }
            else if (part1 != null && part2 != null)
            {
                if (RadioSendingState.IsSending)
                {
                    RadioSendingState.IsSending = false;

                    if (RadioSendingState.SendingOn >= 0)
                    {
                        var radio = RadioDCSSyncServer.DcsPlayerRadioInfo.radios[RadioSendingState.SendingOn];
                    
                        _audioManager.PlaySoundEffectEndTransmit(RadioSendingState.SendingOn,
                            radio.enc && radio.encKey > 0, radio.volume);
                    }
                }

                if (!hasSentVoicePacket)
                {
                    try
                    {
                        var udpVoicePacket = new UDPVoicePacket()
                        {
                            GuidBytes = _guidAsciiBytes,
                            AudioPart1Bytes = part1,
                            AudioPart1Length = (ushort)part1.Length,
                            AudioPart2Bytes = part2,
                            AudioPart2Length = (ushort)part2.Length,
                            Frequency = 100,
                            UnitId = 1,
                            Encryption = 0,
                            Modulation = 4,
                        }.EncodePacket();

                        hasSentVoicePacket = true;

                        var ip = new IPEndPoint(_address, 5010);
                        _listener.Send(udpVoicePacket, udpVoicePacket.Length, ip);

                        Logger.Info("Sent First Voice Packet");
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending First Audio Message " + e.Message);
                    }
                }
            }
           
        }

        private void StartPing()
        {
            Task.Run(() =>
            {
                byte[] message = { 1, 2, 3, 4, 5,6,7,8,9,10,11,12,13,14,15 };

                while (!_stop)
                {
                    Logger.Info("Pinging Server");
                    try
                    {
                        var ip = new IPEndPoint(_address, 5010);
                        _listener.Send(message, message.Length, ip);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);

                    }

                    Thread.Sleep(25 * 1000);

                }
            });
        }

        
    }
}