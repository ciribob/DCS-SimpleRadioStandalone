using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Caliburn.Micro;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI
{
    public sealed class MainViewModel:Screen,IHandle<ServerStateMessage>
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly ClientAdminViewModel _clientAdminViewModel;

        public MainViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, ClientAdminViewModel clientAdminViewModel)
        {
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _clientAdminViewModel = clientAdminViewModel;
            _eventAggregator.Subscribe(this);

            DisplayName = "DCS-SimpleRadio Server";
        }

        public bool IsServerRunning { get; private set; } = true;

        public string ServerButtonText => IsServerRunning ? "Stop Server" : "Start Server";
      
        public void ServerStartStop()
        {
            if (IsServerRunning)
            {
                _eventAggregator.PublishOnBackgroundThread(new StopServerMessage());
            }
            else
            {
                _eventAggregator.PublishOnBackgroundThread(new StartServerMessage());
            }
            
        }

        public int ClientsCount
        {
            get; private set;
        }

        public void ShowClientList()
        {
            IDictionary<string, object> settings = new Dictionary<string, object>
            {
                { "Icon", new BitmapImage(new Uri("pack://application:,,,/SR-Server;component/server-10.ico")) },
                { "ResizeMode" , ResizeMode.CanMinimize},

            };
            _windowManager.ShowWindow(_clientAdminViewModel,null,settings);
        }

        public string RadioSecurityText => ServerSettings.Instance.ServerSetting[(int)ServerSettingType.COALITION_AUDIO_SECURITY] == "ON"? "ON" : "OFF";

        public void RadioSecurityToggle()
        {
            var newSetting = RadioSecurityText == "ON"?"OFF" : "ON";
            ServerSettings.Instance.WriteSetting(ServerSettingType.COALITION_AUDIO_SECURITY, newSetting);
            NotifyOfPropertyChange(() => RadioSecurityText);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());

        }

        public string SpectatorAudioText => ServerSettings.Instance.ServerSetting[(int)ServerSettingType.SPECTATORS_AUDIO_DISABLED] == "DISABLED" ? "DISABLED" : "ENABLED";

        public void SpectatorAudioToggle()
        {
            var newSetting = SpectatorAudioText == "ENABLED" ? "DISABLED" : "ENABLED";
            ServerSettings.Instance.WriteSetting(ServerSettingType.SPECTATORS_AUDIO_DISABLED, newSetting);
            NotifyOfPropertyChange(() => SpectatorAudioText);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());

        }

        public string ExportListText => ServerSettings.Instance.ServerSetting[(int)ServerSettingType.CLIENT_EXPORT_ENABLED] == "ON" ? "ON" : "OFF";

        public void ExportListToggle()
        {
            var newSetting = ExportListText == "ON" ? "OFF" : "ON";
            ServerSettings.Instance.WriteSetting(ServerSettingType.CLIENT_EXPORT_ENABLED, newSetting);
            NotifyOfPropertyChange(() => ExportListText);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public string LOSText => ServerSettings.Instance.ServerSetting[(int)ServerSettingType.LOS_ENABLED] == "ON" ? "ON" : "OFF";

        public void LOSToggle()
        {
            var newSetting = LOSText == "ON" ? "OFF" : "ON";
            ServerSettings.Instance.WriteSetting(ServerSettingType.LOS_ENABLED, newSetting);
            NotifyOfPropertyChange(() => LOSText);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public string DistanceLimitText => ServerSettings.Instance.ServerSetting[(int)ServerSettingType.DISTANCE_ENABLED] == "ON" ? "ON" : "OFF";

        public void DistanceLimitToggle()
        {
            var newSetting = DistanceLimitText == "ON" ? "OFF" : "ON";
            ServerSettings.Instance.WriteSetting(ServerSettingType.DISTANCE_ENABLED, newSetting);
            NotifyOfPropertyChange(() => DistanceLimitText);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void Handle(ServerStateMessage message)
        {
            IsServerRunning = message.IsRunning;
            ClientsCount = message.Count;
        }
    }
}
