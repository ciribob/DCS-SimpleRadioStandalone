using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using LogManager = NLog.LogManager;
using Timer = System.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class TCPClientHandler : IHandle<DisconnectRequestMessage>, IHandle<UnitUpdateMessage>,
    IHandle<EAMConnectRequestMessage>, IHandle<GatewayUnitUpdateMessage>, IHandle<GatewayUnitDisconnectedMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly int MAX_DECODE_ERRORS = 5;
    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
    private readonly string _guid;

    private readonly Timer _idleTimeout;

    private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

    private long _lastSent = -1;
    private SRClientBase _playerUnitState;
    private IPEndPoint _serverEndpoint;

    private volatile bool _stop;
    private TcpClient _tcpClient;

    public TCPClientHandler(string guid, SRClientBase playerUnitState)
    {
        _clients.Clear();
        _guid = guid;
        _playerUnitState = playerUnitState;

        _idleTimeout = new Timer();
        _idleTimeout.Interval = TimeSpan.FromSeconds(30).TotalMilliseconds;
        _idleTimeout.Elapsed += CheckIfIdleTimeOut;
        _idleTimeout.AutoReset = true;
        _idleTimeout.Enabled = false;
    }

    public bool TCPConnected
    {
        get
        {
            if (_tcpClient != null) return _tcpClient.Connected;
            return false;
        }
    }

    public Task HandleAsync(DisconnectRequestMessage message, CancellationToken cancellationToken)
    {
        Disconnect();

        return Task.CompletedTask;
    }

    public Task HandleAsync(EAMConnectRequestMessage eamConnectRequestMessage, CancellationToken cancellationToken)
    {
        _playerUnitState.Name = eamConnectRequestMessage.Name;
        var message = new NetworkMessage
        {
            Client = _playerUnitState,
            ExternalAWACSModePassword = eamConnectRequestMessage.Password,
            MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD
        };

        SendToServer(message);

        return Task.CompletedTask;
    }

    public Task HandleAsync(GatewayUnitDisconnectedMessage message, CancellationToken cancellationToken)
    {
        var clone = message.UnitUpdate.DeepClone();
        clone.GatewayClient = true;

        clone.RadioInfo = null;
        var updateMessage = new NetworkMessage
        {
            Client = clone,
            MsgType = NetworkMessage.MessageType.GATEWAY_CLIENT_DISCONNECT
        };

        SendToServer(updateMessage);

        return Task.CompletedTask;
    }

    public Task HandleAsync(GatewayUnitUpdateMessage message, CancellationToken cancellationToken)
    {
        if (message.FullUpdate)
        {
            var clone = message.UnitUpdate.DeepClone();
            clone.GatewayClient = true;

            var updateMessage = new NetworkMessage
            {
                Client = clone,
                MsgType = NetworkMessage.MessageType.GATEWAY_CLIENT_FULL_UPDATE
            };

            var needValidPosition = _serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ||
                                    _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);

            if (!needValidPosition)
                updateMessage.Client.LatLngPosition = new LatLngPosition();


            SendToServer(updateMessage);
        }
        else
        {
            var clone = message.UnitUpdate.DeepClone();
            clone.GatewayClient = true;

            var needValidPosition = _serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ||
                                    _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);


            clone.RadioInfo = null;
            var updateMessage = new NetworkMessage
            {
                Client = clone,
                MsgType = NetworkMessage.MessageType.GATEWAY_CLIENT_METADATA_UPDATE
            };

            if (!needValidPosition)
                updateMessage.Client.LatLngPosition = new LatLngPosition();

            //update state
            SendToServer(updateMessage);
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(UnitUpdateMessage message, CancellationToken cancellationToken)
    {
        if (message.FullUpdate)
            ClientRadioUpdated(message.UnitUpdate);
        else
            ClientCoalitionUpdate(message.UnitUpdate);

        return Task.CompletedTask;
    }

    private void CheckIfIdleTimeOut(object state, ElapsedEventArgs elapsedEventArgs)
    {
        var timeout = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.IdleTimeOut).IntValue;
        if (_lastSent > 1 && TimeSpan.FromTicks(DateTime.Now.Ticks - _lastSent).TotalSeconds > timeout)
        {
            Logger.Warn("Disconnecting - Idle Time out");
            Disconnect();
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void TryConnect(IPEndPoint endpoint)
    {
        _serverEndpoint = endpoint;

        //make absolutely sure we only connect once
        try
        {
            _tcpClient?.Close();
        }
        catch (Exception)
        {
        }

        Logger.Info($"TryConnect @ {_serverEndpoint}");
        var tcpThread = new Thread(Connect);
        tcpThread.Start();
    }

    private void Connect()
    {
        _lastSent = DateTime.Now.Ticks;
        _idleTimeout.Enabled = true;
        _idleTimeout.Start();

        using (_tcpClient = new TcpClient())
        {
            try
            {
                _tcpClient.SendTimeout = 90000;
                _tcpClient.NoDelay = true;

                // Wait for 10 seconds before aborting connection attempt - no SRS server running/port opened in that case
                _tcpClient.ConnectAsync(_serverEndpoint.Address, _serverEndpoint.Port)
                    .Wait(TimeSpan.FromSeconds(10));

                if (_tcpClient.Connected)
                {
                    _tcpClient.NoDelay = true;

                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    ClientSyncLoop();
                }
                else
                {
                    Logger.Error($"Failed to connect to server @ {_serverEndpoint}");
                    EventBus.Instance.PublishOnUIThreadAsync(new TCPClientStatusMessage(false,
                        TCPClientStatusMessage.ErrorCode.TIMEOUT));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not connect to server");
                Disconnect();
            }
        }

        _idleTimeout.Enabled = false;
        _idleTimeout.Stop();
    }

    private void ClientRadioUpdated(SRClientBase updatedUnitState)
    {
        Logger.Debug(
            $"Sending Full Update to Server if there is a change or {Constants.CLIENT_UPDATE_INTERVAL_LIMIT} seconds have passed since last update");

        //Only send if there is an actual change
        if (!updatedUnitState.Equals(_playerUnitState)
            || TimeSpan.FromTicks(DateTime.Now.Ticks - _lastSent).TotalSeconds > Constants.CLIENT_UPDATE_INTERVAL_LIMIT)
        {
            _playerUnitState = updatedUnitState;
            var message = new NetworkMessage
            {
                Client = updatedUnitState,
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE
            };

            var needValidPosition = _serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ||
                                    _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);

            if (needValidPosition)
                message.Client.LatLngPosition = updatedUnitState.LatLngPosition;
            else
                message.Client.LatLngPosition = new LatLngPosition();


            SendToServer(message);
        }
    }

    private void ClientCoalitionUpdate(SRClientBase updatedMetadata)
    {
        Logger.Debug(
            $"Sending Full Update to Server if there is a change or {Constants.CLIENT_UPDATE_INTERVAL_LIMIT} seconds have passed since last update");
        var needValidPosition = _serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ||
                                _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);

        //only send if there is an actual change to metadata or 60 seconds have passed since last update
        if (!_playerUnitState.MetaDataEquals(updatedMetadata, needValidPosition)
            || TimeSpan.FromTicks(DateTime.Now.Ticks - _lastSent).TotalSeconds > Constants.CLIENT_UPDATE_INTERVAL_LIMIT)
        {
            _playerUnitState.AllowRecord = updatedMetadata.AllowRecord;
            _playerUnitState.Coalition = updatedMetadata.Coalition;
            _playerUnitState.Name = updatedMetadata.Name;
            _playerUnitState.Seat = updatedMetadata.Seat;
            _playerUnitState.LatLngPosition = updatedMetadata.LatLngPosition;

            var message = new NetworkMessage
            {
                Client = updatedMetadata,
                MsgType = NetworkMessage.MessageType.UPDATE
            };

            //double check this doesnt break anything else
            updatedMetadata.RadioInfo = null;

            if (needValidPosition)
                message.Client.LatLngPosition = updatedMetadata.LatLngPosition;
            else
                message.Client.LatLngPosition = new LatLngPosition();

            //update state
            SendToServer(message);
        }
    }

    private void ClientSyncLoop()
    {
        EventBus.Instance.SubscribeOnBackgroundThread(this);
        //clear the clients list
        _clients.Clear();
        var decodeErrors = 0; //if the JSON is unreadable - new version likely

        using (var reader = new StreamReader(_tcpClient.GetStream(), Encoding.UTF8))
        {
            try
            {
                //TODO switch to proxy for everything
                //TODO remove _clientstate and just pass in the initial state
                //then use broadcasts / events for the rest

                //start the loop off by sending a SYNC Request
                SendToServer(new NetworkMessage
                {
                    Client = _playerUnitState,
                    MsgType = NetworkMessage.MessageType.SYNC
                });

                EventBus.Instance.PublishOnUIThreadAsync(new TCPClientStatusMessage(true, _serverEndpoint));

                string line;
                while ((line = reader.ReadLine()) != null)
                    try
                    {
                        var serverMessage = JsonSerializer.Deserialize<NetworkMessage>(line, new JsonSerializerOptions()
                        {
                            IncludeFields = true
                        });
                        decodeErrors = 0; //reset counter
                        if (serverMessage != null)
                            Logger.Debug("Received " + serverMessage.MsgType);
                        switch (serverMessage.MsgType)
                        {
                            case NetworkMessage.MessageType.PING:
                                // Do nothing for now
                                break;
                            case NetworkMessage.MessageType.RADIO_UPDATE:
                            case NetworkMessage.MessageType.UPDATE:

                                if (serverMessage.ServerSettings != null)
                                    _serverSettings.Decode(serverMessage.ServerSettings);

                                SRClientBase srClient;
                                if (_clients.TryGetValue(serverMessage.Client.ClientGuid, out srClient))
                                {
                                    if (serverMessage.MsgType == NetworkMessage.MessageType.RADIO_UPDATE)
                                        HandleFullUpdate(serverMessage, srClient);
                                    else if (serverMessage.MsgType == NetworkMessage.MessageType.UPDATE)
                                        HandlePartialUpdate(serverMessage, srClient);
                                }
                                else
                                {
                                    var connectedClient = serverMessage.Client;

                                    //init with LOS true so you can hear them incase of bad DCS install where
                                    //LOS isnt working
                                    connectedClient.LineOfSightLoss = 0.0f;
                                    //0.0 is NO LOSS therefore full Line of sight

                                    _clients[serverMessage.Client.ClientGuid] = connectedClient;

                                    srClient = connectedClient;

                                    // Logger.Debug("Received New Client: " + NetworkMessage.MessageType.UPDATE +
                                    //             " From: " +
                                    //             serverMessage.Client.Name + " Coalition: " +
                                    //             serverMessage.Client.Coalition);
                                }

                                EventBus.Instance.PublishOnBackgroundThreadAsync(new SRClientUpdateMessage(srClient));

                                break;
                            case NetworkMessage.MessageType.SYNC:
                                // Logger.Info("Recevied: " + NetworkMessage.MessageType.SYNC);

                                //check server version
                                if (serverMessage.Version == null)
                                {
                                    Logger.Error("Disconnecting Unversioned Server");
                                    Disconnect();
                                    break;
                                }

                                var serverVersion = Version.Parse(serverMessage.Version);
                                var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

                                SyncedServerSettings.Instance.ServerVersion = serverMessage.Version;

                                if (serverVersion < protocolVersion)
                                {
                                    Logger.Error(
                                        $"Server version ({serverMessage.Version}) older than minimum procotol version ({UpdaterChecker.MINIMUM_PROTOCOL_VERSION}) - disconnecting");

                                    //TODO show warning
                                    ShowVersionMistmatchWarning(serverMessage.Version);

                                    Disconnect();
                                    break;
                                }

                                if (serverMessage.Clients != null)
                                    foreach (var client in serverMessage.Clients)
                                    {
                                        //init with LOS true so you can hear them incase of bad DCS install where
                                        //LOS isnt working
                                        client.LineOfSightLoss = 0.0f;
                                        //0.0 is NO LOSS therefore full Line of sight
                                        _clients[client.ClientGuid] = client;

                                        EventBus.Instance.PublishOnBackgroundThreadAsync(
                                            new SRClientUpdateMessage(client));
                                    }

                                //add server settings
                                _serverSettings.Decode(serverMessage.ServerSettings);
                                break;

                            case NetworkMessage.MessageType.SERVER_SETTINGS:

                                _serverSettings.Decode(serverMessage.ServerSettings);
                                SyncedServerSettings.Instance.ServerVersion = serverMessage.Version;

                                if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                    EventBus.Instance.PublishOnUIThreadAsync(new EAMDisconnectMessage());

                                break;
                            case NetworkMessage.MessageType.CLIENT_DISCONNECT:

                                SRClientBase outClient;
                                _clients.TryRemove(serverMessage.Client.ClientGuid, out outClient);

                                if (outClient != null)
                                    EventBus.Instance.PublishOnBackgroundThreadAsync(
                                        new SRClientUpdateMessage(outClient, false));

                                break;
                            case NetworkMessage.MessageType.VERSION_MISMATCH:
                                Logger.Error(
                                    $"Version Mismatch Between Client ({UpdaterChecker.VERSION}) & Server ({serverMessage.Version}) - Disconnecting");

                                //TODO handle this on the client
                                ShowVersionMistmatchWarning(serverMessage.Version);

                                Disconnect();
                                break;
                            case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:


                                if (serverMessage.Client.Coalition == 0)
                                {
                                    Logger.Info("External AWACS mode authentication failed");
                                    //TODO handle this on the client
                                    EventBus.Instance.PublishOnUIThreadAsync(new EAMDisconnectMessage());
                                }
                                else
                                {
                                    //TODO handle this on the client
                                    EventBus.Instance.PublishOnUIThreadAsync(
                                        new EAMConnectedMessage(serverMessage.Client.Coalition));
                                }

                                break;
                            default:
                                Logger.Error("Recevied unknown " + line);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        decodeErrors++;
                        if (!_stop) Logger.Error(ex, "Client exception reading from socket ");

                        if (decodeErrors > MAX_DECODE_ERRORS)
                        {
                            ShowVersionMistmatchWarning("unknown");
                            Disconnect();
                            break;
                        }
                    }

                // do something with line
            }
            catch (Exception ex)
            {
                if (!_stop) Logger.Error(ex, "Client exception reading - Disconnecting ");
            }
        }

        EventBus.Instance.Unsubscribe(this);

        //clear the clients list
        _clients.Clear();

        Disconnect();
    }

    private void HandlePartialUpdate(NetworkMessage networkMessage, SRClientBase client)
    {
        var updatedSrClient = networkMessage.Client;

        client.AllowRecord = updatedSrClient.AllowRecord;
        client.ClientGuid = updatedSrClient.ClientGuid;
        client.Coalition = updatedSrClient.Coalition;
        client.LatLngPosition = updatedSrClient.LatLngPosition;
        client.Name = updatedSrClient.Name;
        client.Seat = updatedSrClient.Seat;
        client.DISEntityId = updatedSrClient.DISEntityId;
    }

    private void HandleFullUpdate(NetworkMessage networkMessage, SRClientBase client)
    {
        HandlePartialUpdate(networkMessage, client);

        if (networkMessage.Client.RadioInfo != null)
            client.RadioInfo = networkMessage.Client.RadioInfo;
    }

    private void ShowVersionMistmatchWarning(string serverVersion)
    {
        //TODO handle this on the client
        /*
        MessageBox.Show($"The SRS server you're connecting to is incompatible with this Client. " +
                        $"\n\nMake sure to always run the latest version of the SRS Server & Client" +
                        $"\nClient Version: {UpdaterChecker.VERSION}",
                        "SRS Server Incompatible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
        */
        EventBus.Instance.PublishOnUIThreadAsync(new InvalidServerVersionMessage(serverVersion));
    }

    private void SendToServer(NetworkMessage message)
    {
        try
        {
            _lastSent = DateTime.Now.Ticks;
            message.Version = UpdaterChecker.VERSION;

            var json = message.Encode();

            if (message.MsgType == NetworkMessage.MessageType.RADIO_UPDATE)
                Logger.Debug("Sending Radio Update To Server: " + json);

            var bytes = Encoding.UTF8.GetBytes(json);
            _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
            //Need to flush?
        }
        catch (Exception ex)
        {
            if (!_stop) Logger.Error(ex, "Client exception sending to server");

            Disconnect();
        }
    }

    //implement IDispose? To close stuff properly?

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Disconnect()
    {
        EventBus.Instance.Unsubscribe(this);

        _stop = true;

        _lastSent = DateTime.Now.Ticks;

        try
        {
            if (_tcpClient != null)
            {
                _tcpClient?.Close(); // this'll stop the socket blocking
                _tcpClient = null;
                EventBus.Instance.PublishOnUIThreadAsync(new TCPClientStatusMessage(false));
            }
        }
        catch (Exception)
        {
        }

        Logger.Error("Disconnecting from server");
    }
}