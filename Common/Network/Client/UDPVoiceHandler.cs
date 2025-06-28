using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;
using Timer = System.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class UDPVoiceHandler
{
    private const int UDP_VOIP_TIMEOUT = 42; // seconds for timeout before redoing VoIP
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly EventBus _eventBus = EventBus.Instance;
    private readonly byte[] _guidAsciiBytes;
    private readonly CancellationTokenSource _pingStop = new();
    private readonly IPEndPoint _serverEndpoint;
    private readonly Timer _updateTimer;

    private UdpClient _listener;
    private bool _ready;

    private bool _started;
    private volatile bool _stop;
    private long _udpLastReceived;

    public UDPVoiceHandler(string guid, IPEndPoint endPoint)
    {
        _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

        _serverEndpoint = endPoint;

        //TODO not sure if we need this anymore
        _updateTimer = new Timer { Interval = 5000 };
        _updateTimer.Elapsed += UpdateVOIPStatus;
        _updateTimer.Start();
    }

    public BlockingCollection<byte[]> EncodedAudio { get; } = new();


    public bool Ready
    {
        get => _ready;
        private set
        {
            _ready = value;
            EventBus.Instance.PublishOnUIThreadAsync(new VOIPStatusMessage(_ready));
        }
    }

    private void UpdateVOIPStatus(object sender, EventArgs e)
    {
        var diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

        //ping every 10 so after 40 seconds VoIP UDP issue
        if (diff.TotalSeconds > UDP_VOIP_TIMEOUT)
            _eventBus.PublishOnUIThreadAsync(new VOIPStatusMessage(false));
        else
            _eventBus.PublishOnUIThreadAsync(new VOIPStatusMessage(true));
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

    private void StartUDP()
    {
        _udpLastReceived = 0;
        Ready = false;
        _listener = new UdpClient();

        if (OperatingSystem.IsWindows())
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch
            {
                // ignored
            }

        StartPing();

        while (!_stop)
            if (Ready)
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, _serverEndpoint.Port);

                    var bytes = _listener.Receive(ref groupEp);

                    if (bytes?.Length == 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        Logger.Info($"Received Ping Back from Server {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else if (bytes?.Length > 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        EncodedAudio.Add(bytes);
                    }
                }
                catch (Exception)
                {
                    //  logger.Error(e, "error listening for UDP Voip");
                }

        Ready = false;

        //stop UI Refreshing
        _updateTimer.Stop();

        _eventBus.PublishOnCurrentThreadAsync(new VOIPStatusMessage(false));

        _started = false;

        Logger.Info($"UDP Voice Handler Thread Stop {Thread.CurrentThread.ManagedThreadId}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void RequestStop()
    {
        _stop = true;
        try
        {
            _listener?.Close();
            _listener = null;
        }
        catch (Exception)
        {
        }

        try
        {
            _pingStop.Cancel();
        }
        catch (Exception)
        {
        }

        _started = false;

        _eventBus.PublishOnCurrentThreadAsync(new VOIPStatusMessage(false));
    }

    public bool Send(UDPVoicePacket udpVoicePacket)
    {
        if (Ready
            && _listener != null
            && udpVoicePacket != null)
            try
            {
                if (udpVoicePacket.GuidBytes == null) udpVoicePacket.GuidBytes = _guidAsciiBytes;

                if (udpVoicePacket.OriginalClientGuidBytes == null)
                    udpVoicePacket.OriginalClientGuidBytes = _guidAsciiBytes;

                var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                _listener?.Send(encodedUdpVoicePacket, encodedUdpVoicePacket.Length, _serverEndpoint);

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending Audio Message " + e.Message);
            }


        return false;
    }

    private void StartPing()
    {
        Logger.Info($"Pinging Server - Starting {Thread.CurrentThread.ManagedThreadId}");

        var message = _guidAsciiBytes;

        // Force immediate ping once to avoid race condition before starting to listen
        _listener?.Send(message, message.Length, _serverEndpoint);

        new Thread(() =>
        {
            Logger.Info($"Pinging Server - Thread Starting {Thread.CurrentThread.ManagedThreadId}");
            //wait for initial sync - then ping
            if (_pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2))) return;

            Ready = true;

            while (!_stop)
            {
                //Logger.Info("Pinging Server");
                try
                {
                    _listener?.Send(message, message.Length, _serverEndpoint);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                }

                //wait for cancel or quit
                var cancelled = _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));

                if (cancelled) break;

                var diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

                //reconnect to UDP - port is no good!
                if (diff.TotalSeconds > UDP_VOIP_TIMEOUT)
                {
                    Logger.Error($"VoIP Timeout - Recreating VoIP Connection {Thread.CurrentThread.ManagedThreadId}");
                    Ready = false;
                    try
                    {
                        _listener?.Close();
                    }
                    catch (Exception)
                    {
                    }

                    _listener = null;

                    _udpLastReceived = 0;

                    _listener = new UdpClient();

                    if (OperatingSystem.IsWindows())
                        try
                        {
                            _listener?.AllowNatTraversal(true);
                        }
                        catch
                        {
                            // ignored
                        }

                    try
                    {
                        // Force immediate ping once to avoid race condition before starting to listen
                        _listener?.Send(message, message.Length, _serverEndpoint);
                        Ready = true;
                        Logger.Error("VoIP Timeout - Success Recreating VoIP Connection");
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                    }
                }
            }

            Logger.Info($"VoIP Ping Thread Stop {Thread.CurrentThread.ManagedThreadId}");
        }).Start();
    }
}