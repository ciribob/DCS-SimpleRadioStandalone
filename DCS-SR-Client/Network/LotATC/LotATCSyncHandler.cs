using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.LotATC.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using CommunityToolkit.Mvvm.DependencyInjection;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.LotATC
{
    public class LotATCSyncHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly long UPDATE_SYNC_RATE = 5*1000 * 10000; //There are 10,000 ticks in a millisecond, or 10 million ticks in a second. Update every 5 seconds
        private UdpClient _lotATCPositionListener;
        private ClientSettingsModel ClientSettings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>().ClientSettings;
        private ServerSettingsModel ServerSettings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>().CurrentServerSettings;
        private volatile bool _stop = false;
        private readonly ClientStateSingleton _clientStateSingleton;
        private readonly DCSRadioSyncManager.ClientSideUpdate _clientSideUpdate;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private readonly string _guid;
        private long _lastSent = 0;

        private double _heightOffset;

        public LotATCSyncHandler(DCSRadioSyncManager.ClientSideUpdate clientSideUpdate, string guid)
        {
            _clientSideUpdate = clientSideUpdate;
            _guid = guid;
            _clientStateSingleton = ClientStateSingleton.Instance;

            _heightOffset = ClientSettings.LotAtcHeightOffset;
        }

        public void Start()
        {
  
            

            Task.Factory.StartNew(() =>
            {
                while (!_stop)
                {
                    
                    try
                    {
                        var localEp = new IPEndPoint(IPAddress.Any, ClientSettings.LotAtcIncomingUdp);
                        _lotATCPositionListener = new UdpClient(localEp);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Unable to bind to the LotATC Export Listener Socket Port: {ClientSettings.LotAtcIncomingUdp}");
                        Thread.Sleep(500);
                    }
                }
                
                while (!_stop)
                {
                    try
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any,0);
                        var bytes = _lotATCPositionListener.Receive(ref groupEp);

                        var lotAtcPositionWrapper =
                            JsonConvert.DeserializeObject<LotATCMessageWrapper>(Encoding.UTF8.GetString(
                                bytes, 0, bytes.Length));

                        if (lotAtcPositionWrapper != null )
                        {
                                
                            if (lotAtcPositionWrapper.los != null)
                            {
                                HandleLOSResponse(lotAtcPositionWrapper.los);
                            } 
                            else if (lotAtcPositionWrapper.controller !=null)
                            {
                                HandleLotATCUpdate(lotAtcPositionWrapper.controller);
                            }
                        }
                    }
                    catch (SocketException e)
                    {
                        if (!_stop)
                        {
                            Logger.Error(e, "SocketException Handling LotATC UDP Message");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Handling LotATC UDP Message");
                    }
                }

                try
                {
                    _lotATCPositionListener.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stoping LotATC UDP listener");
                }
                
            });

            StartLotATCLOSSender();
        }

        private void HandleLotATCUpdate(LotATCMessageWrapper.LotATCPosition controller)
        {
            _clientStateSingleton.LotATCLastReceived = DateTime.Now.Ticks;

            //only send update if position and line of sight are enabled
            var shouldUpdate = ServerSettings.IsDistanceCheckingEnabled || ServerSettings.IsLineOfSightCheckingEnabled;

            if (_clientStateSingleton.ShouldUseLotATCPosition())
            {
                _clientStateSingleton.UpdatePlayerPosition(new DCSLatLngPosition(){ lat=controller.latitude, lng=controller.longitude, alt=controller.altitude+_heightOffset });
                long diff = DateTime.Now.Ticks - _lastSent;

                if (diff > UPDATE_SYNC_RATE && shouldUpdate) // There are 10,000 ticks in a millisecond, or 10 million ticks in a second. Update ever 5 seconds
                {
                    _lastSent = DateTime.Now.Ticks;
                    _clientSideUpdate();
                }
            }

        }

        private void HandleLOSResponse(LotATCMessageWrapper.LotATCLineOfSightResponse response)
        {
            SRClient client;

            if (_clients.TryGetValue(response.clientId, out client))
            {
                //1 is total loss so if see is false
                //0 is full line of sight so see is true
                client.LineOfSightLoss = response.see ? 0:1;
            }
        }

        private void StartLotATCLOSSender()
        {
            var _udpSocket = new UdpClient();
            var _host = new IPEndPoint(IPAddress.Loopback, ClientSettings.LotAtcOutgoingUdp);


            Task.Factory.StartNew(() =>
            {
                using (_udpSocket)
                {
                    while (!_stop)
                    {
                        try
                        {
                            if (_clientStateSingleton.IsLotATCConnected)
                            {
                                //Chunk client list into blocks of 10 to stay below 8000 ish UDP socket limit
                                var clientsList = GenerateDcsLosCheckRequests();

                                if (clientsList.Count > 0)
                                {
                                    foreach (var losRequest in clientsList)
                                    {
                                        var byteData =
                                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(losRequest) + "\n");

                                        _udpSocket.Send(byteData, byteData.Length, _host);

                                        //every 50ms - Wait for the queue
                                        Thread.Sleep(50);
                                    }
                                }
                            }
                           
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Sending LotATC LOS Request Message");
                        }
                        //every 2000 - Wait for the queue
                        Thread.Sleep(2000);
                    }

                    try
                    {
                        _udpSocket.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping LotATC LOS Sender ");
                    }
                }
            });
        }

        private List<LotATCLineOfSightRequest> GenerateDcsLosCheckRequests()
        {
            var clients = _clients.Values.ToList();

            var requests = new List<LotATCLineOfSightRequest>();

            if (_clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition != null
                && _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.isValid()
                && _clientStateSingleton.ShouldUseLotATCPosition() 
                && ServerSettings.IsLineOfSightCheckingEnabled)
            {
                foreach (var client in clients)
                {
                    //only check if its worth it
                    if ((client.LatLngPosition != null
                         && client.LatLngPosition.isValid()) 
                        && (client.ClientGuid != _guid)
                        )
                    {
                        requests.Add(new LotATCLineOfSightRequest()
                        {
                            lat1 = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.lat,
                            long1 = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.lng,
                            alt1 = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition.alt,

                            lat2 = client.LatLngPosition.lat,
                            long2 = client.LatLngPosition.lng,
                            alt2 = client.LatLngPosition.alt,

                            clientId = client.ClientGuid
                        });
                    }
                }
            }

            return requests;
        }

        public void Stop()
        {
            _stop = true;
            try
            {
                _lotATCPositionListener?.Close();
            }
            catch (Exception)
            {
                //IGNORE
            }
        }
    }
}
