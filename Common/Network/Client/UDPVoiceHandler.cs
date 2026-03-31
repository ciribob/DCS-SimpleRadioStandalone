using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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
    private CancellationTokenSource _stopRequest;
    private readonly IPEndPoint _serverEndpoint;
    private bool _started;
    private SemaphoreSlim _outgoingSemaphore = new SemaphoreSlim(0);

    public UDPVoiceHandler(string guid, IPEndPoint endPoint)
    {
        _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

        _serverEndpoint = endPoint;
    }

    public BlockingCollection<byte[]> EncodedAudio { get; } = new();


    public bool Ready
    {
        get => field;
        private set
        {
            if (Interlocked.CompareExchange(ref field, value, !value) != value)
            {
                EventBus.Instance.PublishOnUIThreadAsync(new VOIPStatusMessage(field));
            }
        }
    }

    public void Connect()
    {
        if (!Interlocked.CompareExchange(ref _started, true, false))
        {
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
        using (_stopRequest = new CancellationTokenSource())
        {
            var token = _stopRequest.Token;
            var listener = SetupListener();

            // Send a first ping to check connectivity.
            Logger.Info($"Pinging Server - Starting");
            var pingInterval = TimeSpan.FromSeconds(15);

            // Initial states to avoid null checks and also avoid throwing before we enter the loop.
            var receiveTask = Task.FromException<UdpReceiveResult>(new Exception());
            var pingTask = Task.CompletedTask;
            var timeoutTask = Task.Delay(UDP_VOIP_TIMEOUT, token);
            var outgoingAvailableTask = _outgoingSemaphore.WaitAsync(token);
            while (!token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (pingTask.IsCompletedSuccessfully)
                    {
                        // Send ping every 15s.
                        await listener.SendAsync(_guidAsciiBytes, token);
                        pingTask = Task.Delay(pingInterval, token);
                    }

                    if (receiveTask.IsCompleted)
                    {
                        if (receiveTask.IsCompletedSuccessfully)
                        {
                            var bytes = receiveTask.Result.Buffer;
                            if (bytes?.Length == 22)
                            {
                                if (!Ready)
                                {
                                    Logger.Info($"Received initial Ping Back from Server");
                                }
                                Ready = true;
                                
                            }
                            else if (Ready && bytes?.Length > 22)
                            {
                                EncodedAudio.Add(bytes);
                            }

                            // Consider this a valid heartbeat. Reset the clock!
                            timeoutTask = Task.Delay(UDP_VOIP_TIMEOUT, token);
                        }

                        receiveTask = listener.ReceiveAsync(token).AsTask();
                    }


                    // Process send queue if in ready state.
                    if (Ready && outgoingAvailableTask.IsCompletedSuccessfully)
                    {
                        // Drain the queue.
                        if (_outgoing.TryTake(out var outgoing))
                        {
                            await listener.SendAsync(outgoing, token);
                        }

                        outgoingAvailableTask = _outgoingSemaphore.WaitAsync(token);
                    }

                    // Reset the socket on a timeout.
                    if (timeoutTask.IsCompletedSuccessfully)
                    {
                        Logger.Error("VoIP Timeout - Recreating VoIP Connection");


                        CloseListener(listener);
                        listener = SetupListener();
                        pingTask = Task.CompletedTask;
                        timeoutTask = Task.Delay(UDP_VOIP_TIMEOUT, token);
                        receiveTask = listener.ReceiveAsync(token).AsTask();
                    }

                    await Task.WhenAny([timeoutTask, pingTask, receiveTask, outgoingAvailableTask]);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Voice handler exception");
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

            Interlocked.Exchange(ref _started, false);

            Logger.Info("UDP Voice Handler Thread Stop");
        }
    }

    public void RequestStop()
    {
        try
        {
            _stopRequest?.Cancel();
        }
        catch (Exception)
        {
        }
    }

    public bool Send(UDPVoicePacket udpVoicePacket, bool gatewayClient = false)
    {
        if (udpVoicePacket != null)
            try
            {
                if (!gatewayClient)
                {
                    udpVoicePacket.GuidBytes ??= _guidAsciiBytes;
                    udpVoicePacket.OriginalClientGuidBytes ??= _guidAsciiBytes;
                }
                
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