using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server
{
    public class WebSocketVoiceServer : IHandle<RecordingClientRegistered>, IHandle<RecordingClientDeregistered>
    {
        private HttpListener _listener;
        private CancellationToken _token;
        private readonly ConcurrentDictionary<string, WebSocket> _wsclients = new();
        private readonly int _port;
        private readonly bool _enabled;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<IEnumerable<string>> _getAllowedIds;
        private HashSet<string> _allowedIdsCache = new();
        private readonly object _allowedIdsLock = new();
        private readonly IEventAggregator _eventAggregator;

        public bool IsRunning => _listener != null && _listener.IsListening;

        public WebSocketVoiceServer(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _port = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.WEBSOCKET_SERVER_PORT).IntValue;
            _enabled = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.WEBSOCKET_SERVER_ENABLED).BoolValue;
            eventAggregator.SubscribeOnBackgroundThread(this);
        }

        public Task HandleAsync(RecordingClientRegistered message, CancellationToken cancellationToken)
        {
            UpdateAllowedIds();
            return Task.CompletedTask;
        }

        public Task HandleAsync(RecordingClientDeregistered message, CancellationToken cancellationToken)
        {
            UpdateAllowedIds();
            return Task.CompletedTask;
        }

        public void Start()
        {
            if (!_enabled)
            {
                Logger.Info("WebSocket Voice Server DISABLED on Port " + _port);
                return;
            }

            if (IsRunning)
            {
                Logger.Warn("WebSocket Voice Server is already running on Port " + _port);
                return;
            }

            //when enabled and not running, start the listener
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + _port + "/");
            _listener.Start();
            Logger.Info("WebSocket Voice Server Started on Port " + _port);
            _token = new CancellationTokenSource().Token;

            // Publish the event here
            _eventAggregator.PublishOnBackgroundThreadAsync(new WebSocketServerStarted(this));

            _ = StartAsync(_token);
        }

        public void Stop()
        {
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping WebSocketVoiceServer listener");
            }

            foreach (var ws in _wsclients.Values)
            {
                try
                {
                    if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    {
                        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error closing WebSocket connection");
                }
            }
            _wsclients.Clear();
            Logger.Info("WebSocket Voice Server stopped and all clients disconnected.");
        }

        private async Task StartAsync(CancellationToken token)
        {
            _listener.Start();
            while (!token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var ws = wsContext.WebSocket;
                    var buffer = new byte[128];
                    var result = await ws.ReceiveAsync(buffer, token);
                    var clientId = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    HashSet<string> allowedIds;
                    lock (_allowedIdsLock)
                    {
                        allowedIds = _allowedIdsCache;
                    }

                    if (allowedIds.Contains(clientId))
                    {
                        _wsclients.TryAdd(clientId, ws);
                        _ = Task.Run(() => Listen(ws, clientId, token));
                    }
                    else
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Not registered", token);
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async Task Listen(WebSocket ws, string id, CancellationToken token)
        {
            var buffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(buffer, token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            finally
            {
                _wsclients.TryRemove(id, out _);
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", token);
            }
        }

        /// <summary>
        /// Call this method whenever the allowed IDs change (e.g., after registration/deregistration).
        /// </summary>
        public void UpdateAllowedIds()
        {
            lock (_allowedIdsLock)
            {
                _allowedIdsCache = new HashSet<string>(HttpServer.GetRecordingClientIds());
            }
        }

        public async Task BroadcastVoicePacket(byte[] data, SRClientBase client, CancellationToken token)
        {
            if (!_enabled || !IsRunning)
                return;

            HashSet<string> allowedIds;
            lock (_allowedIdsLock)
            {
                allowedIds = _allowedIdsCache;
            }

            foreach (var recording_client in _wsclients)
            {
                if (allowedIds.Contains(recording_client.Key) && recording_client.Value.State == WebSocketState.Open)
                {
                    await recording_client.Value.SendAsync(data, WebSocketMessageType.Binary, true, token);
                }
            }
        }
    }
}
