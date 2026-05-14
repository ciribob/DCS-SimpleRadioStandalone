using System;
using System.Threading;
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

    public async void OpenNAT()
    {
        try
        {
            using var cts = new CancellationTokenSource(10000);
           _device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts.Token);

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
            var task = _device?.DeletePortMapAsync(_tcpMapping);
            var task2 = _device?.DeletePortMapAsync(_udpMapping);
            task?.Wait(3000);
            task2?.Wait(3000);

            //Doesnt clear mappings on Shutdown - not sure why? The async deletes also dont work on application close but DO work on start / stop button press.
            //Maybe background threads are terminated?
        }
        catch (Exception)
        {
        }
    }
}