

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.ExternalAudioClient.Audio;
using NLog;
using LogManager = NLog.LogManager;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.ExternalAudioClient.Client;

public class ExternalAudioClient : IHandle<TCPClientStatusMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private double[] freq;
    private Modulation[] modulation;
    private byte[] modulationBytes;

    private readonly string Guid = ShortGuid.NewGuid();

    private CancellationTokenSource finished;
    private UDPVoiceHandler udpVoiceHandler;
    private Program.Options opts;
    private IPEndPoint endPoint;
    private readonly byte[] encryptionBytes;
    private uint unitId = 100000;

    public ExternalAudioClient(double[] freq, Modulation[] modulation, Program.Options opts)
    {
        this.unitId = opts.UnitId;
        this.freq = freq;
        this.modulation = modulation;
        this.opts = opts;
        this.modulationBytes = new byte[modulation.Length];
        for (int i = 0; i < modulationBytes.Length; i++)
        {
            modulationBytes[i] = (byte)modulation[i];
        }
        
        encryptionBytes = new byte[modulation.Length];
        for (var i = 0; i < encryptionBytes.Length; i++) encryptionBytes[i] = 0;
        
        endPoint = new IPEndPoint(IPAddress.Loopback, opts.Port);
        
        EventBus.Instance.SubscribeOnUIThread(this);
    }
    
    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        if (message.Connected)
            ReadyToSend();
        else
            Disconnected();

        return Task.CompletedTask;
    }

    public void Start()
    {
        var radioInfoBase = new PlayerRadioInfoBase();
        radioInfoBase.radios[1].modulation = modulation[0];
        radioInfoBase.radios[1].freq = freq[0]; // get into Hz
        radioInfoBase.unitId = unitId;

        Logger.Info($"Starting with params:");
        for (int i = 0; i < freq.Length; i++)
        {
            Logger.Info($"Frequency: {freq[i]} Hz - {modulation[i]} ");
        }

        LatLngPosition position = new LatLngPosition()
        {
            alt = opts.Altitude,
            lat = opts.Latitude,
            lng = opts.Longitude
        };

        radioInfoBase.ambient = new Ambient()
            { abType = opts.AmbientCockpit.ToLowerInvariant().Trim(), vol = opts.AmbientCockpitVolume };

        var srClient = new SRClientBase
        {
            LatLngPosition = position,
            AllowRecord = true,
            ClientGuid = Guid,
            Coalition = opts.Coalition,
            Name = opts.Name,
            RadioInfo = radioInfoBase
        };
        var srsClientSyncHandler = new TCPClientHandler(Guid, srClient);

        using (finished = new CancellationTokenSource())
        {
            srsClientSyncHandler.TryConnect(endPoint);

            //wait for it to end
            finished.Token.WaitHandle.WaitOne();
        }
       
        Logger.Info("Finished - Closing");

        udpVoiceHandler?.RequestStop();
        srsClientSyncHandler?.Disconnect();
    }

    private void ReadyToSend()
    {
        if (udpVoiceHandler == null)
        {
            Logger.Info($"Connecting UDP VoIP {endPoint}");
            udpVoiceHandler = new UDPVoiceHandler(Guid, endPoint);
            udpVoiceHandler.Connect();
            Task.Run(SendAudio, finished.Token);
        }
    }

    private void Disconnected()
    {
        finished.Cancel();
    }

    private async Task SendAudio()
    {
        Logger.Info("Sending Audio... Please Wait");
        var audioGenerator = new AudioGenerator(opts);
        var opusBytes = audioGenerator.GetOpusBytes();
        var count = 0;
       
        //Wait until voip is ready and we're not cancelled
        while(!udpVoiceHandler.Ready && !finished.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), finished.Token);
        }

        using var tokenSource = new CancellationTokenSource();

        uint _packetNumber = 1;
        //get all the audio as Opus frames of 40 ms
        //send on 40 ms timer 

        //when empty - disconnect
        //user timer for accurate sending
        var _timer = new Timer(() =>
        {
            if (!finished.IsCancellationRequested)
            {
                if (count < opusBytes.Count)
                {
                    var udpVoicePacket = new UDPVoicePacket
                    {
                        AudioPart1Bytes = opusBytes[count],
                        AudioPart1Length = (ushort)opusBytes[count].Length,
                        Frequencies = freq,
                        UnitId = unitId,
                        Encryptions = encryptionBytes,
                        Modulations = modulationBytes,
                        RetransmissionCount = 0,
                        PacketNumber = _packetNumber++
                    };

                    udpVoiceHandler.Send(udpVoicePacket);
                    count++;

                    if (count % 50 == 0)
                        Logger.Info(
                            $"Playing audio - sent {count * 40}ms - {count / (float)opusBytes.Count * 100.0:F0}% ");
                }
                else
                {
                    tokenSource.Cancel();
                }
            }
            else
            {
                Logger.Error("Client Disconnected");
                tokenSource.Cancel();
            }
        }, TimeSpan.FromMilliseconds(40));
        _timer.Start();

        //wait for cancel
        tokenSource.Token.WaitHandle.WaitOne();
        _timer.Stop();

        Logger.Info("Finished Sending Audio");
        finished.Cancel();
    }
}