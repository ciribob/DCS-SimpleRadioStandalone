using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Newtonsoft.Json;
using NLog;

/**
Keeps radio information in Sync Between DCS and 

**/

namespace Ciribob.DCS.SimpleRadio.Standalone.Server
{
    public class RadioDCSSyncServer
    {
        public delegate void ClientSideUpdate();

        public delegate void SendRadioUpdate();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static volatile DCSPlayerRadioInfo DcsPlayerRadioInfo = new DCSPlayerRadioInfo();

        public static volatile DCSPlayerSideInfo DcsPlayerSideInfo = new DCSPlayerSideInfo();

        private volatile bool _stop;

        private readonly SendRadioUpdate _clientRadioUpdate;
        private readonly ClientSideUpdate _clientSideUpdate;

        private UdpClient _dcsGameGuiudpListener;
        private UdpClient _dcsUdpListener;

        private UdpClient _dcsLOSListener;
        private ConcurrentDictionary<string, SRClient> _clients;
        private string _guid;

        public static long LastSent { get; set; }

        public RadioDCSSyncServer(SendRadioUpdate clientRadioUpdate, ClientSideUpdate clientSideUpdate, ConcurrentDictionary<string, SRClient> _clients,String guid)
        {
            this._clientRadioUpdate = clientRadioUpdate;
            this._clientSideUpdate = clientSideUpdate;
            this._clients = _clients;
            this._guid = guid;
        }

       
        public void Listen()
        {
            DcsListener();
        }

        private void DcsListener()
        {
            StartDcsBroadcastListener();
            StartDcsGameGuiBroadcastListener();
            StartDCSLOSBroadcastListener();
            StartDCSLOSSender();
        }

        private void StartDcsBroadcastListener()
        {
            _dcsUdpListener = new UdpClient();
            _dcsUdpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _dcsUdpListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

         //   var multicastaddress = IPAddress.Parse("239.255.50.10");
      //      _dcsUdpListener.JoinMulticastGroup(multicastaddress);

            var localEp = new IPEndPoint(IPAddress.Any, 9084);
            _dcsUdpListener.Client.Bind(localEp);
            //   activeRadioUdpClient.Client.ReceiveTimeout = 10000;

            //reset last sent
            LastSent = 0;

            Task.Factory.StartNew(() =>
            {
                using (_dcsUdpListener)
                {
                    while (!_stop)
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any, 9084);
                        var bytes = _dcsUdpListener.Receive(ref groupEp);

                        try
                        {
                            var message =
                                JsonConvert.DeserializeObject<DCSPlayerRadioInfo>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                          //  Logger.Info("Recevied Message from DCS: "+ Encoding.UTF8.GetString(
                          //          bytes, 0, bytes.Length));

                            //sync with others
                            //Radio info is marked as Stale for FC3 aircraft after every frequency change

                            if (UpdateRadio(message) || IsRadioInfoStale(message))
                            {
                                
                                LastSent = Environment.TickCount;
                                _clientRadioUpdate();
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS  Message");
                        }
                    }

                    try
                    {
                        _dcsUdpListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        private void StartDcsGameGuiBroadcastListener()
        {
            _dcsGameGuiudpListener = new UdpClient();
            _dcsGameGuiudpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            _dcsGameGuiudpListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

        //    var multicastaddress = IPAddress.Parse("239.255.50.10");
         //   _dcsGameGuiudpListener.JoinMulticastGroup(multicastaddress);

            var localEp = new IPEndPoint(IPAddress.Any, 5068);
            _dcsGameGuiudpListener.Client.Bind(localEp);
            //   activeRadioUdpClient.Client.ReceiveTimeout = 10000;

            Task.Factory.StartNew(() =>
            {
                using (_dcsGameGuiudpListener)
                {
                //    var count = 0;
                    while (!_stop)
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any, 5068);
                        var bytes = _dcsGameGuiudpListener.Receive(ref groupEp);

                        try
                        {
                            var playerInfo =
                                JsonConvert.DeserializeObject<DCSPlayerSideInfo>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            if (playerInfo != null)
                            {
                                //update position
                                playerInfo.Position = DcsPlayerRadioInfo.pos;
                                DcsPlayerSideInfo = playerInfo;
                                _clientSideUpdate();
                                //     count = 0;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS GameGUI Message");
                        }
                    }

                    try
                    {
                        _dcsGameGuiudpListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        //used for the result
        private void StartDCSLOSBroadcastListener()
        {
            _dcsLOSListener = new UdpClient();
            _dcsLOSListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            _dcsLOSListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            var localEp = new IPEndPoint(IPAddress.Any, 9085);
            _dcsLOSListener.Client.Bind(localEp);

            Task.Factory.StartNew(() =>
            {
                using (_dcsLOSListener)
                {
                    //    var count = 0;
                    while (!_stop)
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any, 9085);
                        var bytes = _dcsLOSListener.Receive(ref groupEp);

                        try
                        {
                         /*   Logger.Debug(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));*/
                            var playerInfo =
                                JsonConvert.DeserializeObject<DCSLosCheckResult[]>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            foreach (var player in playerInfo)
                            {
                               
                                SRClient client;
                                
                                if (_clients.TryGetValue(player.id, out client))
                                {
                                    client.HasLineOfSight = player.los;

                                   // Logger.Debug(client.ToString());
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS Los Result Message");
                        }
                    }

                    try
                    {
                        _dcsLOSListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS LOS Result listener ");
                    }
                }
            });
        }

        private void StartDCSLOSSender()
        {
            var _udpSocket = new UdpClient();
            var _host = new IPEndPoint(IPAddress.Loopback, 9086);


            Task.Factory.StartNew(() =>
            {
                using (_dcsLOSListener)
                {
                    while (!_stop)
                    {
                        try
                        {
                            //Chunk client list into blocks of 10 to stay below 8000 ish UDP socket limit
                            var clientsList = GenerateDcsLosCheckRequests();

                            if (clientsList.Count > 0)
                            {
                                var splitList =  clientsList.ChunkBy(10);
                                foreach (var clientSubList in splitList)
                                {
                                   // Logger.Info( "Sending LOS Request: "+ JsonConvert.SerializeObject(clientSubList));
                                    var byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(clientSubList) + "\n");
                                    
                                    _udpSocket.Send(byteData,byteData.Length, _host);

                                    //every 250 - Wait for the queue
                                    Thread.Sleep(250);
                                }
                            }
                           
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Sending DCS LOS Request Message");
                        }
                        //every 300 - Wait for the queue
                        Thread.Sleep(250);
                    }

                    try
                    {
                        _dcsLOSListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        private List<DCSLosCheckRequest> GenerateDcsLosCheckRequests()
        {
            var clients = _clients.Values.ToList();

            var requests = new List<DCSLosCheckRequest>();

            if (ClientSync.ServerSettings[(int) ServerSettingType.LOS_ENABLED] != null &&
                ClientSync.ServerSettings[(int) ServerSettingType.LOS_ENABLED] == "ON")
            {
                foreach (var client in clients)
                {
                    //only check if its worth it
                    if (client.Position.x != 0 && client.Position.z != 0 && client.ClientGuid != _guid)
                    {
                        requests.Add(new DCSLosCheckRequest()
                        {
                            id = client.ClientGuid,
                            x = client.Position.x,
                            y = client.Position.y,
                            z = client.Position.z
                        });
                    }
                }
            }
            return requests;
        }
       
        private bool UpdateRadio(DCSPlayerRadioInfo message)
        {

            bool changed = false;
            if (message.radioType == DCSPlayerRadioInfo.AircraftRadioType.FULL_COCKPIT_INTEGRATION)
                // Full radio, all from DCS
            {
                changed = !DcsPlayerRadioInfo.Equals(message);

                HandleEncryptionSettingsFullFidelity(message);

                DcsPlayerRadioInfo = message;
            }
            else if (message.radioType == DCSPlayerRadioInfo.AircraftRadioType.PARTIAL_COCKPIT_INTEGRATION)
                // Partial radio - can select radio but the rest is from DCS
            {
                //check if its changed frequency wise
                changed = !DcsPlayerRadioInfo.Equals(message);

                //update common parts
                DcsPlayerRadioInfo.name = message.name;
                DcsPlayerRadioInfo.radioType = message.radioType;
                DcsPlayerRadioInfo.unit = message.unit;
                DcsPlayerRadioInfo.unitId = message.unitId;
                DcsPlayerRadioInfo.pos = message.pos;

                HandleEncryptionSettingsFullFidelity(message);

                //copy over the radios
                DcsPlayerRadioInfo.radios = message.radios;

                //change PTT last
                DcsPlayerRadioInfo.ptt = message.ptt;

            }
            else // FC3 Radio - Take nothing from DCS, just update the last tickcount, UPDATE triggered a different way
            {
                if (DcsPlayerRadioInfo.unitId != message.unitId)
                {
                    //replace it all - new aircraft
                    DcsPlayerRadioInfo = message;
                    changed = true;
                }
                else // same aircraft
                {
                    //update common parts
                    DcsPlayerRadioInfo.name = message.name;
                    DcsPlayerRadioInfo.radioType = message.radioType;
                    DcsPlayerRadioInfo.unit = message.unit;
                    DcsPlayerRadioInfo.pos = message.pos;

                    DcsPlayerRadioInfo.unitId = message.unitId;

                    //copy over radio names, min + max
                    for (var i = 0; i < DcsPlayerRadioInfo.radios.Length; i++)
                    {
                        var updateRadio = message.radios[i];

                        var clientRadio = DcsPlayerRadioInfo.radios[i];

                        clientRadio.freqMin = updateRadio.freqMin;
                        clientRadio.freqMax = updateRadio.freqMax;
                                             
                        clientRadio.name = updateRadio.name;

                        if (clientRadio.secondaryFrequency == 0)
                        {
                            //currently turned off
                            clientRadio.secondaryFrequency = 0;

                        }
                        else
                        {
                            //put back
                            clientRadio.secondaryFrequency = updateRadio.secondaryFrequency;
                        }

                        clientRadio.modulation = updateRadio.modulation;

                        //check we're not over a limit

                        if (clientRadio.frequency > clientRadio.freqMax)
                        {
                            clientRadio.frequency = clientRadio.freqMax;
                        }
                        else if (clientRadio.frequency < clientRadio.freqMin)
                        {
                            clientRadio.frequency = clientRadio.freqMin;
                        }

                        clientRadio.encMode = updateRadio.encMode;

                        //Handle Encryption
                        if (updateRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                        {
                            if (clientRadio.encKey == 0)
                            {
                                clientRadio.encKey = 1;
                            }
                        }

                    }

                    //change PTT last
                    DcsPlayerRadioInfo.ptt = message.ptt;
                }
               
            }

            //update
            DcsPlayerRadioInfo.LastUpdate = Environment.TickCount;

            return changed;
        }

        private void HandleEncryptionSettingsFullFidelity(DCSPlayerRadioInfo radioUpdate)
        {
            // handle encryption type
            for (int i = 0; i < radioUpdate.radios.Length; i++)
            {
                var updatedRadio = radioUpdate.radios[i];
                var currentRadio = DcsPlayerRadioInfo.radios[i];

                if (updatedRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                {
                    if (currentRadio.encKey != 0)
                    {
                        updatedRadio.encKey = currentRadio.encKey;
                    }
                }
               
            }
        }

        private bool IsRadioInfoStale(DCSPlayerRadioInfo radioUpdate)
        {
            //send update if our metadata is nearly stale
            if (Environment.TickCount - LastSent < 7000)
            {
                return false;
            }

            return true;
        }

        public void Stop()
        {
            _stop = true;

            try
            {
                _dcsUdpListener.Close();
            }
            catch (Exception ex)
            {
            }
            
            try
            {
                _dcsGameGuiudpListener.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsLOSListener.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}