using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.LotATC;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Easy.MessageHub;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class SRSClientSyncHandler
    {
        public delegate void ConnectCallback(bool result, bool connectionError, string connection);
        public delegate void ExternalAWACSModeConnectCallback(bool result, int coalition);
        public delegate void UpdateUICallback();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private volatile bool _stop = false;

        public static string ServerVersion = "Unknown";
        private readonly ConcurrentDictionary<string, SRClient> _clients;
        private readonly string _guid;
        private ConnectCallback _callback;
        private ExternalAWACSModeConnectCallback _externalAWACSModeCallback;
        private UpdateUICallback _updateUICallback;
        private readonly DCSRadioSyncHandler.NewAircraft _newAircraft;
        private IPEndPoint _serverEndpoint;
        private TcpClient _tcpClient;

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private DCSRadioSyncManager _radioDCSSync = null;
        private LotATCSyncHandler _lotATCSync;

        private static readonly int MAX_DECODE_ERRORS = 5;
        private VAICOMSyncHandler _vaicomSync;


        public SRSClientSyncHandler(ConcurrentDictionary<string, SRClient> clients, string guid, UpdateUICallback uiCallback, DCSRadioSyncHandler.NewAircraft _newAircraft)
        {
            _clients = clients;
            _guid = guid;
            _updateUICallback = uiCallback;
            this._newAircraft = _newAircraft;
        }


        public void TryConnect(IPEndPoint endpoint, ConnectCallback callback)
        {
            _callback = callback;
            _serverEndpoint = endpoint;

            var tcpThread = new Thread(Connect);
            tcpThread.Start();
        }

        public void ConnectExternalAWACSMode(string password, ExternalAWACSModeConnectCallback callback)
        {
            if (_clientStateSingleton.InExternalAWACSMode)
            {
                return;
            }

            _externalAWACSModeCallback = callback;

            var sideInfo = _clientStateSingleton.PlayerCoaltionLocationMetadata;
            sideInfo.name = _clientStateSingleton.LastSeenName;
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    LatLngPosition = sideInfo.LngLngPosition,
                    ClientGuid = _guid
                },
                ExternalAWACSModePassword = password,
                MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD
            });
        }

        public void DisconnectExternalAWACSMode()
        {
            if (!_clientStateSingleton.InExternalAWACSMode || _radioDCSSync == null)
            {
                return;
            }

            if (_tcpClient != null && _tcpClient.Connected)
            {
                SendToServer(new NetworkMessage
                {
                    Client = new SRClient
                    {
                        Coalition = 0,
                        Name = "",
                        LatLngPosition = new DCSLatLngPosition(){lat=0,lng=0,alt=0},
                        ClientGuid = _guid
                    },
                    MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_DISCONNECT
                });
            }

            _radioDCSSync.StopExternalAWACSModeLoop();

            CallExternalAWACSModeOnMain(false, 0);
        }

        private void Connect()
        {
            if (_radioDCSSync != null)
            {
                _radioDCSSync.Stop();
                _radioDCSSync = null;
            }
            if (_lotATCSync != null)
            {
                _lotATCSync.Stop();
                _lotATCSync = null;
            }
            if (_vaicomSync != null)
            {
                _vaicomSync.Stop();
                _vaicomSync = null;
            }

            bool connectionError = false;

            _radioDCSSync = new DCSRadioSyncManager(ClientRadioUpdated, ClientCoalitionUpdate, _clients, _guid,_newAircraft);
            _lotATCSync = new LotATCSyncHandler(ClientCoalitionUpdate,_clients, _guid);
            _vaicomSync = new VAICOMSyncHandler();

            using (_tcpClient = new TcpClient())
            {
                try
                {
                    _tcpClient.SendTimeout = 10000;
                    _tcpClient.NoDelay = true;

                    // Wait for 10 seconds before aborting connection attempt - no SRS server running/port opened in that case
                    _tcpClient.ConnectAsync(_serverEndpoint.Address, _serverEndpoint.Port).Wait(TimeSpan.FromSeconds(10));

                    if (_tcpClient.Connected)
                    {
                        _radioDCSSync.Start();
                        _lotATCSync.Start();
                        _vaicomSync.Start();

                        _tcpClient.NoDelay = true;

                        CallOnMain(true);
                        ClientSyncLoop();
                    }
                    else
                    {
                        Logger.Error($"Failed to connect to server @ {_serverEndpoint.ToString()}");

                        // Signal disconnect including an error
                        connectionError = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Could not connect to server");
                    connectionError = true;
                }
            }

            _radioDCSSync.Stop();
            _lotATCSync.Stop();
            _vaicomSync.Stop();

            //disconnect callback
            CallOnMain(false, connectionError);
        }

        private void ClientRadioUpdated()
        {
            Logger.Debug("Sending Radio Update to Server");
            var sideInfo = _clientStateSingleton.PlayerCoaltionLocationMetadata;
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    ClientGuid = _guid,
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                    LatLngPosition = sideInfo.LngLngPosition
                },
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE
            });
        }

        private void ClientCoalitionUpdate()
        {
            var sideInfo = _clientStateSingleton.PlayerCoaltionLocationMetadata;
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    LatLngPosition = sideInfo.LngLngPosition,
                    ClientGuid = _guid
                },
                MsgType = NetworkMessage.MessageType.UPDATE
            });
        }

        private void CallOnMain(bool result, bool connectionError = false)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new ThreadStart(delegate { _callback(result, connectionError, _serverEndpoint.ToString()); }));
            }
            catch (Exception ex)
            {
                 Logger.Error(ex, "Failed to update UI after connection callback (result {result}, connectionError {connectionError})", result, connectionError);
            }
        }

        private void CallExternalAWACSModeOnMain(bool result, int coalition)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new ThreadStart(delegate { _externalAWACSModeCallback(result, coalition); }));
            }
            catch (Exception ex)
            {
            }
        }

        private void CallUpdateUIOnMain()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new ThreadStart(delegate { _updateUICallback(); }));
            }
            catch (Exception ex)
            {
            }
        }

        private void ClientSyncLoop()
        {
            //clear the clients list
            _clients.Clear();
            int decodeErrors = 0; //if the JSON is unreadable - new version likely

            using (var reader = new StreamReader(_tcpClient.GetStream(), Encoding.UTF8))
            {
                try
                {
                    var sideInfo = _clientStateSingleton.PlayerCoaltionLocationMetadata;
                    //start the loop off by sending a SYNC Request
                    SendToServer(new NetworkMessage
                    {
                        Client = new SRClient
                        {
                            Coalition = sideInfo.side,
                            Name = sideInfo.name.Length > 0 ? sideInfo.name : _clientStateSingleton.LastSeenName,
                            LatLngPosition = sideInfo.LngLngPosition,
                            ClientGuid = _guid
                        },
                        MsgType = NetworkMessage.MessageType.SYNC,
                    });

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var serverMessage = JsonConvert.DeserializeObject<NetworkMessage>(line);
                            decodeErrors = 0; //reset counter
                            if (serverMessage != null)
                            {
                                switch (serverMessage.MsgType)
                                {
                                    case NetworkMessage.MessageType.PING:
                                        // Do nothing for now
                                        break;
                                    case NetworkMessage.MessageType.RADIO_UPDATE:
                                    case NetworkMessage.MessageType.UPDATE:

                                        _serverSettings.Decode(serverMessage.ServerSettings);

                                        if (_clients.ContainsKey(serverMessage.Client.ClientGuid))
                                        {
                                            var srClient = _clients[serverMessage.Client.ClientGuid];
                                            var updatedSrClient = serverMessage.Client;
                                            if (srClient != null)
                                            {
                                                srClient.LastUpdate = DateTime.Now.Ticks;
                                                srClient.Name = updatedSrClient.Name;
                                                srClient.Coalition = updatedSrClient.Coalition;

                                                srClient.LatLngPosition = updatedSrClient.LatLngPosition;

                                                if (updatedSrClient.RadioInfo != null)
                                                {
                                                    srClient.RadioInfo = updatedSrClient.RadioInfo;
                                                    srClient.RadioInfo.LastUpdate = DateTime.Now.Ticks;
                                                }

//                                                Logger.Info("Recevied Update Client: " + NetworkMessage.MessageType.UPDATE + " From: " +
//                                                            srClient.Name + " Coalition: " +
//                                                            srClient.Coalition + " Pos: " + srClient.Position);
                                            }
                                        }
                                        else
                                        {
                                            var connectedClient = serverMessage.Client;
                                            connectedClient.LastUpdate = DateTime.Now.Ticks;

                                            //init with LOS true so you can hear them incase of bad DCS install where
                                            //LOS isnt working
                                            connectedClient.LineOfSightLoss = 0.0f;
                                            //0.0 is NO LOSS therefore full Line of sight

                                            _clients[serverMessage.Client.ClientGuid] = connectedClient;

//                                            Logger.Info("Recevied New Client: " + NetworkMessage.MessageType.UPDATE +
//                                                        " From: " +
//                                                        serverMessage.Client.Name + " Coalition: " +
//                                                        serverMessage.Client.Coalition);
                                        }

                                        if (_clientStateSingleton.InExternalAWACSMode &&
                                            !_serverSettings.GetSettingAsBool(Common.Setting.ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                        {
                                            DisconnectExternalAWACSMode();
                                        }

                                        CallUpdateUIOnMain();

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

                                        ServerVersion = serverMessage.Version;

                                        if (serverVersion < protocolVersion)
                                        {
                                            Logger.Error($"Server version ({serverMessage.Version}) older than minimum procotol version ({UpdaterChecker.MINIMUM_PROTOCOL_VERSION}) - disconnecting");

                                            ShowVersionMistmatchWarning(serverMessage.Version);

                                            Disconnect();
                                            break;
                                        }

                                        if (serverMessage.Clients != null)
                                        {
                                            foreach (var client in serverMessage.Clients)
                                            {
                                                client.LastUpdate = DateTime.Now.Ticks;
                                                //init with LOS true so you can hear them incase of bad DCS install where
                                                //LOS isnt working
                                                client.LineOfSightLoss = 0.0f;
                                                //0.0 is NO LOSS therefore full Line of sight
                                                _clients[client.ClientGuid] = client;
                                            }
                                        }
                                        //add server settings
                                        _serverSettings.Decode(serverMessage.ServerSettings);

                                        if (_clientStateSingleton.InExternalAWACSMode &&
                                            !_serverSettings.GetSettingAsBool(Common.Setting.ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                        {
                                            DisconnectExternalAWACSMode();
                                        }

                                        CallUpdateUIOnMain();

                                        break;

                                    case NetworkMessage.MessageType.SERVER_SETTINGS:

                                        //  Logger.Info("Recevied: " + NetworkMessage.MessageType.SERVER_SETTINGS);
                                        _serverSettings.Decode(serverMessage.ServerSettings);
                                        ServerVersion = serverMessage.Version;

                                        if (_clientStateSingleton.InExternalAWACSMode &&
                                            !_serverSettings.GetSettingAsBool(Common.Setting.ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                        {
                                            DisconnectExternalAWACSMode();
                                        }

                                        CallUpdateUIOnMain();

                                        break;
                                    case NetworkMessage.MessageType.CLIENT_DISCONNECT:
                                        //   Logger.Info("Recevied: " + NetworkMessage.MessageType.CLIENT_DISCONNECT);

                                        SRClient outClient;
                                        _clients.TryRemove(serverMessage.Client.ClientGuid, out outClient);

                                        if (outClient != null)
                                        {
                                            MessageHub.Instance.Publish(outClient);
                                        }

                                        break;
                                    case NetworkMessage.MessageType.VERSION_MISMATCH:
                                        Logger.Error($"Version Mismatch Between Client ({UpdaterChecker.VERSION}) & Server ({serverMessage.Version}) - Disconnecting");

                                        ShowVersionMistmatchWarning(serverMessage.Version);

                                        Disconnect();
                                        break;
                                    case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:
                                        if (serverMessage.Client.Coalition == 0)
                                        {
                                            Logger.Info("External AWACS mode authentication failed");

                                            CallExternalAWACSModeOnMain(false, 0);
                                        }
                                        else if (_radioDCSSync != null && _radioDCSSync.IsListening)
                                        {
                                            Logger.Info("External AWACS mode authentication succeeded, coalition {0}", serverMessage.Client.Coalition == 1 ? "red" : "blue");

                                            CallExternalAWACSModeOnMain(true, serverMessage.Client.Coalition);

                                            _radioDCSSync.StartExternalAWACSModeLoop();
                                        }
                                        break;
                                    default:
                                        Logger.Error("Recevied unknown " + line);
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            decodeErrors++;
                            if (!_stop)
                            {
                                Logger.Error(ex, "Client exception reading from socket ");
                            }

                            if (decodeErrors > MAX_DECODE_ERRORS)
                            {
                                ShowVersionMistmatchWarning("unknown");
                                Disconnect();
                                break;
                            }
                        }

                        // do something with line
                    }
                }
                catch (Exception ex)
                {
                    if (!_stop)
                    {
                        Logger.Error(ex, "Client exception reading - Disconnecting ");
                    }
                }
            }

            //disconnected - reset DCS Info
            ClientStateSingleton.Instance.DcsPlayerRadioInfo.LastUpdate = 0;

            //clear the clients list
            _clients.Clear();
        }

        private void ShowVersionMistmatchWarning(string serverVersion)
        {
            MessageBox.Show($"The SRS server you're connecting to is incompatible with this Client. " +
                            $"\n\nMake sure to always run the latest version of the SRS Server & Client" +
                            $"\n\nServer Version: {serverVersion}" +
                            $"\nClient Version: {UpdaterChecker.VERSION}" +
                            $"\nMinimum Version: {UpdaterChecker.MINIMUM_PROTOCOL_VERSION}",
                            "SRS Server Incompatible",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }

        private void SendToServer(NetworkMessage message)
        {
            try
            {

                message.Version = UpdaterChecker.VERSION;

                var json = message.Encode();

                if (message.MsgType == NetworkMessage.MessageType.RADIO_UPDATE)
                {
                    Logger.Debug("Sending Radio Update To Server: "+ (json));
                }

                var bytes = Encoding.UTF8.GetBytes(json);
                _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
                //Need to flush?
            }
            catch (Exception ex)
            {
                if (!_stop)
                {
                    Logger.Error(ex, "Client exception sending to server");
                }

                Disconnect();
            }
        }

        //implement IDispose? To close stuff properly?
        public void Disconnect()
        {
            _stop = true;

            DisconnectExternalAWACSMode();

            try
            {
                if (_tcpClient != null)
                {
                    _tcpClient.Close(); // this'll stop the socket blocking
                }
            }
            catch (Exception ex)
            {
            }

            Logger.Error("Disconnecting from server");

            //CallOnMain(false);
        }
    }
}