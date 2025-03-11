using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Open.Nat;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public class NatHandler
    {
        private int _port;
        private NatDevice _device;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource _searchToken;
        private NatDiscoverer _discoverer;
        private Mapping _tcpMapping;
        private Mapping _udpMapping;

        public NatHandler(int port)
        {
            this._port = port;
            _tcpMapping = new Mapping(Protocol.Tcp, _port, _port, $"SRS Server TCP - {_port}");
            _udpMapping = new Mapping(Protocol.Udp, _port, _port, $"SRS Server UDP - {_port}");
        }
        public async void OpenNAT()
        {
            try
            {
                _discoverer = new NatDiscoverer();
                _searchToken = new CancellationTokenSource(5000);
                _device = await _discoverer.DiscoverDeviceAsync(PortMapper.Upnp, _searchToken);
              
                await _device.CreatePortMapAsync(_udpMapping);
                await _device.CreatePortMapAsync(_tcpMapping);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open port with UPNP/NAT");
            }
        }

        public void CloseNAT()
        {
            try
            {
                _searchToken?.Cancel();

                var task = _device?.DeletePortMapAsync(_tcpMapping);
                var task2 =  _device?.DeletePortMapAsync(_udpMapping);
                task?.Wait(3000);
                task2?.Wait(3000);

                //Doesnt clear mappings on Shutdown - not sure why? The async deletes also dont work on application close but DO work on start / stop button press.
                //Maybe background threads are terminated?
            }
            catch (Exception) { }

           

        }
    }
}
