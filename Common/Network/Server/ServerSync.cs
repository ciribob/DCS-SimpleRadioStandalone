using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.NetCoreServer;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class ServerSync : TcpServer, IHandle<ServerSettingsChangedMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly HashSet<IPAddress> _bannedIps;

    private readonly ConcurrentDictionary<string, SRClientBase> _clients = new();
    private readonly IEventAggregator _eventAggregator;
    private readonly NatHandler _natHandler;

    private readonly ServerSettingsStore _serverSettings;

    public ServerSync(ConcurrentDictionary<string, SRClientBase> connectedClients, HashSet<IPAddress> _bannedIps,
        IEventAggregator eventAggregator) : base(IPAddress.Any,
        ServerSettingsStore.Instance.GetServerPort())
    {
        _clients = connectedClients;
        this._bannedIps = _bannedIps;
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnPublishedThread(this);
        _serverSettings = ServerSettingsStore.Instance;

        OptionKeepAlive = true;

        if (_serverSettings.GetServerSetting(ServerSettingsKeys.UPNP_ENABLED).BoolValue)
        {
            _natHandler = new NatHandler(_serverSettings.GetServerPort());
            _natHandler.OpenNAT();
        }
    }

    public async Task HandleAsync(ServerSettingsChangedMessage message, CancellationToken token)
    {
        await Task.Run(() =>
        {
            try
            {
                HandleServerSettingsMessage();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception Sending Server Settings ");
            }
        });
    }

    protected override TcpSession CreateSession()
    {
        return new SRSClientSession(this, _bannedIps);
    }

    protected override void OnError(SocketError error)
    {
        Logger.Error($"TCP SERVER ERROR: {error} ");
    }

    public void StartListening()
    {
        OptionKeepAlive = true;
        try
        {
            Start();
        }
        catch (Exception ex)
        {
            try
            {
                _natHandler?.CloseNAT();
            }
            catch
            {
            }

            Logger.Error(ex, "Unable to start the SRS Server");


            Environment.Exit(0);
        }
    }

    public void HandleDisconnect(SRSClientSession state)
    {
        Logger.Info("Disconnecting Client");

        if (state != null && state.SRSGuid != null)
        {
            //removed
            SRClientBase client;
            _clients.TryRemove(state.SRSGuid, out client);

            if (client != null)
            {
                Logger.Info("Removed Disconnected Client " + state.SRSGuid);

                HandleClientDisconnect(state, client);
            }

            try
            {
                _eventAggregator.PublishOnUIThreadAsync(
                    new ServerStateMessage(true, new List<SRClientBase>(_clients.Values), state.SRSGuid));
            }
            catch (Exception ex)
            {
                Logger.Info(ex, "Exception Publishing Client Update After Disconnect");
            }
        }
        else
        {
            Logger.Info("Removed Disconnected Unknown Client");
        }
    }


    public void HandleMessage(SRSClientSession state, NetworkMessage message)
    {
        try
        {
            Logger.Debug($"Received:  Msg - {message.MsgType} from {state.SRSGuid}");

            if (message.MsgType != NetworkMessage.MessageType.GATEWAY_CLIENT_FULL_UPDATE &&
                message.MsgType != NetworkMessage.MessageType.GATEWAY_CLIENT_METADATA_UPDATE)
            {
                if (!HandleConnectedClient(state, message))
                    Logger.Info($"Invalid Client - disconnecting {state.SRSGuid}");
            }

            switch (message.MsgType)
            {
                case NetworkMessage.MessageType.PING:
                    // Do nothing for now
                    break;
                case NetworkMessage.MessageType.UPDATE: //Partial metadata update
                    if (!HandleClientMetaDataUpdate(state, message, true, out var client))
                        if (state.ShouldSendFullRadioUpdate())
                            SendFullRadioUpdate(state, client);

                    break;
                case NetworkMessage.MessageType.RADIO_UPDATE: //Full update with radio info
                    var sent = HandleClientMetaDataUpdate(state, message, false, out var ignore);
                    HandleClientRadioUpdate(state, message, sent);
                    break;
                case NetworkMessage.MessageType.SYNC:
                    HandleRadioClientsSync(state, message);
                    break;
                case NetworkMessage.MessageType.SERVER_SETTINGS:
                    HandleServerSettingsMessage();
                    break;
                case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:
                    HandleExternalAWACSModePassword(state, message.ExternalAWACSModePassword, message.Client);
                    break;
                case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_DISCONNECT:
                    HandleExternalAWACSModeDisconnect(state, message.Client);
                    break;
                case NetworkMessage.MessageType.GATEWAY_CLIENT_FULL_UPDATE:
                case NetworkMessage.MessageType.GATEWAY_CLIENT_METADATA_UPDATE:
                    //Update Metadata data and send metadata update
                    HandleGatewayClientUpdate(message);
                    break;
                case NetworkMessage.MessageType.GATEWAY_CLIENT_DISCONNECT:
                    HandleGatewayClientDisconnect(state, message.Client);
                    break;
                default:
                    Logger.Warn("Recevied unknown message type");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception Handling Message " + ex.Message);
        }
    }

    private void HandleGatewayClientDisconnect(SRSClientSession state, SRClientBase disconnectedClient)
    {
        state.GatewayClients.TryRemove(disconnectedClient.ClientGuid, out _);
        if (_clients.TryRemove(disconnectedClient.ClientGuid, out _))
        {
            Logger.Info("Removed Disconnected Gateway Client " + disconnectedClient.ClientGuid);

            var message = new NetworkMessage
            {
                Client = disconnectedClient.DeepClone(),
                MsgType = NetworkMessage.MessageType.CLIENT_DISCONNECT
            };

            Multicast(message.Encode());

            try
            {
                _eventAggregator.PublishOnUIThreadAsync(
                    new ServerStateMessage(true, new List<SRClientBase>(_clients.Values), state.SRSGuid));
            }
            catch (Exception ex)
            {
                Logger.Info(ex, "Exception Publishing Client Update After Disconnect");
            }
        }
    }

    private void HandleGatewayClientUpdate(NetworkMessage message)
    {
        SRClientBase client;
        if (!_clients.TryGetValue(message.Client.ClientGuid, out client))
        {
            client = message.Client.DeepClone();
            _clients[message.Client.ClientGuid] = client;
        }

        if (message.Client.LatLngPosition == null) message.Client.LatLngPosition = new LatLngPosition();

        if (message.MsgType == NetworkMessage.MessageType.GATEWAY_CLIENT_METADATA_UPDATE)
        {
            bool changed = !client.MetaDataEquals(message.Client, true);

            if (changed)
            {
                //copy the data we need
                client.Name = message.Client.Name;
                client.Coalition = message.Client.Coalition;
                client.LatLngPosition = message.Client.LatLngPosition;
                client.Seat = message.Client.Seat;
                client.AllowRecord = message.Client.AllowRecord;

                //send update to everyone
                //Remove Client Radio Info
                var replyMessage = new NetworkMessage
                {
                    MsgType = NetworkMessage.MessageType.UPDATE,
                    Client = new SRClientBase
                    {
                        ClientGuid = client.ClientGuid,
                        Coalition = client.Coalition,
                        Name = client.Name,
                        LatLngPosition = client.LatLngPosition,
                        Seat = client.Seat,
                        AllowRecord = client.AllowRecord,
                        Gateway = client.Gateway,
                        DISEntityId = client.DISEntityId,
                        //remove radios
                        RadioInfo = null
                    }
                };

                Multicast(replyMessage.Encode());
                _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                    new List<SRClientBase>(_clients.Values)));
            }
        }
        else if (message.MsgType == NetworkMessage.MessageType.GATEWAY_CLIENT_FULL_UPDATE)
        {
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE,
                Client = new SRClientBase
                {
                    ClientGuid = client.ClientGuid,
                    Coalition = client.Coalition,
                    Name = client.Name,
                    LatLngPosition = client.LatLngPosition,
                    RadioInfo = client.RadioInfo, //send radio info
                    Seat = client.Seat,
                    DISEntityId = client.DISEntityId,
                    Gateway = client.Gateway,
                    AllowRecord = client.AllowRecord
                }
            };
            Multicast(replyMessage.Encode());

            _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                new List<SRClientBase>(_clients.Values)));
        }
    }

    private bool HandleConnectedClient(SRSClientSession state, NetworkMessage message)
    {
        var srClient = message.Client;
        if (!_clients.ContainsKey(srClient.ClientGuid))
        {
            var clientIp = (IPEndPoint)state.Socket.RemoteEndPoint;
            if (message.Version == null)
            {
                Logger.Warn("Disconnecting Unversioned Client -  " + clientIp.Address + " " +
                            clientIp.Port);
                state.Disconnect();
                return false;
            }

            var clientVersion = Version.Parse(message.Version);
            var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

            if (clientVersion < protocolVersion)
            {
                Logger.Warn(
                    $"Disconnecting Unsupported  Client Version - Version {clientVersion} IP {clientIp.Address} Port {clientIp.Port}");
                HandleVersionMismatch(state);

                //close socket after
                state.Disconnect();

                return false;
            }

            //add to proper list
            _clients[srClient.ClientGuid] = srClient;

            state.SRSGuid = srClient.ClientGuid;
            srClient.ClientSession = state.Id;


            _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                new List<SRClientBase>(_clients.Values)));
        }

        return true;
    }

    private void HandleServerSettingsMessage()
    {
        //send server settings
        var replyMessage = new NetworkMessage
        {
            MsgType = NetworkMessage.MessageType.SERVER_SETTINGS,
            ServerSettings = _serverSettings.ToDictionary()
        };

        Multicast(replyMessage.Encode());
    }

    private void HandleVersionMismatch(SRSClientSession session)
    {
        //send server settings
        var replyMessage = new NetworkMessage
        {
            MsgType = NetworkMessage.MessageType.VERSION_MISMATCH
        };
        session.Send(replyMessage.Encode());
    }

    private bool HandleClientMetaDataUpdate(SRSClientSession session, NetworkMessage message, bool send,
        out SRClientBase client)
    {
        var changed = false;
        if (_clients.TryGetValue(session.SRSGuid, out client))
            if (client != null)
            {
                if (message.Client.LatLngPosition == null) message.Client.LatLngPosition = new LatLngPosition();

                changed = !client.MetaDataEquals(message.Client, true);

                //check timeout
                if (changed || session.ShouldSendMetadataUpdate())
                {
                    //copy the data we need
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.LatLngPosition = message.Client.LatLngPosition;
                    client.Seat = message.Client.Seat;
                    client.AllowRecord = message.Client.AllowRecord;

                    //send update to everyone
                    //Remove Client Radio Info
                    var replyMessage = new NetworkMessage
                    {
                        MsgType = NetworkMessage.MessageType.UPDATE,
                        Client = new SRClientBase
                        {
                            ClientGuid = client.ClientGuid,
                            Coalition = client.Coalition,
                            Name = client.Name,
                            LatLngPosition = client.LatLngPosition,
                            Seat = client.Seat,
                            AllowRecord = client.AllowRecord,
                            Gateway = client.Gateway,
                            DISEntityId = client.DISEntityId,
                            //remove radios
                            RadioInfo = null
                        }
                    };

                    if (send)
                    {
                        //Client state updated
                        Multicast(replyMessage.Encode());
                        session.LastMetaDataSent = DateTime.Now.Ticks;
                        _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                            new List<SRClientBase>(_clients.Values)));
                    }
                }
            }

        return changed;
    }

    private void HandleClientDisconnect(SRSClientSession srsSession, SRClientBase client)
    {
        var message = new NetworkMessage
        {
            Client = client,
            MsgType = NetworkMessage.MessageType.CLIENT_DISCONNECT
        };

        MulticastAllExeceptOne(message.Encode(), srsSession.Id);

        if (!srsSession.GatewayClients.IsEmpty)
        {
            var gatewayClients = _clients.Values.Where(connectedClient => connectedClient.GatewayClient).ToList();
            //find all gateway clients and remove - currently assume only one gateway per server

            foreach (var gatewayClientGuid in srsSession.GatewayClients.Keys)
            {
                if (_clients.TryRemove(gatewayClientGuid, out var gatewayClient))
                {
                    Logger.Info("Removed Gateway Client " + gatewayClient.ClientGuid);
                    MulticastAllExeceptOne(new NetworkMessage
                    {
                        Client = gatewayClient,
                        MsgType = NetworkMessage.MessageType.CLIENT_DISCONNECT
                    }.Encode(), srsSession.Id);
                }
            }
        }

        try
        {
            srsSession.Dispose();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void HandleClientRadioUpdate(SRSClientSession session, NetworkMessage message, bool send)
    {
        if (_clients.TryGetValue(session.SRSGuid, out var client))
            if (client != null)
            {
                var changed = false;

                //shouldnt be the case but just incase...
                if (message.Client.RadioInfo == null)
                {
                    message.Client.RadioInfo = new PlayerRadioInfoBase();
                    changed = true;
                }
                else
                {
                    changed = !client.RadioInfo.Equals(message.Client.RadioInfo);
                }

                client.RadioInfo = message.Client.RadioInfo;

                var lastSent = new TimeSpan(DateTime.Now.Ticks - session.LastFullRadioSent);

                //send update to everyone
                if (send || changed || lastSent.TotalSeconds > Constants.CLIENT_UPDATE_INTERVAL_LIMIT - 5)
                    SendFullRadioUpdate(session, client);
            }
    }

    private void SendFullRadioUpdate(SRSClientSession session, SRClientBase client)
    {
        var replyMessage = new NetworkMessage
        {
            MsgType = NetworkMessage.MessageType.RADIO_UPDATE,
            Client = new SRClientBase
            {
                ClientGuid = client.ClientGuid,
                Coalition = client.Coalition,
                Name = client.Name,
                LatLngPosition = client.LatLngPosition,
                RadioInfo = client.RadioInfo, //send radio info
                Seat = client.Seat,
                DISEntityId = client.DISEntityId,
                Gateway = client.Gateway,
                AllowRecord = client.AllowRecord
            }
        };
        Multicast(replyMessage.Encode());
        //marks both as metadata is included in full radio
        session.LastFullRadioSent = DateTime.Now.Ticks;
        _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
            new List<SRClientBase>(_clients.Values)));
    }

    private void HandleRadioClientsSync(SRSClientSession session, NetworkMessage message)
    {
        //store new client
        var replyMessage = new NetworkMessage
        {
            MsgType = NetworkMessage.MessageType.SYNC,
            Clients = new List<SRClientBase>(_clients.Values),
            ServerSettings = _serverSettings.ToDictionary(),
            Version = UpdaterChecker.VERSION
        };

        session.Send(replyMessage.Encode());

        //send update to everyone
        var update = new NetworkMessage
        {
            MsgType = NetworkMessage.MessageType.RADIO_UPDATE,
            Client = new SRClientBase
            {
                ClientGuid = message.Client.ClientGuid,
                RadioInfo = message.Client.RadioInfo,
                Name = message.Client.Name,
                Coalition = message.Client.Coalition,
                Seat = message.Client.Seat,
                DISEntityId = message.Client.DISEntityId,
                Gateway = message.Client.Gateway,
                LatLngPosition = message.Client.LatLngPosition,
                AllowRecord = message.Client.AllowRecord
            }
        };

        Multicast(update.Encode());
    }

    private void HandleExternalAWACSModePassword(SRSClientSession session, string password, SRClientBase client)
    {
        // Response of clientCoalition = 0 indicates authentication success (or external AWACS mode disabled)
        var clientCoalition = 0;
        if (_serverSettings.GetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue
            && !string.IsNullOrWhiteSpace(password))
        {
            if (_serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD)
                    .StringValue == password)
                clientCoalition = 2;
            else if (_serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD)
                         .StringValue == password) clientCoalition = 1;
        }

        if (_clients.ContainsKey(client.ClientGuid))
        {
            _clients[client.ClientGuid].Coalition = clientCoalition;
            _clients[client.ClientGuid].Name = client.Name;

            _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                new List<SRClientBase>(_clients.Values)));
        }

        var replyMessage = new NetworkMessage
        {
            Client = new SRClientBase
            {
                Coalition = clientCoalition
            },
            MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD
        };

        session.Send(replyMessage.Encode());

        var message = new NetworkMessage
        {
            MsgType = NetworkMessage.MessageType.UPDATE,
            Client = new SRClientBase
            {
                ClientGuid = client.ClientGuid,
                Coalition = clientCoalition,
                Name = client.Name,
                LatLngPosition = client.LatLngPosition,
                Seat = client.Seat,
                AllowRecord = client.AllowRecord,
                Gateway = client.Gateway,
                DISEntityId = client.DISEntityId,
            }
        };

        Multicast(message.Encode());
    }

    private void HandleExternalAWACSModeDisconnect(SRSClientSession session, SRClientBase client)
    {
        if (_clients.ContainsKey(client.ClientGuid))
        {
            _clients[client.ClientGuid].Coalition = 0;
            _clients[client.ClientGuid].Name = "";

            _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                new List<SRClientBase>(_clients.Values)));

            var message = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE,
                Client = new SRClientBase
                {
                    ClientGuid = client.ClientGuid,
                    Coalition = client.Coalition,
                    Name = client.Name,
                    RadioInfo = new PlayerRadioInfoBase(),
                    LatLngPosition = client.LatLngPosition,
                    Seat = client.Seat,
                    AllowRecord = client.AllowRecord,
                    Gateway = client.Gateway,
                    DISEntityId = client.DISEntityId,
                }
            };

            MulticastAllExeceptOne(message.Encode(), session.Id);
        }
    }

    public void RequestStop()
    {
        try
        {
            _natHandler?.CloseNAT();
        }
        catch
        {
        }

        try
        {
            DisconnectAll();
            Stop();
            _clients.Clear();
        }
        catch (Exception)
        {
        }
    }
}