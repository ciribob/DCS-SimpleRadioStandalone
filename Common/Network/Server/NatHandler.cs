using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using SharpOpenNat;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;

public class NatHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly Mapping _tcpMapping;
    private readonly Mapping _udpMapping;
    private INatDevice _device;

    public NatHandler(int port)
    {
        _tcpMapping = new Mapping(Protocol.Tcp, port, port, $"SRS Server TCP - {port}");
        _udpMapping = new Mapping(Protocol.Udp, port, port, $"SRS Server UDP - {port}");
    }

    public async Task OpenNATAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts.Token);
            await Task.WhenAll([_device.CreatePortMapAsync(_udpMapping, cts.Token), _device.CreatePortMapAsync(_tcpMapping, cts.Token)]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open port with UPNP/NAT");
        }
    }

    public async Task CloseNATAsync()
    {
        try
        {
            if (_device != null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await Task.WhenAll([_device.DeletePortMapAsync(_tcpMapping, cts.Token), _device.DeletePortMapAsync(_udpMapping, cts.Token)]);
                //Doesnt clear mappings on Shutdown - not sure why? The async deletes also dont work on application close but DO work on start / stop button press.
                //Maybe background threads are terminated?
            }

        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error closing UPNP/NAT ports.");
        }
    }
}