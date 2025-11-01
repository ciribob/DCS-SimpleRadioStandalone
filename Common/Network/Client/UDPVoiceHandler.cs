using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class UDPVoiceHandler
{
    private const int UDP_VOIP_TIMEOUT = 42; // seconds for timeout before redoing VoIP
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentBag<byte[]> _outgoing = new ConcurrentBag<byte[]>();
    private readonly byte[] _guidAsciiBytes;
    private readonly CancellationTokenSource _stopRequest = new();
    private readonly IPEndPoint _serverEndpoint;
    private volatile bool _ready;
    private volatile bool _started;

    public UDPVoiceHandler(string guid, IPEndPoint endPoint)
    {
        _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

        _serverEndpoint = endPoint;
    }

    public BlockingCollection<byte[]> EncodedAudio { get; } = new();


    public bool Ready
    {
        get => _ready;
        private set
        {
            if (_ready != value)
            {
                _ready = value;
                EventBus.Instance.PublishOnUIThreadAsync(new VOIPStatusMessage(_ready));
            }
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Connect()
    {
        if (!_started)
        {
            _started = true;
            new Thread(StartUDP).Start();
        }
    }

    private UdpClient SetupListener()
    {
        Ready = false;
        var listener = new UdpClient();
        listener.Connect(_serverEndpoint);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                listener.AllowNatTraversal(true);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Unable to set NAT traversal for UDP voice socket");
            }
        }

        return listener;
    }

    private void CloseListener(UdpClient listener)
    {
        Ready = false;
        try
        {
            listener.Close();
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to close listener");
        }
    }

    private void StartUDP()
    {
        var listener = SetupListener();

        // Send a first ping to check connectivity.
        Logger.Info($"Pinging Server - Starting {Thread.CurrentThread.ManagedThreadId}");
        var pingInterval = TimeSpan.FromSeconds(15);
        ValueTask<UdpReceiveResult>? receiveTask = null;
        ValueTask<int>? pingTask = null;
        ValueTask<int>? sendTask = null;
        long lastPingSent = 0;
        while (!_stopRequest.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var timeSinceLastPing = TimeSpan.FromTicks(now.Ticks - lastPingSent);
            Ready = pingTask.HasValue && pingTask.Value.IsCompletedSuccessfully;
            if (!pingTask.HasValue || pingTask.Value.IsCompletedSuccessfully && timeSinceLastPing > pingInterval)
            {
                try
                {
                    pingTask = listener.SendAsync(_guidAsciiBytes, _stopRequest.Token);
                    lastPingSent = now.Ticks;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Sending Audio Ping! ");
                }
            }
            else if (timeSinceLastPing > TimeSpan.FromSeconds(UDP_VOIP_TIMEOUT))
            {
                Logger.Error($"VoIP Timeout - Recreating VoIP Connection {Thread.CurrentThread.ManagedThreadId}");
                pingTask = null;
                receiveTask = null;
                sendTask = null;
                CloseListener(listener);
                listener = SetupListener();
            }
            try
            {
                if (Ready)
                {
                
                    if (!sendTask.HasValue || sendTask.Value.IsCompleted)
                    {
                        if (_outgoing.TryTake(out var outgoing))
                        {
                            sendTask = listener.SendAsync(outgoing, _stopRequest.Token);
                        }
                    }
                    
                    if (receiveTask.HasValue && receiveTask.Value.IsCompletedSuccessfully)
                    {
                        var bytes = receiveTask.Value.Result.Buffer;
                        if (bytes?.Length == 22)
                        {
                            Logger.Info($"Received Ping Back from Server {Thread.CurrentThread.ManagedThreadId}");
                        }
                        else if (bytes?.Length > 22)
                        {
                            EncodedAudio.Add(bytes);
                        }
                        receiveTask = null;
                    }

                    if (!receiveTask.HasValue)
                    {
                        receiveTask = listener.ReceiveAsync(_stopRequest.Token);
                    }
                }

                // #TODO: Don't poll.
                Task.Delay(5, _stopRequest.Token).Wait(_stopRequest.Token);
            }
            catch (Exception)
            {
                //  logger.Error(e, "error listening for UDP Voip");
            }

            
        }

        receiveTask = null;
        sendTask = null;
        pingTask = null;

        CloseListener(listener);
        _outgoing.Clear();

        _started = false;

        Logger.Info($"UDP Voice Handler Thread Stop {Thread.CurrentThread.ManagedThreadId}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void RequestStop()
    {
        try
        {
            _stopRequest.Cancel();
        }
        catch (Exception)
        {
        }
    }

    public bool Send(UDPVoicePacket udpVoicePacket)
    {
        if (udpVoicePacket != null)
            try
            {
                udpVoicePacket.GuidBytes ??= _guidAsciiBytes;
                udpVoicePacket.OriginalClientGuidBytes ??= _guidAsciiBytes;

                _outgoing.Add(udpVoicePacket.EncodePacket());

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending Audio Message");
            }


        return false;
    }
}