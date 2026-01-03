using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server.TransmissionLogging;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    private CancellationTokenSource _stopCancellationToken;

    private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;
    private List<double> _globalFrequencies = new();
    //private List<double> _recordingFrequencies = new();
    // private AudioRecordingManager _recordingManager;
    private List<double> _testFrequencies = new();

    private TransmissionLoggingQueue _transmissionLoggingQueue;

    public UDPVoiceRouter(ConcurrentDictionary<string, SRClientBase> clientsList, IEventAggregator eventAggregator)
    {
        _clientsList = clientsList;
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnBackgroundThread(this);

        var freqString = _serverSettings.GetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES).StringValue;
        UpdateTestFrequencies(freqString);

        var globalFreqString =
            _serverSettings.GetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES).StringValue;
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

    public async void Listen()
    {
        Logger.Info("UDP Voice Router starting...");
        _transmissionLoggingQueue = new TransmissionLoggingQueue();
        _transmissionLoggingQueue.Start();

        var listener = new UdpClient();

        //TODO check this
        if (OperatingSystem.IsWindows())
        {
            try
            {
                listener.AllowNatTraversal(true);
            }
            catch
            {
                // ignored
            }
        }
        
        listener.ExclusiveAddressUse = true;
        listener.DontFragment = true;

        var port = _serverSettings.GetServerPort();
        listener.Client.Bind(new IPEndPoint(_serverSettings.GetServerIP(), port));

        // Incoming queue.
        await ProcessIncomingPacketsAsync(listener);

        try
        {
            listener.Close();
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Error closing UDP Voice router socket");
        }

        Logger.Info("UDP Voice Router stopped.");
    }

    public void RequestStop()
    {
        _stopCancellationToken?.Cancel();
        _transmissionLoggingQueue?.Stop();
        _transmissionLoggingQueue = null;
    }

    private async Task DispatchOutgoingPacketsAsync(UdpClient listener, OutgoingUDPPackets outgoingUdpPacket)
    {
        var recipients = new List<Task>(outgoingUdpPacket.OutgoingEndPoints.Count);
        foreach (var outgoingEndPoint in outgoingUdpPacket.OutgoingEndPoints)
        {
            try
            {
                recipients.Add(listener.SendAsync(outgoingUdpPacket.ReceivedPacket, outgoingEndPoint, _stopCancellationToken.Token).AsTask());
            }
            catch (OperationCanceledException)
            {
                // Expected termination.
            }
            catch (Exception)
            {
                // Deliberately ignored, can be spammy.
            }
        }

        await Task.WhenAll(recipients);
    }

    private async Task ProcessIncomingPacketsAsync(UdpClient listener)
    {
        using (_stopCancellationToken = new CancellationTokenSource())
        {
            while (!_stopCancellationToken.IsCancellationRequested)
            {
                try
                {
                    var inbound = await listener.ReceiveAsync(_stopCancellationToken.Token);
                    var rawBytes = inbound.Buffer;
                    var receivedFromEP = inbound.RemoteEndPoint;
                    if (rawBytes?.Length == 22)
                    {
                        try
                        {
                            //lookup guid here
                            //22 bytes are guid!
                            var guid = Encoding.ASCII.GetString(
                                rawBytes, 0, 22);

                            if (_clientsList.TryGetValue(guid, out var client))
                            {
                                client.VoipPort = receivedFromEP;
                            }
                            //send back ping UDP, don't care much about the result.
                            var pong = Task.Run(async () => await listener.SendAsync(rawBytes, rawBytes.Length, receivedFromEP), _stopCancellationToken.Token);
                           
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Bad send?");
                        }
                    }
                    else if (rawBytes?.Length > 22)
                    {
                        var forwarded = Task.Run(async () => await ProcessPendingPacketAsync(listener, new PendingPacket
                        {
                            RawBytes = rawBytes,
                            ReceivedFrom = receivedFromEP
                        }), _stopCancellationToken.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal termination, let the top while loop catch it.
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error in UDP Voice Router listener");
                }
            }

            Logger.Info("UDP Voice Router Listener stopped.");
        }
    }

    private async Task ProcessPendingPacketAsync(UdpClient listener, PendingPacket udpPacket)
    {
        if (udpPacket == null)
        {
            return;
        }

        try
        {
            //last 22 bytes are guid!
            var guid = Encoding.ASCII.GetString(
                udpPacket.RawBytes, udpPacket.RawBytes.Length - 22, 22);

            if (_clientsList.TryGetValue(guid, out var client))
            {
                if(!client.GatewayClient)
                    client.VoipPort = udpPacket.ReceivedFrom;

                var spectatorAudioDisabled =
                    _serverSettings.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;

                if ((client.Coalition == 0 && spectatorAudioDisabled) || client.Muted)
                {
                    // IGNORE THE AUDIO
                }
                else
                {
                    //decode
                    var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(udpPacket.RawBytes);

                    if (udpVoicePacket != null)
                    //magical ping ignore message 4 - its an empty voip packet to initialise VoIP if
                    //someone doesnt transmit
                    {
                        var outgoingVoice = GenerateOutgoingPacket(udpVoicePacket, udpPacket, client.ClientGuid, client.Coalition);

                        if (outgoingVoice != null)
                        {
                            //Add to the processing queue
                            await DispatchOutgoingPacketsAsync(listener, outgoingVoice);

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
            }
        }
        catch (OperationCanceledException)
        {
            // Expected termination.
        }
        catch (Exception)
        {
            //Hide for now, slows down too much....
        }
    }

    private OutgoingUDPPackets GenerateOutgoingPacket(UDPVoicePacket udpVoice, PendingPacket pendingPacket,
        string sendingClientGuid, int sendingClientCoalition)
    {
        var nodeHopCount =
            _serverSettings.GetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT).IntValue;

        if (udpVoice.RetransmissionCount > nodeHopCount)
            //not allowed to retransmit any further
            return null;

        var outgoingList = new HashSet<IPEndPoint>();

        var coalitionSecurity =
            _serverSettings.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;

        var strictEncryption = _serverSettings.GetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION).BoolValue;

        foreach (var client in _clientsList)
            if (!client.Key.Equals(sendingClientGuid))
            {
                var ip = client.Value.VoipPort;
                var global = false;
                if (client.Value.GatewayClient || ip == null) continue;
                
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
                else if (!coalitionSecurity || client.Value.Coalition == sendingClientCoalition)
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