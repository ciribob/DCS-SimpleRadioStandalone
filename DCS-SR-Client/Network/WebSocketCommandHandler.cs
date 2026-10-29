using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network;

public class WebSocketCommandHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly ConcurrentDictionary<WebSocket, bool> Clients = new();
    private static readonly SemaphoreSlim SendLock = new(1, 1);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromMilliseconds(500);
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private HttpListener _listener;
    private volatile bool _stop;

    public static async Task SendAsync(byte[] bytes)
    {
        if (Clients.IsEmpty) return;

        await SendLock.WaitAsync();
        try
        {
            foreach (var client in Clients.Keys)
                try
                {
                    //don't let a client that has stopped reading block the DCS sync loop
                    using var timeout = new CancellationTokenSource(SendTimeout);
                    await client.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
                }
                catch (Exception)
                {
                    Clients.TryRemove(client, out _);
                    client.Abort();
                }
        }
        finally
        {
            SendLock.Release();
        }
    }

    public void Start()
    {
        var origins = _globalSettings.GetClientSetting(GlobalSettingsKeys.CommandListenerWebSocketOrigins).RawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (origins.Length == 0) return;

        Task.Run(async Task () =>
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(
                    $"http://localhost:{_globalSettings.GetNetworkSetting(GlobalSettingsKeys.CommandListenerWebSocket)}/");
                _listener.Start();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex,
                    $"Unable to start the WebSocket Command Listener on Port: {_globalSettings.GetNetworkSetting(GlobalSettingsKeys.CommandListenerWebSocket)}");
                return;
            }

            while (!_stop)
                try
                {
                    var context = await _listener.GetContextAsync();

                    if (!context.Request.IsWebSocketRequest || !context.Request.IsLocal ||
                        !origins.Contains(context.Request.Headers["Origin"]))
                    {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                        continue;
                    }

                    var webSocket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                    _ = Task.Run(() => ReceiveAsync(webSocket));
                }
                catch (Exception e)
                {
                    if (!_stop) Logger.Error(e, "Exception Handling WebSocket Connection");
                }
        });
    }

    private async Task ReceiveAsync(WebSocket webSocket)
    {
        Clients[webSocket] = true;

        try
        {
            using var udpClient = new UdpClient();
            var buffer = new byte[8192];

            while (!_stop && webSocket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                var bytes = message.ToArray();
                var port = JsonNode.Parse(bytes) is JsonObject json &&
                           (json.ContainsKey("controller") || json.ContainsKey("los"))
                    ? GlobalSettingsKeys.LotATCIncomingUDP
                    : GlobalSettingsKeys.CommandListenerUDP;

                await udpClient.SendAsync(bytes, bytes.Length,
                    new IPEndPoint(IPAddress.Loopback, _globalSettings.GetNetworkSetting(port)));
            }
        }
        catch (Exception e)
        {
            if (!_stop) Logger.Error(e, "Exception Handling WebSocket Message");
        }
        finally
        {
            Clients.TryRemove(webSocket, out _);
            webSocket.Dispose();
        }
    }

    public void Stop()
    {
        _stop = true;

        try
        {
            _listener?.Close();
        }
        catch (Exception)
        {
            //IGNORE
        }

        foreach (var client in Clients.Keys) client.Abort();
    }
}