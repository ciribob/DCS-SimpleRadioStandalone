using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server
{
    internal class UDPVoiceRouter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, SRClient> _clientsList;
        private readonly IEventAggregator _eventAggregator;
        private UdpClient _listener;

        private volatile bool _stop;
        private ServerSettings _serverSettings = ServerSettings.Instance;

        public UDPVoiceRouter(ConcurrentDictionary<string, SRClient> clientsList, IEventAggregator eventAggregator)
        {
            _clientsList = clientsList;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        public void Listen()
        {
            _listener = new UdpClient();
            _listener.AllowNatTraversal(true);
            _listener.ExclusiveAddressUse = true;
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, 5010));
            while (!_stop)
            {
                try
                {
                    var groupEP = new IPEndPoint(IPAddress.Any, 5010);
                    var rawBytes = _listener.Receive(ref groupEP);
                    if (rawBytes!=null && rawBytes.Length >= 22)
                    {
                        //WRAP IN REAL THREAD
     //                   Task.Run(() =>
         //               {
                            //last 36 bytes are guid!
                        var guid = Encoding.ASCII.GetString(
                            rawBytes, rawBytes.Length - 22, 22);

                        if (_clientsList.ContainsKey(guid))
                        {
                            var client = _clientsList[guid];
                            client.VoipPort = groupEP;

                            var spectatorAudio = _serverSettings.ServerSetting[(int)ServerSettingType.SPECTATORS_AUDIO_DISABLED];

                            if (client.Coalition == 0 && spectatorAudio == "DISABLED")
                            {
                                // IGNORE THE AUDIO
                            }
                            else
                            {
                                try
                                {
                                    //decode
                                    var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(rawBytes);

                                    if (udpVoicePacket != null && udpVoicePacket.Modulation != 4) //magical ignore message 4
                                    {
                                        SendToOthers(rawBytes, client, udpVoicePacket);
                                    }
                                }
                                catch (Exception)
                                {
                                    //Hide for now, slows down loop to much....
                                }
                                   
                                   
                            }
                        }
                        else
                        {
                            SRClient value;
                            _clientsList.TryRemove(guid, out value);
                            //  logger.Info("Removing  "+guid+" From UDP pool");
                        }
    //                    });
                    }
                    else if (rawBytes!=null && rawBytes.Length == 15 && rawBytes[0] == 1 && rawBytes[14] == 15)
                    {
                        try
                        {
                            //send back ping UDP
                            _listener.Send(rawBytes, rawBytes.Length, groupEP);
                        }
                        catch (Exception ex)
                        {
                            //dont log because it slows down thread too much...
                        }

                    }
                }
                catch (Exception e)
                {
                    //  logger.Error(e,"Error receving audio UDP for client " + e.Message);
                }
            }

            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }
        }

        public void RequestStop()
        {
            _stop = true;
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }
        }

        private void SendToOthers(byte[] bytes, SRClient fromClient, UDPVoicePacket udpVoicePacket)
        {
            var coalitionSecurity =
                                    _serverSettings.ServerSetting[(int)ServerSettingType.COALITION_AUDIO_SECURITY] == "ON";
            var guid = fromClient.ClientGuid;

            foreach (var client in _clientsList)
            {
                try
                {
                    if (!client.Key.Equals(guid))
                    {
                        var ip = client.Value.VoipPort;

                        // check that either coalition radio security is disabled OR the coalitions match
                        if (ip != null && (!coalitionSecurity || client.Value.Coalition == fromClient.Coalition))
                        {
                            var radioInfo = client.Value.RadioInfo;
                         
                            if (radioInfo != null  )
                            {
                                RadioReceivingState radioReceivingState = null;
                                var receivingRadio = radioInfo.CanHearTransmission(udpVoicePacket.Frequency, udpVoicePacket.Modulation,
                                    udpVoicePacket.UnitId, out radioReceivingState);

                                //only send if we can hear!
                                if (receivingRadio != null)
                                {
                                    _listener.Send(bytes, bytes.Length, ip);
                                }
                            }
                        }
                    }
                    else
                    {
                        var ip = client.Value.VoipPort;

                        if (ip != null)
                        {
                             //    _listener.Send(bytes, bytes.Length, ip);
                        }
                    }
                }
                catch (Exception e)
                {
                    //      IPEndPoint ip = client.Value;
                    //   logger.Error(e, "Error sending audio UDP for client " + e.Message);
                    SRClient value;
                    _clientsList.TryRemove(guid, out value);
                }
            }
        }


     
    }
}