using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server
{
    internal class WebSocketVoiceServer
    {
        private HttpListener _listener;
        private CancellationToken _token;
        private readonly ConcurrentDictionary<string, WebSocket> _wsclients = new();
        private readonly int _port;
        private readonly bool _enabled;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<IEnumerable<string>> _getAllowedIds;

        public WebSocketVoiceServer(Func<IEnumerable<string>> getAllowedIds)
        {
            _getAllowedIds = getAllowedIds;
            _port = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.WEBSOCKET_SERVER_PORT).IntValue;
            _enabled = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.WEBSOCKET_SERVER_ENABLED).BoolValue;
        }

        public void Start()
        {
            if (_enabled)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://*:" + _port + "/");
                _listener.Start();
                Logger.Info("WebSocket Voice Server Started on Port " + _port);
                _token = new CancellationTokenSource().Token;
                _ = StartAsync(_token);
            }
            else
            {
                Logger.Info("WebSocket Voice Server DISABLED on PORT " + _port);
            }
        }
        public void Stop()
        {
            try
            {
                if (_listener != null && _listener.IsListening)
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

                    if (_getAllowedIds().Contains(clientId))
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

        public async Task BroadcastVoicePacket(byte[] data, CancellationToken token)
        {
            var allowedIds = new HashSet<string>(_getAllowedIds());
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
