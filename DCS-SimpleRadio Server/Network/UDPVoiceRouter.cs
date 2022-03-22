 using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    internal class UDPVoiceRouter: IHandle<ServerFrequenciesChanged>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, SRClient> _clientsList;
        private readonly IEventAggregator _eventAggregator;

        private readonly BlockingCollection<OutgoingUDPPackets> _outGoing = new BlockingCollection<OutgoingUDPPackets>();
        private readonly CancellationTokenSource _outgoingCancellationToken = new CancellationTokenSource();

        private readonly CancellationTokenSource _pendingProcessingCancellationToken = new CancellationTokenSource();

        private readonly BlockingCollection<PendingPacket> _pendingProcessingPackets =
            new BlockingCollection<PendingPacket>();

        private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;
        private UdpClient _listener;

        private volatile bool _stop;

        private static readonly List<int> _emptyBlockedRadios = new List<int>(); // Used in radio reachability check below, server does not track blocked radios, so forward all
        private List<double> _testFrequencies = new List<double>();
        private List<double> _globalFrequencies = new List<double>();

        private readonly TransmissionLoggingQueue transmissionLoggingQueue = new TransmissionLoggingQueue();

        public UDPVoiceRouter(ConcurrentDictionary<string, SRClient> clientsList, IEventAggregator eventAggregator)
        {
            _clientsList = clientsList;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);

            var freqString = _serverSettings.GetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES).StringValue;
            UpdateTestFrequencies(freqString);

            var globalFreqString = _serverSettings.GetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES).StringValue;
            UpdateGlobalLobbyFrequencies(globalFreqString);
        }


        private void UpdateTestFrequencies(string freqString)
        {
            
            var freqStringList = freqString.Split(',');

            var newList = new List<double>();
            foreach (var freq in freqStringList)
            {
                if (double.TryParse(freq.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var freqDouble))
                {
                    freqDouble *= 1e+6; //convert to Hz from MHz
                    newList.Add(freqDouble);
                    Logger.Info("Adding Test Frequency: " + freqDouble);
                }
            }

            _testFrequencies = newList;
        }

        private void UpdateGlobalLobbyFrequencies(string freqString)
        {

            var freqStringList = freqString.Split(',');

            var newList = new List<double>();
            foreach (var freq in freqStringList)
            {
                if (double.TryParse(freq.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var freqDouble))
                {
                    freqDouble *= 1e+6; //convert to Hz from MHz
                    newList.Add(freqDouble);
                    Logger.Info("Adding Global Frequency: " + freqDouble);
                }
            }

            _globalFrequencies = newList;
        }

        public void Listen()
        {
            //start threads
            //packets that need processing
            new Thread(ProcessPackets).Start();
            //outgoing packets
            new Thread(SendPendingPackets).Start();
            transmissionLoggingQueue.Start();

            var port = _serverSettings.GetServerPort();
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }
            
            _listener.ExclusiveAddressUse = true;
            _listener.DontFragment = true;
            _listener.Client.DontFragment = true;
            _listener.Client.Bind(new IPEndPoint(_serverSettings.GetServerIP(), port));
            while (!_stop)
                try
                {
                    var groupEP = new IPEndPoint(_serverSettings.GetServerIP(), port);
                    var rawBytes = _listener.Receive(ref groupEP);

                    if (rawBytes?.Length == 22)
                    {
                        try
                        {
                            //lookup guid here
                            //22 bytes are guid!
                            var guid = Encoding.ASCII.GetString(
                                rawBytes, 0, 22);

                            if (_clientsList.ContainsKey(guid))
                            {
                                var client = _clientsList[guid];
                                client.VoipPort = groupEP;

                                //send back ping UDP
                                _listener.Send(rawBytes, rawBytes.Length, groupEP);
                            }
                        }
                        catch (Exception ex)
                        {
                            //dont log because it slows down thread too much...
                        }
                    }
                    else if (rawBytes?.Length > 22)
                        _pendingProcessingPackets.Add(new PendingPacket
                        {
                            RawBytes = rawBytes,
                            ReceivedFrom = groupEP
                        });

                }
                catch (Exception e)
                {
                      Logger.Error(e,"Error receving audio UDP for client " + e.Message);
                }

            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
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

            _outgoingCancellationToken.Cancel();
            _pendingProcessingCancellationToken.Cancel();
            transmissionLoggingQueue.Stop();
        }

        private void ProcessPackets()
        {
            while (!_stop)
                try
                {
                    PendingPacket udpPacket = null;
                    _pendingProcessingPackets.TryTake(out udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                    if (udpPacket != null)
                    {
                        //last 22 bytes are guid!
                        var guid = Encoding.ASCII.GetString(
                            udpPacket.RawBytes, udpPacket.RawBytes.Length - 22, 22);

                        if (_clientsList.ContainsKey(guid))
                        {
                            var client = _clientsList[guid];
                            client.VoipPort = udpPacket.ReceivedFrom;

                            var spectatorAudioDisabled =
                                _serverSettings.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;

                            if ((client.Coalition == 0 && spectatorAudioDisabled) || client.Muted)
                            {
                                // IGNORE THE AUDIO
                            }
                            else
                            {
                                try
                                {
                                    //decode
                                    var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(udpPacket.RawBytes);

                                    if (udpVoicePacket != null)
                                        //magical ping ignore message 4 - its an empty voip packet to intialise VoIP if
                                        //someone doesnt transmit
                                    {
                                        var outgoingVoice = GenerateOutgoingPacket(udpVoicePacket, udpPacket, client);

                                        if (outgoingVoice != null)
                                        {
                                            //Add to the processing queue
                                            _outGoing.Add(outgoingVoice);
                                            
                                            //mark as transmitting for the UI
                                            double mainFrequency = udpVoicePacket.Frequencies.FirstOrDefault();
                                            // Only trigger transmitting frequency update for "proper" packets (excluding invalid frequencies and magic ping packets with modulation 4)
                                            if (mainFrequency > 0)
                                            {
                                                RadioInformation.Modulation mainModulation = (RadioInformation.Modulation)udpVoicePacket.Modulations[0];
                                                if (mainModulation == RadioInformation.Modulation.INTERCOM)
                                                {
                                                    client.TransmittingFrequency = "INTERCOM";
                                                }
                                                else
                                                {
                                                    client.TransmittingFrequency = $"{(mainFrequency / 1000000).ToString("0.000", CultureInfo.InvariantCulture)} {mainModulation}";
                                                }
                                                client.LastTransmissionReceived = DateTime.Now;

                                                // Only log the initial transmission
                                                // only log received transmissions!
                                                if (udpVoicePacket.RetransmissionCount == 0)
                                                {
                                                    transmissionLoggingQueue.LogTransmission(client);
                                                }
                                            }
                                        }

                                    }
                                }
                                catch (Exception)
                                {
                                    //Hide for now, slows down loop to much....
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("Failed to Process UDP Packet: " + ex.Message);
                }
        }

        private
            void SendPendingPackets()
        {
            //_listener.Send(bytes, bytes.Length, ip);
            while (!_stop)
                try
                {
                    OutgoingUDPPackets udpPacket = null;
                    _outGoing.TryTake(out udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                    if (udpPacket != null)
                    {
                        var bytes = udpPacket.ReceivedPacket;
                        var bytesLength = bytes.Length;
                        foreach (var outgoingEndPoint in udpPacket.OutgoingEndPoints)
                            try
                            {
                                _listener.Send(bytes, bytesLength, outgoingEndPoint);
                            }
                            catch (Exception ex)
                            {
                                //dont log, slows down too much...
                            }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("Error processing Sending Queue UDP Packet: " + ex.Message);
                }
        }

        private OutgoingUDPPackets GenerateOutgoingPacket(UDPVoicePacket udpVoice, PendingPacket pendingPacket,
            SRClient fromClient)
        {
            var nodeHopCount =
                _serverSettings.GetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT).IntValue;

            if (udpVoice.RetransmissionCount > nodeHopCount)
            {
                //not allowed to retransmit any further
                return null;
            }

            var outgoingList = new HashSet<IPEndPoint>();

            var coalitionSecurity =
                _serverSettings.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;

            var guid = fromClient.ClientGuid;

            var strictEncryption = _serverSettings.GetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION).BoolValue;

            foreach (var client in _clientsList)
            {
                if (!client.Key.Equals(guid))
                {
                    var ip = client.Value.VoipPort;
                    bool global = false;
                    if (ip != null)
                    {

                        for (int i = 0; i < udpVoice.Frequencies.Length; i++)
                        {
                            foreach (var testFrequency in _globalFrequencies)
                            {
                                if (DCSPlayerRadioInfo.FreqCloseEnough(testFrequency, udpVoice.Frequencies[i]))
                                {
                                    //ignore everything as its global frequency
                                    global = true;
                                    break;
                                }
                            }
                        }

                        if (global)
                        {
                            outgoingList.Add(ip);
                        }
                        // check that either coalition radio security is disabled OR the coalitions match
                        else if ((!coalitionSecurity || (client.Value.Coalition == fromClient.Coalition)))
                        {

                            var radioInfo = client.Value.RadioInfo;

                            if (radioInfo != null)
                            {
                                for (int i = 0; i < udpVoice.Frequencies.Length; i++)
                                {
                                    RadioReceivingState radioReceivingState = null;
                                    bool decryptable;
                                    var receivingRadio = radioInfo.CanHearTransmission(udpVoice.Frequencies[i],
                                        (RadioInformation.Modulation)udpVoice.Modulations[i],
                                        udpVoice.Encryptions[i],
                                        strictEncryption,
                                        udpVoice.UnitId,
                                        _emptyBlockedRadios,
                                        out radioReceivingState,
                                        out decryptable);

                                    //only send if we can hear!
                                    if (receivingRadio != null)
                                    {
                                        outgoingList.Add(ip);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    var ip = client.Value.VoipPort;

                    if (ip != null)
                    {
                        foreach (var frequency in udpVoice.Frequencies)
                        {
                            foreach (var testFrequency in _testFrequencies)
                            {
                                if (DCSPlayerRadioInfo.FreqCloseEnough(testFrequency, frequency))
                                {
                                    //send back to sending client as its a test frequency
                                    outgoingList.Add(ip);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            if (outgoingList.Count > 0)
            {
                return new OutgoingUDPPackets
                {
                    OutgoingEndPoints = outgoingList.ToList(),
                    ReceivedPacket = pendingPacket.RawBytes
                };
            }
            else
            {
                return null;
            }
        }

        public void Handle(ServerFrequenciesChanged message)
        {
            if (message.TestFrequencies != null)
            {
                UpdateTestFrequencies(message.TestFrequencies);
            }
            else
            {
                UpdateGlobalLobbyFrequencies(message.GlobalLobbyFrequencies);
            }
        }

    }
}