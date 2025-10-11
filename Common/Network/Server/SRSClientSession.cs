using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.NetCoreServer;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class SRSClientSession : TcpSession
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly HashSet<IPAddress> _bannedIps;

    // Received data string.
    private readonly StringBuilder _receiveBuffer = new();

    private string _ip;
    private long _lastFullRadioSent;
    private int _port;

    public SRSClientSession(ServerSync server,
        HashSet<IPAddress> bannedIps) : base(server)
    {
        _bannedIps = bannedIps;
    }

    public long LastMetaDataSent { get; set; }

    public long LastFullRadioSent
    {
        get => _lastFullRadioSent;
        set
        {
            _lastFullRadioSent = value;
            //metadata is always sent with a full update
            LastMetaDataSent = value;
        }
    }

    public long LastMessageReceived { get; set; }

    public string SRSGuid { get; set; }

    protected override void OnConnected()
    {
        var clientIp = (IPEndPoint)Socket.RemoteEndPoint;

        EventBus.Instance.PublishOnBackgroundThreadAsync(new SRSClientStatus
        {
            Connected = true,
            ClientIP = clientIp.ToString(),
            SRSGuid = SRSGuid
        });

        _ip = clientIp.Address.ToString();
        _port = clientIp.Port;

        if (_bannedIps.Contains(clientIp.Address))
        {
            Logger.Warn("Disconnecting Banned Client -  " + clientIp.Address + " " + clientIp.Port);

            Disconnect();
        }
    }

    protected override void OnSent(long sent, long pending)
    {
        // Disconnect slow client with 50MB send buffer
        if (pending > 5e+7)
        {
            Logger.Error("Disconnecting - pending is too large");
            Disconnect();
        }
    }

    protected override void OnDisconnected()
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new SRSClientStatus
        {
            Connected = false,
            SRSGuid = SRSGuid,
            ClientIP = $"{_ip}:{_port}"
        });

        _receiveBuffer.Clear();
        ((ServerSync)Server).HandleDisconnect(this);
    }

    private List<NetworkMessage> GetNetworkMessage()
    {
        var messages = new List<NetworkMessage>();
        //search for a \n, extract up to that \n and then remove from buffer
        var content = _receiveBuffer.ToString();
        while (content.Length > 2 && content.Contains("\n"))
        {
            //extract message
            var message = content.Substring(0, content.IndexOf("\n", StringComparison.Ordinal) + 1);

            //now clear from buffer
            _receiveBuffer.Remove(0, message.Length);

            try
            {
                var networkMessage = JsonSerializer.Deserialize<NetworkMessage>(message.Trim(), new JsonSerializerOptions()
                {
                    IncludeFields = true,
                });
                //trim the received part
                messages.Add(networkMessage);
            }
            catch (Exception ex)
            {
                // Can be extremely noisy, use only for debugging!
                // We conditionally check the trace to avoid building a string with a potentially really long message.
                if (Logger.IsTraceEnabled)
                {
                    Logger.Trace(ex, $"Unable to process JSON: \n {message}");
                }
                
            }


            //load in next part
            content = _receiveBuffer.ToString();
        }

        return messages;
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        _receiveBuffer.Append(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

        LastMessageReceived = DateTime.Now.Ticks;

        foreach (var s in GetNetworkMessage()) ((ServerSync)Server).HandleMessage(this, s);
    }

    protected override void OnTrySendException(Exception ex)
    {
        Logger.Error(ex, "Caught Client Session Exception");
    }

    protected override void OnError(SocketError error)
    {
        Logger.Error($"Caught Socket Error: {error}");
    }

    /**
     * Send a full radio update every 120 seconds if one hasnt been sent
     * This will be triggered by a metadata UPDATE message
     */
    public bool ShouldSendFullRadioUpdate()
    {
        var lastSent = new TimeSpan(DateTime.Now.Ticks - LastFullRadioSent);
        return lastSent.TotalSeconds > Constants.CLIENT_UPDATE_INTERVAL_LIMIT - 5;
    }

    public bool ShouldSendMetadataUpdate()
    {
        var lastSent = new TimeSpan(DateTime.Now.Ticks - LastMetaDataSent);
        return lastSent.TotalSeconds > Constants.CLIENT_UPDATE_INTERVAL_LIMIT - 5;
    }
}