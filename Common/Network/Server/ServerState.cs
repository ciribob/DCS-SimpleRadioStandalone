using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class ServerState : IHandle<StartServerMessage>, IHandle<StopServerMessage>, IHandle<KickClientMessage>,
    IHandle<BanClientMessage>
{
    private static readonly string DEFAULT_CLIENT_EXPORT_FILE = "clients-list.json";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly HashSet<IPAddress> _bannedIps = new();

    private readonly ConcurrentDictionary<string, SRClientBase> _connectedClients =
        new();

    private readonly IEventAggregator _eventAggregator;
    private UDPVoiceRouter _serverListener;
    private ServerSync _serverSync;
    private volatile bool _stop = true;
    private HttpServer _httpServer;

    public ServerState(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnPublishedThread(this);

        StartServer();
    }


    public async Task HandleAsync(BanClientMessage message, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            WriteBanIP(message.Client);
            KickClient(message.Client);
        });
    }

    public async Task HandleAsync(KickClientMessage message, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var client = message.Client;
            KickClient(client);
        });
    }

    public async Task HandleAsync(StartServerMessage message, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            StartServer();
            _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(true,
                new List<SRClientBase>(_connectedClients.Values)));
        });
    }

    public async Task HandleAsync(StopServerMessage message, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            StopServer();
            _eventAggregator.PublishOnUIThreadAsync(new ServerStateMessage(false,
                new List<SRClientBase>(_connectedClients.Values)));
        });
    }


    private static string GetCurrentDirectory()
    {
        //To get the location the assembly normally resides on disk or the install directory
        var currentPath = AppContext.BaseDirectory;

        //once you have the path you get the directory with:
        var currentDirectory = Path.GetDirectoryName(currentPath);

        if (currentDirectory.StartsWith("file:\\")) currentDirectory = currentDirectory.Replace("file:\\", "");

        return currentDirectory;
    }

    private void StartServer()
    {
        if (_serverListener == null)
        {
            PopulateBanList();
            _stop = false;
            _serverListener = new UDPVoiceRouter(_connectedClients, _eventAggregator);
            Task.Run(_serverListener.Listen);

            _serverSync = new ServerSync(_connectedClients, _bannedIps, _eventAggregator);
            var serverSyncThread = new Thread(_serverSync.StartListening);
            serverSyncThread.Start();

            StartExport();

            StartHttpServer();
        }
    }

    private void StartHttpServer()
    {
        _httpServer = new HttpServer(_connectedClients, this);
        _httpServer.Start();
    }

    public void StopServer()
    {
        if (_serverListener != null)
        {
            _stop = true;
            _serverSync.RequestStop();
            _serverSync = null;
            _serverListener.RequestStop();
            _serverListener = null;
            _httpServer?.Stop();
        }
    }

    private void StartExport()
    {
        _stop = false;

        var exportFilePath = ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH)
            .StringValue;
        if (string.IsNullOrWhiteSpace(exportFilePath) || exportFilePath == DEFAULT_CLIENT_EXPORT_FILE)
            // Make sure we're using a full file path in case we're falling back to default values
            exportFilePath = Path.Combine(GetCurrentDirectory(), DEFAULT_CLIENT_EXPORT_FILE);
        else
            // Normalize file path read from config to ensure properly escaped local path
            exportFilePath = NormalizePath(exportFilePath);

        var exportFileDirectory = Path.GetDirectoryName(exportFilePath);

        if (!Directory.Exists(exportFileDirectory))
        {
            Logger.Warn($"Client export directory \"{exportFileDirectory}\" does not exist, trying to create it");

            try
            {
                Directory.CreateDirectory(exportFileDirectory);
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    $"Failed to create client export directory \"{exportFileDirectory}\", falling back to default path");

                // Failed to create desired client export directory, fall back to default path in current application directory
                exportFilePath = NormalizePath(Path.Combine(GetCurrentDirectory(), DEFAULT_CLIENT_EXPORT_FILE));
            }
        }

        Task.Run(async () =>
        {
            while (!_stop)
            {
                if (ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED).BoolValue)
                {
                    var data = new ClientListExport
                        { Clients = _connectedClients.Values, ServerVersion = UpdaterChecker.VERSION };
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions()
                    {
                        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                        {
                            Modifiers = { JsonNetworkPropertiesResolver.StripNetworkIgnored }
                        },
                        IncludeFields = true,
                    }) + "\n";
                    try
                    {
                        await File.WriteAllTextAsync(exportFilePath, json);
                    }
                    catch (IOException e)
                    {
                        Logger.Error(e);
                    }
                }

                await Task.Delay(5000);
            }
        });

        Task.Run(async () =>
        {
            using (var udpSocket = new UdpClient())
            {
                while (!_stop)
                {
                    try
                    {
                        if (ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED)
                            .BoolValue)
                        {
                            var host = new IPEndPoint(
                                IPAddress.Parse(ServerSettingsStore.Instance
                                    .GetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_IP).StringValue),
                                ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT)
                                    .IntValue);

                            var data = new ClientListExport
                                { ServerVersion = UpdaterChecker.VERSION, Clients = new List<SRClientBase>() };

                            var firstSeatDict = new Dictionary<uint, SRClientBase>();
                            var secondSeatDict = new Dictionary<uint, SRClientBase>();

                            foreach (var srClient in _connectedClients.Values)
                                if (srClient.RadioInfo?.iff != null)
                                {
                                    var newClient = new SRClientBase
                                    {
                                        ClientGuid = srClient.ClientGuid,
                                        RadioInfo = new PlayerRadioInfoBase
                                        {
                                            radios = null,
                                            unitId = srClient.RadioInfo.unitId,
                                            iff = srClient.RadioInfo.iff.Copy(),
                                            unit = srClient.RadioInfo.unit
                                        },
                                        Coalition = srClient.Coalition,
                                        Name = srClient.Name,
                                        LatLngPosition = srClient?.LatLngPosition,
                                        Seat = srClient.Seat,
                                        AllowRecord = srClient.AllowRecord,
                                        Gateway = srClient.Gateway,
                                        DISEntityId = srClient.DISEntityId
                                    };

                                    //reset and hide anything if the IFF is off
                                    if (newClient.RadioInfo.iff.status == Transponder.IFFStatus.OFF)
                                        newClient.RadioInfo.iff = new Transponder();

                                    data.Clients.Add(newClient);

                                    //will need to be expanded as more aircraft have more seats and transponder controls
                                    if (newClient.Seat == 0)
                                        firstSeatDict[newClient.RadioInfo.unitId] = newClient;
                                    else
                                        secondSeatDict[newClient.RadioInfo.unitId] = newClient;
                                }

                            //now look for other seats and handle the logic
                            foreach (var secondSeatPair in secondSeatDict)
                            {
                                SRClientBase firstSeat = null;

                                firstSeatDict.TryGetValue(secondSeatPair.Key, out firstSeat);
                                //copy second seat 
                                if (firstSeat != null)
                                {
                                    //F-14 has RIO IFF Control so use the second seat IFF
                                    if (firstSeat.RadioInfo.unit.StartsWith("F-14"))
                                        //copy second to first
                                        firstSeat.RadioInfo.iff = secondSeatPair.Value.RadioInfo.iff;
                                    else
                                        //copy first to second
                                        secondSeatPair.Value.RadioInfo.iff = firstSeat.RadioInfo.iff;
                                }
                            }

                            var byteData =
                                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data,
                                    new JsonSerializerOptions()
                                    {
                                        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
                                        {
                                            Modifiers = { JsonNetworkPropertiesResolver.StripNetworkIgnored }
                                        },
                                        IncludeFields = true,
                                    }) + "\n");

                            await udpSocket.SendAsync(byteData, byteData.Length, host);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending LotATC Client Info");
                    }

                    //every 2s
                    await Task.Delay(2000);
                }

                try
                {
                    udpSocket.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stoping LotATC Client Info");
                }
            }
        });
    }

    private void PopulateBanList()
    {
        try
        {
            _bannedIps.Clear();

            var path = Path.Combine(GetCurrentDirectory(), "banned.txt");
            if (!File.Exists(path))
            {
                Logger.Info($"'{path}' was not found or you don't have permission to read the file");
                return;
            }

            foreach (var line in File.ReadAllLines(path))
                if (IPAddress.TryParse(line.Trim(), out var ip))
                {
                    Logger.Info($"Loaded Banned IP: {line}");
                    _bannedIps.Add(ip);
                }
        }
        catch (Exception)
        {
            Logger.Error("Unable to read banned.txt");
        }
    }


    private static string NormalizePath(string path)
    {
        // Taken from https://stackoverflow.com/a/21058121 on 2018-06-22
        return Path.GetFullPath(new Uri(path).LocalPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }


    public void KickClient(SRClientBase client)
    {
        if (client != null)
            try
            {
                _serverSync.FindSession(client.ClientSession)?.Disconnect();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error kicking client");
            }
    }

    public void WriteBanIP(SRClientBase client)
    {
        try
        {
            var remoteIpEndPoint = _serverSync.FindSession(client.ClientSession)?.Socket.RemoteEndPoint as IPEndPoint;

            if (remoteIpEndPoint == null) return;

            _bannedIps.Add(remoteIpEndPoint.Address);

            File.AppendAllText(GetCurrentDirectory() + Path.DirectorySeparatorChar + "banned.txt",
                remoteIpEndPoint.Address + "\r\n");
            
            KickClient(client);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving banned client");
        }
    }
}