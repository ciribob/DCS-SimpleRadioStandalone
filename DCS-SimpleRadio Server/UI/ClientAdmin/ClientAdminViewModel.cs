using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Network;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin
{
    public sealed class ClientAdminViewModel : Screen, IHandle<ServerStateMessage>
    {
        private static readonly TimeSpan LastTransmissionThreshold = TimeSpan.FromMilliseconds(200);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IEventAggregator _eventAggregator;
        private readonly DispatcherTimer _updateTimer;

        public ClientAdminViewModel(IEventAggregator eventAggregator)
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnPublishedThread(this);

            DisplayName = $"{Properties.Resources.TitleClientList}";

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _updateTimer.Tick += _updateTimer_Tick;
        }

        public ObservableCollection<ClientViewModel> Clients { get; } = new ObservableCollection<ClientViewModel>();

        protected override Task OnActivateAsync(CancellationToken token)
        {
            _updateTimer?.Start();
            return Task.CompletedTask;
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken token)
        {
            if (close)
            {
                _updateTimer?.Stop();
            }

            return Task.CompletedTask;
        }

        public Task HandleAsync(ServerStateMessage message, CancellationToken token)
        {
            Clients.Clear();

            message.Clients.Apply(client => Clients.Add(new ClientViewModel(client, _eventAggregator)));

            return Task.CompletedTask;
        }

        private void _updateTimer_Tick(object sender, EventArgs e)
        {
            foreach (ClientViewModel client in Clients)
            {
                if ((DateTime.Now - client.Client.LastTransmissionReceived) >= LastTransmissionThreshold)
                {
                    client.Client.TransmittingFrequency = "---";
                }
            }
        }
    }
}