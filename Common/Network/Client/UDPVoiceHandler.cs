using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly TimeSpan UDP_VOIP_TIMEOUT = TimeSpan.FromSeconds(42); // seconds for timeout before redoing VoIP
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentBag<byte[]> _outgoing = new ConcurrentBag<byte[]>();
    private readonly byte[] _guidAsciiBytes;
    private readonly CancellationTokenSource _stopRequest = new();
    private readonly IPEndPoint _serverEndpoint;
    private volatile bool _ready;
    private volatile bool _started;
    private SemaphoreSlim _outgoingSemaphore = new SemaphoreSlim(0);

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

    private async void StartUDP()
    {
        var listener = SetupListener();

        // Send a first ping to check connectivity.
        Logger.Info($"Pinging Server - Starting {Thread.CurrentThread.ManagedThreadId}");
        var pingInterval = TimeSpan.FromSeconds(15);

        // Initial states to avoid null checks and also avoid throwing before we enter the loop.
        var receiveTask = Task.FromException<UdpReceiveResult>(new Exception());
        Task pingTask = Task.CompletedTask;
        var timeoutTask = Task.Delay(UDP_VOIP_TIMEOUT, _stopRequest.Token);
        var outgoingAvailableTask = _outgoingSemaphore.WaitAsync(_stopRequest.Token);
        while (!_stopRequest.IsCancellationRequested)
        {
            try
            {
                if (pingTask.IsCompletedSuccessfully)
                {
                    // Send ping every 15s.
                    pingTask = listener.SendAsync(_guidAsciiBytes, _stopRequest.Token).AsTask().ContinueWith(async ping =>
                    {
                        if (ping.IsCompletedSuccessfully)
                        {
                            await Task.Delay(pingInterval, _stopRequest.Token);
                        }
                        else if (ping.IsFaulted)
                        {
                            Logger.Error(ping.Exception, "Exception Sending Audio Ping! ");
                        }
                    }, _stopRequest.Token).Unwrap();
                }

                if (receiveTask.IsCompleted)
                {
                    if (receiveTask.IsCompletedSuccessfully)
                    {
                        var bytes = receiveTask.Result.Buffer;
                        if (bytes?.Length == 22)
                        {
                            Ready = true;
                            Logger.Info($"Received Ping Back from Server {Thread.CurrentThread.ManagedThreadId}");
                        }
                        else if (Ready && bytes?.Length > 22)
                        {
                            EncodedAudio.Add(bytes);
                        }

                        // Consider this a valid heartbeat. Reset the clock!
                        timeoutTask = Task.Delay(UDP_VOIP_TIMEOUT, _stopRequest.Token);
                    }

                    receiveTask = listener.ReceiveAsync(_stopRequest.Token).AsTask();
                }


                // Process send queue if in ready state.
                if (Ready && outgoingAvailableTask.IsCompletedSuccessfully)
                {
                    // Drain the queue.
                    var sent = new List<Task>();
                    while (_outgoing.TryTake(out var outgoing))
                    {
                        sent.Add(listener.SendAsync(outgoing, _stopRequest.Token).AsTask());
                    }

                    await Task.WhenAll(sent);

                    outgoingAvailableTask = _outgoingSemaphore.WaitAsync(_stopRequest.Token);
                }

                // Reset the socket on a timeout.
                if (timeoutTask.IsCompletedSuccessfully)
                {
                    Logger.Error($"VoIP Timeout - Recreating VoIP Connection {Thread.CurrentThread.ManagedThreadId}");
                    

                    CloseListener(listener);
                    listener = SetupListener();
                    pingTask = Task.CompletedTask;
                    timeoutTask = Task.Delay(UDP_VOIP_TIMEOUT, _stopRequest.Token);
                    receiveTask = listener.ReceiveAsync(_stopRequest.Token).AsTask();
                }

                await Task.WhenAny(new[] { timeoutTask, pingTask, receiveTask, outgoingAvailableTask });

            }
            catch
            {
                // Reset everything but the timeout.
                receiveTask = Task.FromException<UdpReceiveResult>(new Exception());
                pingTask = Task.CompletedTask;
                outgoingAvailableTask = _outgoingSemaphore.WaitAsync(_stopRequest.Token);
            }
        }

        receiveTask = null;
        outgoingAvailableTask = null;
        pingTask = null;
        timeoutTask = null;

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
                _outgoingSemaphore.Release(1);

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending Audio Message");
            }


        return false;
    }
}