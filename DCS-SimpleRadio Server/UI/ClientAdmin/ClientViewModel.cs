using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Network;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin
{
    public class ClientViewModel : Screen
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IEventAggregator _eventAggregator;

        public ClientViewModel(SRClient client, IEventAggregator eventAggregator)
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
            _eventAggregator = eventAggregator;
            Client = client;
            Client.PropertyChanged += ClientOnPropertyChanged;
        }

        public SRClient Client { get; }

        public string ClientName => Client.Name;

        public string TransmittingFrequency => Client.TransmittingFrequency;

        public bool ClientMuted => Client.Muted;

        public SolidColorBrush ClientCoalitionColour
        {
            get
            {
                switch (Client.Coalition)
                {
                    case 0:
                        return new SolidColorBrush(Colors.White);
                    case 1:
                        return new SolidColorBrush(Colors.Red);
                    case 2:
                        return new SolidColorBrush(Colors.Blue);
                    default:
                        return new SolidColorBrush(Colors.White);
                }
            }
        }

        private void ClientOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Coalition")
            {
                NotifyOfPropertyChange(() => ClientCoalitionColour);
            }
            else if (propertyChangedEventArgs.PropertyName == "TransmittingFrequency")
            {
                NotifyOfPropertyChange(() => TransmittingFrequency);
            }
        }

        public void KickClient()
        {
            var messageBoxResult = MessageBox.Show($"Are you sure you want to Kick {Client.Name}?", "Ban Confirmation",
                MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                _eventAggregator.PublishOnBackgroundThreadAsync(new KickClientMessage(Client));
            }
        }

        public void BanClient()
        {
            var messageBoxResult = MessageBox.Show($"Are you sure you want to Ban {Client.Name}?", "Ban Confirmation",
                MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                _eventAggregator.PublishOnBackgroundThreadAsync(new BanClientMessage(Client));
            }
        }

        public void ToggleClientMute()
        {
            Client.Muted = !Client.Muted;
            NotifyOfPropertyChange(() => ClientMuted);
        }
    }
}