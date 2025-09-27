using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class HttpServer
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ConcurrentDictionary<string, SRClientBase> _connectedClients;
    private readonly bool _enabled;
    private readonly int _port;
    private readonly ServerState _serverState;
    private readonly Caliburn.Micro.IEventAggregator _eventAggregator;
    private readonly string _authentication;

    private HttpListener _listener;
    
    private static readonly string CLIENT_BAN_GUID = "/client/ban/guid";
    private static readonly string CLIENT_BAN_NAME = "/client/ban/name";
    private static readonly string CLIENT_KICK_GUID = "/client/kick/guid";
    private static readonly string CLIENT_KICK_NAME = "/client/kick/name";
    private static readonly string CLIENTS_LIST = "/clients";
    private static readonly string REGISTER_VOICE_STRAM = "/register/voice-stream/";

    private static readonly ConcurrentDictionary<string, (DateTime, string)> _recordingClients = new();
    private WebSocketVoiceServer _wsVoiceServer = null;
    private static readonly string API_HEADER = "X-API-KEY";

    private readonly HashSet<string> _publicEndpoints;

    public HttpServer(ConcurrentDictionary<string, SRClientBase> connectedClients, ServerState serverState, Caliburn.Micro.IEventAggregator eventAggregator)
    {
        _connectedClients = connectedClients;
        _serverState = serverState;
        _eventAggregator = eventAggregator;
        _port = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT).IntValue;
        _enabled = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED).BoolValue;
        _authentication = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_API_KEY).RawValue.Trim();

        // Use the new parsing function
        _publicEndpoints = ServerSettingsStore.Instance.GetPublicHttpEndpoints();
    }

    public void Start()
    {
        if (_enabled)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + _port + "/");
            _listener.Start();
            Logger.Info($"HTTP Server Started on Port: {_port}");
            Logger.Info($"HTTP Server Header {API_HEADER} Required: {_authentication}" );
            Receive();

            // after starting the HTTP server start the WebSocket voice server because it depends on the HTTP server
            StartWebSocketVoiceServer();
        }
        else
        {
            Logger.Info("HTTP Server DISABLED on PORT " + _port);
        }
    }

    public void Stop()
    {
        if (_enabled)
        {
            Logger.Info("HTTP Server Stopped on Port " + _port);
            _listener.Stop();

            // when stopping the HTTP server also stop the WebSocket voice server
            StopWebSocketVoiceServer();
        }
    }

    private void Receive()
    {
        _listener.BeginGetContext(ListenerCallback, _listener);
    }

    private void ListenerCallback(IAsyncResult result)
    {
        if (_listener.IsListening)
        {
            var context = _listener.EndGetContext(result);

            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling HTTP Request");
                try
                {
                    context.Response.StatusCode = 500;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // ignored
            }

            Receive();
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        Logger.Info(
            $"HTTP Request {context?.Request?.Url} {context?.Request?.HttpMethod} from {context?.Request?.RemoteEndPoint}");

        if (context.Request.Url == null)
            return;

        var path = context.Request.Url.AbsolutePath;
        if (!_publicEndpoints.Contains(path) && context.Request.Headers.Get(API_HEADER) != _authentication)
        {
            context.Response.StatusCode = 401;
            context.Response.StatusDescription = $"Unauthorized - Verify you've sent the Header: {API_HEADER} YOU_API_KEY correctly. The API KEY will be printed in the logs on server startup";
            return;
        }
        
        if (context.Request.HttpMethod == "GET" && context.Request.Url != null &&
            context.Request.Url.AbsolutePath == CLIENTS_LIST)
        {
            var data = new ClientListExport
                { Clients = _connectedClients.Values, ServerVersion = UpdaterChecker.VERSION };
            var json = JsonSerializer.Serialize(data) + "\n";

            var output = context.Response.OutputStream;
            using (output)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var buffer = Encoding.UTF8.GetBytes(json);
                output.Write(buffer, 0, buffer.Length);
                output.Flush();
            }
        }
        else if (context.Request.HttpMethod == "POST")
        {
            if (context.Request.Url.AbsolutePath.StartsWith(CLIENT_BAN_GUID))
            {
                var clientGuid = context.Request.Url.AbsolutePath.Replace(CLIENT_BAN_GUID, "");

                if (_connectedClients.TryGetValue(clientGuid, out var client))
                    _serverState.WriteBanIP(client);
                else
                    context.Response.StatusCode = 404;
            }
            else if (context.Request.Url.AbsolutePath.StartsWith(CLIENT_BAN_NAME))
            {
                var clientName = context.Request.Url.AbsolutePath.Replace(CLIENT_BAN_NAME, "").Trim()
                    .ToLowerInvariant();

                foreach (var client in _connectedClients)
                    if (client.Value.Name.Trim().ToLowerInvariant() == clientName)
                    {
                        _serverState.WriteBanIP(client.Value);
                        context.Response.StatusCode = 200;
                        return;
                    }

                context.Response.StatusCode = 404;
            }
            else if (context.Request.Url.AbsolutePath.StartsWith(CLIENT_KICK_GUID))
            {
                var clientGuid = context.Request.Url.AbsolutePath.Replace(CLIENT_KICK_GUID, "");

                if (_connectedClients.TryGetValue(clientGuid, out var client))
                {
                    _serverState.KickClient(client);
                    context.Response.StatusCode = 200;
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            else if (context.Request.Url.AbsolutePath.StartsWith(CLIENT_KICK_NAME))
            {
                var clientName = context.Request.Url.AbsolutePath.Replace(CLIENT_KICK_NAME, "").Trim()
                    .ToLowerInvariant();

                foreach (var client in _connectedClients)
                    if (client.Value.Name.Trim().ToLowerInvariant() == clientName)
                    {
                        _serverState.KickClient(client.Value);
                        context.Response.StatusCode = 200;
                        return;
                    }

                context.Response.StatusCode = 404;
            }
            else if (context.Request.Url.AbsolutePath == REGISTER_VOICE_STRAM)
            {
                int maxConnections = ServerSettingsStore.Instance
                    .GetServerSetting(ServerSettingsKeys.WEBSOCKET_SERVER_MAX_CONNECTIONS).IntValue;

                if (_recordingClients.Count >= maxConnections)
                {
                    context.Response.StatusCode = 429;
                    var responseJson = "{\"error\":\"Maximum number of recording clients reached.\"}";
                    var buffer = Encoding.UTF8.GetBytes(responseJson);
                    context.Response.ContentType = "application/json";
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Flush();
                    Logger.Warn($"Recording client registration refused: max connections reached (max: {maxConnections}).");
                    return;
                }

                // Get remote IP
                var remoteIp = context.Request.RemoteEndPoint?.Address.ToString();

                // Check for duplicate IP
                if (!string.IsNullOrEmpty(remoteIp) && _recordingClients.Values.Any(v => v.Item2 == remoteIp))
                {
                    context.Response.StatusCode = 409; // Conflict
                    var responseJson = "{\"error\":\"A recording client from this IP is already registered.\"}";
                    var buffer = Encoding.UTF8.GetBytes(responseJson);
                    context.Response.ContentType = "application/json";
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Flush();
                    Logger.Warn($"Recording client registration refused: duplicate IP {remoteIp}.");
                    return;
                }

                // Register new client
                var id = Guid.NewGuid().ToString();
                _recordingClients[id] = (DateTime.UtcNow, remoteIp);
                _eventAggregator.PublishOnBackgroundThreadAsync(new RecordingClientRegistered(id));

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var response = $"{{\"id\":\"{id}\"}}";
                var responseBuffer = Encoding.UTF8.GetBytes(response);
                context.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
                context.Response.OutputStream.Flush();

                Logger.Info($"Recording client '{id}' registered for voice stream from IP {remoteIp}.");
                return;
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    }

    public static IEnumerable<string> GetRecordingClientIds()
    {
        return _recordingClients.Keys;
    }

    public void StartWebSocketVoiceServer()
    {
        if (_wsVoiceServer == null)
        {
            _wsVoiceServer = new WebSocketVoiceServer(_eventAggregator);
            Task.Run(() => _wsVoiceServer.Start());
        }
    }

    public void StopWebSocketVoiceServer()
    {
        _wsVoiceServer?.Stop();
    }
}