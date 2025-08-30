using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class HttpServer
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ConcurrentDictionary<string, SRClientBase> _connectedClients;
    private readonly bool _enabled;
    private readonly int _port;
    private readonly ServerState _serverState;

    private HttpListener _listener;
    
    private static readonly string CLIENT_BAN_GUID = "/client/ban/guid";
    private static readonly string CLIENT_BAN_NAME = "/client/ban/name";
    private static readonly string CLIENT_KICK_GUID = "/client/kick/guid";
    private static readonly string CLIENT_KICK_NAME = "/client/kick/name";
    private static readonly string CLIENTS_LIST = "/clients";
    private static readonly string CLIENT_REGISTER_VOICE = "/client/register/voice";

    private static readonly ConcurrentDictionary<string, DateTime> _recordingClients = new();

    public HttpServer(ConcurrentDictionary<string, SRClientBase> connectedClients, ServerState serverState)
    {
        _connectedClients = connectedClients;
        _serverState = serverState;
        _port = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT).IntValue;
        _enabled = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED).BoolValue;
    }

    public void Start()
    {
        if (_enabled)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://*:" + _port + "/");
            _listener.Start();
            Logger.Info("HTTP Server Started on Port " + _port);
            Receive();
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
            else if (context.Request.Url.AbsolutePath == CLIENT_REGISTER_VOICE)
            {
                // Generate a unique ID for the recording client
                var id = Guid.NewGuid().ToString();
                _recordingClients[id] = DateTime.UtcNow;

                // Respond with the generated ID as JSON
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var responseJson = $"{{\"id\":\"{id}\"}}";
                var buffer = Encoding.UTF8.GetBytes(responseJson);
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Flush();

                Logger.Info($"Recording client '{id}' registered for voice stream.");
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
}