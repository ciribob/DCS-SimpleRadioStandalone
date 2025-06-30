using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class DCSAutoConnectHandler
{
    private static readonly object _lock = new();

    private CancellationTokenSource _cts = new();
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private UdpClient _dcsUdpListener;

    public DCSAutoConnectHandler()
    {
        // _receivedAutoConnect = receivedAutoConnect;

        StartDcsBroadcastListener();
    }

    private void StartDcsBroadcastListener()
    {
        var cancellationToken = _cts.Token;
        Task.Factory.StartNew(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var localEp = new IPEndPoint(IPAddress.Any,
                        GlobalSettingsStore.Instance.GetNetworkSetting(GlobalSettingsKeys.DCSAutoConnectUDP));
                    _dcsUdpListener = new UdpClient(localEp);
                    Logger.Info("DCS AutoConnect Listener Started.");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex,
                        $"Unable to bind to the AutoConnect Socket Port: {GlobalSettingsStore.Instance.GetNetworkSetting(GlobalSettingsKeys.DCSAutoConnectUDP)}");
                    Thread.Sleep(500);
                }
            }
               

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _dcsUdpListener.ReceiveAsync(cancellationToken);
                    var bytes = result.Buffer;

                    var message = Encoding.UTF8.GetString(
                        bytes, 0, bytes.Length);

                    HandleMessage(message);
                }
                catch (SocketException e)
                {
                    // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                    if (!cancellationToken.IsCancellationRequested) Logger.Error(e, "SocketException Handling DCS AutoConnect Message");
                }
                catch (OperationCanceledException)
                {
                    // Expected on closure.
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Handling DCS AutoConnect Message");
                }
            }


            try
            {
                _dcsUdpListener.Close();
                Logger.Info("Shutting down DCS AutoConnect listener.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception stoping DCS AutoConnect listener ");
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void HandleMessage(string message)
    {
        var address = message.Split(':');
        Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
            new ThreadStart(delegate
            {
                //ensure we only send one autoconnect at a time
                lock (_lock)
                {
                    message = message.Trim();
                    if (message.Contains(':'))
                        try
                        {
                            EventBus.Instance.PublishOnUIThreadAsync(new AutoConnectMessage()
                            {
                                Address = $"{address[0].Trim()}:{address[1].Trim()}"
                            });
                            //_receivedAutoConnect(address[0].Trim(), int.Parse(address[1].Trim()));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Exception Parsing DCS AutoConnect Message");
                        }
                    else
                        EventBus.Instance.PublishOnUIThreadAsync(new AutoConnectMessage()
                        {
                            Address = $"{address[0].Trim()}:5002"
                        });
                }
            }));
    }

    public void Stop()
    {
        _cts.Cancel();
        _cts.Dispose();

        try
        {
            _dcsUdpListener?.Close();
        } catch(Exception){
            // ignored
        }

    }
}