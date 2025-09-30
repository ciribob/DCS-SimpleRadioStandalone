using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server.TransmissionLogging;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

internal class UDPVoiceRouter : IHandle<ServerFrequenciesChanged>, IHandle<ServerStateMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly List<int>
        _emptyBlockedRadios =
            new(); // Used in radio reachability check below, server does not track blocked radios, so forward all

    private readonly ConcurrentDictionary<string, SRClientBase> _clientsList;
    private readonly IEventAggregator _eventAggregator;

    private readonly BlockingCollection<OutgoingUDPPackets>
        _outGoing = new();

    private readonly CancellationTokenSource _outgoingCancellationToken = new();

    private readonly CancellationTokenSource _pendingProcessingCancellationToken = new();

    private readonly BlockingCollection<PendingPacket> _pendingProcessingPackets =
        new();

    private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;
    private List<double> _globalFrequencies = new();

    private UdpClient _listener;
    //private List<double> _recordingFrequencies = new();
    // private AudioRecordingManager _recordingManager;

    private volatile bool _stop;
    private List<double> _testFrequencies = new();

    private TransmissionLoggingQueue _transmissionLoggingQueue;

    public UDPVoiceRouter(ConcurrentDictionary<string, SRClientBase> clientsList, IEventAggregator eventAggregator)
    {
        _clientsList = clientsList;
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnBackgroundThread(this);

        var freqString = string.Join(',', _serverSettings.SynchronizedSettings.TestFrequencies);
        UpdateTestFrequencies(freqString);

        var globalFreqString = string.Join(',', _serverSettings.SynchronizedSettings.GlobalLobbyFrequencies);
        UpdateGlobalLobbyFrequencies(globalFreqString);
    }

    public Task HandleAsync(ServerFrequenciesChanged message, CancellationToken cancellationToken)
    {
        if (message.TestFrequencies != null)
            UpdateTestFrequencies(message.TestFrequencies);
        else
            UpdateGlobalLobbyFrequencies(message.GlobalLobbyFrequencies);

        return Task.CompletedTask;
    }

    public Task HandleAsync(ServerStateMessage message, CancellationToken cancellationToken)
    {
        //TODO stop the transmission logging queue
        return Task.CompletedTask;
    }


    private void UpdateTestFrequencies(string freqString)
    {
        var freqStringList = freqString.Split(',');

        var newList = new List<double>();
        foreach (var freq in freqStringList)
            if (double.TryParse(freq.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var freqDouble))
            {
                freqDouble *= 1e+6; //convert to Hz from MHz
                newList.Add(freqDouble);
                Logger.Info("Adding Test Frequency: " + freqDouble);
            }

        _testFrequencies = newList;
    }

    private void UpdateGlobalLobbyFrequencies(string freqString)
    {
        var freqStringList = freqString.Split(',');

        var newList = new List<double>();
        foreach (var freq in freqStringList)
            if (double.TryParse(freq.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var freqDouble))
            {
                freqDouble *= 1e+6; //convert to Hz from MHz
                newList.Add(freqDouble);
                Logger.Info("Adding Global Frequency: " + freqDouble);
            }

        _globalFrequencies = newList;
    }

    public void Listen()
    {
        _transmissionLoggingQueue = new TransmissionLoggingQueue();
        _transmissionLoggingQueue.Start();

        //start threads
        //packets that need processing
        new Thread(ProcessPackets).Start();
        //outgoing packets
        new Thread(SendPendingPackets).Start();

        var port = _serverSettings.ServerSettings.ServerPort;
        _listener = new UdpClient();

        //TODO check this
        if (OperatingSystem.IsWindows())
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch
            {
                // ignored
            }


        _listener.ExclusiveAddressUse = true;
        _listener.DontFragment = true;
        _listener.Client.DontFragment = true;
        _listener.Client.Bind(new IPEndPoint(_serverSettings.ServerSettings.ServerBindIp, port));
        while (!_stop)
            try
            {
                var groupEP = new IPEndPoint(_serverSettings.ServerSettings.ServerBindIp, port);
                var rawBytes = _listener.Receive(ref groupEP);

                if (rawBytes?.Length == 22)
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
                    catch (Exception)
                    {
                        //dont log because it slows down thread too much...
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
                Logger.Error(e, "Error receving audio UDP for client " + e.Message);
            }

        try
        {
            _listener.Close();
        }
        catch (Exception)
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
        catch (Exception)
        {
        }

        _transmissionLoggingQueue?.Stop();
        _transmissionLoggingQueue = null;

        _outgoingCancellationToken.Cancel();
        _pendingProcessingCancellationToken.Cancel();
    }

    private void ProcessPackets()
    {
        // _recordingManager = new AudioRecordingManager();
        //
        // _recordingManager.Start(_recordingFrequencies);

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

                    if (_clientsList.TryGetValue(guid, out var client))
                    {
                        client.VoipPort = udpPacket.ReceivedFrom;

                        var spectatorAudioDisabled = _serverSettings.SynchronizedSettings.IsSpectatorAudioDisabled;

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
                                    //magical ping ignore message 4 - its an empty voip packet to initialise VoIP if
                                    //someone doesnt transmit
                                {
                                    var outgoingVoice = GenerateOutgoingPacket(udpVoicePacket, udpPacket, client);

                                    if (outgoingVoice != null)
                                    {
                                        //Add to the processing queue
                                        _outGoing.Add(outgoingVoice);

                                        //mark as transmitting for the UI
                                        var mainFrequency = udpVoicePacket.Frequencies.FirstOrDefault();
                                        // Only trigger transmitting frequency update for "proper" packets (excluding invalid frequencies and magic ping packets with modulation 4)
                                        if (mainFrequency > 0)
                                        {
                                            var mainModulation = (Modulation)udpVoicePacket.Modulations[0];
                                            if (mainModulation == Modulation.INTERCOM)
                                                client.TransmittingFrequency = "INTERCOM";
                                            else
                                                client.TransmittingFrequency =
                                                    $"{(mainFrequency / 1000000).ToString("0.000", CultureInfo.InvariantCulture)} {mainModulation}";
                                            client.LastTransmissionReceived = DateTime.Now;

                                            // Only log the initial transmission
                                            // only log received transmissions!
                                            if (udpVoicePacket.RetransmissionCount == 0)
                                                _transmissionLoggingQueue?.LogTransmission(client);
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
                _outGoing.TryTake(out var udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                if (udpPacket != null)
                {
                    var bytes = udpPacket.ReceivedPacket;
                    var bytesLength = bytes.Length;
                    foreach (var outgoingEndPoint in udpPacket.OutgoingEndPoints)
                        try
                        {
                            _listener.Send(bytes, bytesLength, outgoingEndPoint);
                        }
                        catch (Exception)
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
        SRClientBase fromClient)
    {
        var nodeHopCount = _serverSettings.SynchronizedSettings.RetransmissionNodeLimit;

        if (udpVoice.RetransmissionCount > nodeHopCount)
            //not allowed to retransmit any further
            return null;

        var outgoingList = new HashSet<IPEndPoint>();

        var coalitionSecurity =
            _serverSettings.SynchronizedSettings.IsCoalitionAudioSecurityEnabled;

        var guid = fromClient.ClientGuid;

        var strictEncryption = _serverSettings.SynchronizedSettings.IsStrictRadioEncryptionEnabled;

        foreach (var client in _clientsList)
            if (!client.Key.Equals(guid))
            {
                var ip = client.Value.VoipPort;
                var global = false;
                if (ip != null)
                {
                    for (var i = 0; i < udpVoice.Frequencies.Length; i++)
                        foreach (var testFrequency in _globalFrequencies)
                            if (RadioBase.FreqCloseEnough(testFrequency, udpVoice.Frequencies[i]))
                            {
                                //ignore everything as its global frequency
                                global = true;
                                break;
                            }

                    if (global || client.Value.Gateway)
                    {
                        outgoingList.Add(ip);
                    }
                    // check that either coalition radio security is disabled OR the coalitions match
                    else if (!coalitionSecurity || client.Value.Coalition == fromClient.Coalition)
                    {
                        var radioInfo = client.Value.RadioInfo;

                        if (radioInfo != null)
                            for (var i = 0; i < udpVoice.Frequencies.Length; i++)
                            {
                                RadioReceivingState radioReceivingState = null;
                                var receivingRadio = radioInfo.CanHearTransmission(udpVoice.Frequencies[i],
                                    (Modulation)udpVoice.Modulations[i],
                                    udpVoice.Encryptions[i],
                                    strictEncryption,
                                    udpVoice.UnitId,
                                    _emptyBlockedRadios,
                                    out radioReceivingState,
                                    out _);

                                //only send if we can hear!
                                if (receivingRadio != null) outgoingList.Add(ip);
                            }
                    }
                }
            }
            else
            {
                var ip = client.Value.VoipPort;

                if (ip == null) continue;

                foreach (var frequency in udpVoice.Frequencies)
                foreach (var testFrequency in _testFrequencies)
                    if (RadioBase.FreqCloseEnough(testFrequency, frequency))
                    {
                        //send back to sending client as its a test frequency
                        outgoingList.Add(ip);
                        break;
                    }
            }

        if (outgoingList.Count > 0)
            return new OutgoingUDPPackets
            {
                OutgoingEndPoints = outgoingList.ToList(),
                ReceivedPacket = pendingPacket.RawBytes
            };

        return null;
    }
}