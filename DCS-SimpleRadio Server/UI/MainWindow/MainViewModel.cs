using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI.MainWindow
{
    public sealed class MainViewModel : Screen, IHandle<ServerStateMessage>
    {
        private readonly ClientAdminViewModel _clientAdminViewModel;
        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private DispatcherTimer _passwordDebounceTimer = null;

        public MainViewModel(IWindowManager windowManager, IEventAggregator eventAggregator,
            ClientAdminViewModel clientAdminViewModel)
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _clientAdminViewModel = clientAdminViewModel;
            _eventAggregator.Subscribe(this);

            DisplayName = $"{Properties.Resources.TitleServer} - {UpdaterChecker.VERSION} - {ListeningPort}" ;

            Logger.Info("DCS-SRS Server Running - " + UpdaterChecker.VERSION);
        }

        public bool IsServerRunning { get; private set; } = true;

        public string ServerButtonText => IsServerRunning ? $"{Properties.Resources.BtnStopServer}" : $"{Properties.Resources.BtnStartServer}";

        public int NodeLimit
        {
            get => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT).IntValue;
            set
            {
                ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT,
                    value.ToString());
                _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
            }
        }

        public int ClientsCount { get; private set; }

        public string RadioSecurityText
            =>
                ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue
                    ? $"{Properties.Resources.BtnOn}"
                    : $"{Properties.Resources.BtnOff}";

        public string SpectatorAudioText
            =>
                ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue
                    ? $"{Properties.Resources.BtnDisabled}"
                    : $"{Properties.Resources.BtnEnabled}";

        public string ExportListText
            =>
                ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED).BoolValue
                    ? $"{Properties.Resources.BtnOn}"
                    : $"{Properties.Resources.BtnOff}";

        public string LOSText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.LOS_ENABLED).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string DistanceLimitText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string RealRadioText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string IRLRadioRxText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string RadioExpansion
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string CheckForBetaUpdates
            => ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string ExternalAWACSMode
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string AllowRadioEncryption
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string StrictRadioEncryption
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public bool IsExternalAWACSModeEnabled { get; set; }
            = ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue;

        private string _externalAWACSModeBluePassword =
            ServerSettingsStore.Instance.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD).StringValue;
        public string ExternalAWACSModeBluePassword
        {
            get { return _externalAWACSModeBluePassword; }
            set
            {
                _externalAWACSModeBluePassword = value.Trim();
                if (_passwordDebounceTimer != null)
                {
                    _passwordDebounceTimer.Stop();
                    _passwordDebounceTimer.Tick -= PasswordDebounceTimerTick;
                    _passwordDebounceTimer = null;
                }

                _passwordDebounceTimer = new DispatcherTimer();
                _passwordDebounceTimer.Tick += PasswordDebounceTimerTick;
                _passwordDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
                _passwordDebounceTimer.Start();

                NotifyOfPropertyChange(() => ExternalAWACSModeBluePassword);
            }
        }

        private string _externalAWACSModeRedPassword =
            ServerSettingsStore.Instance.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD).StringValue;
        public string ExternalAWACSModeRedPassword
        {
            get { return _externalAWACSModeRedPassword; }
            set
            {
                _externalAWACSModeRedPassword = value.Trim();
                if (_passwordDebounceTimer != null)
                {
                    _passwordDebounceTimer.Stop();
                    _passwordDebounceTimer.Tick -= PasswordDebounceTimerTick;
                    _passwordDebounceTimer = null;
                }

                _passwordDebounceTimer = new DispatcherTimer();
                _passwordDebounceTimer.Tick += PasswordDebounceTimerTick;
                _passwordDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
                _passwordDebounceTimer.Start();

                NotifyOfPropertyChange(() => ExternalAWACSModeRedPassword);
            }
        }

        private string _testFrequencies =
            ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES).StringValue;

        private DispatcherTimer _testFrequenciesDebounceTimer;

        public string TestFrequencies
        {
            get { return _testFrequencies; }
            set
            {
                _testFrequencies = value.Trim();
                if (_testFrequenciesDebounceTimer != null)
                {
                    _testFrequenciesDebounceTimer.Stop();
                    _testFrequenciesDebounceTimer.Tick -= TestFrequenciesDebounceTimerTick;
                    _testFrequenciesDebounceTimer = null;
                }

                _testFrequenciesDebounceTimer = new DispatcherTimer();
                _testFrequenciesDebounceTimer.Tick += TestFrequenciesDebounceTimerTick;
                _testFrequenciesDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
                _testFrequenciesDebounceTimer.Start();

                NotifyOfPropertyChange(() => TestFrequencies);
            }
        }

        private string _globalLobbyFrequencies =
            ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES).StringValue;

        private DispatcherTimer _globalLobbyFrequenciesDebounceTimer;

        public string GlobalLobbyFrequencies
        {
            get { return _globalLobbyFrequencies; }
            set
            {
                _globalLobbyFrequencies = value.Trim();
                if (_globalLobbyFrequenciesDebounceTimer != null)
                {
                    _globalLobbyFrequenciesDebounceTimer.Stop();
                    _globalLobbyFrequenciesDebounceTimer.Tick -= GlobalLobbyFrequenciesDebounceTimerTick;
                    _globalLobbyFrequenciesDebounceTimer = null;
                }

                _globalLobbyFrequenciesDebounceTimer = new DispatcherTimer();
                _globalLobbyFrequenciesDebounceTimer.Tick += GlobalLobbyFrequenciesDebounceTimerTick;
                _globalLobbyFrequenciesDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
                _globalLobbyFrequenciesDebounceTimer.Start();

                NotifyOfPropertyChange(() => GlobalLobbyFrequencies);
            }
        }

        public string OverrideEffectsOnGlobal 
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string TunedCountText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string LotATCExportText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string ShowTransmitterNameText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string TransmissionLogEnabledText
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";


        public string ListeningPort
            => ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.SERVER_PORT).StringValue;

        public Task HandleAsync(ServerStateMessage message, CancellationToken token)
        {
            IsServerRunning = message.IsRunning;
            ClientsCount = message.Count;
            return Task.CompletedTask;
        }

        public void ServerStartStop()
        {
            if (IsServerRunning)
            {
                _eventAggregator.PublishOnBackgroundThreadAsync(new StopServerMessage());
            }
            else
            {
                _eventAggregator.PublishOnBackgroundThreadAsync(new StartServerMessage());
            }
        }

        public void ShowClientList()
        {
            IDictionary<string, object> settings = new Dictionary<string, object>
            {
                {"Icon", new BitmapImage(new Uri("pack://application:,,,/SR-Server;component/server-10.ico"))},
                {"ResizeMode", ResizeMode.CanMinimize}
            };
            _windowManager.ShowWindowAsync(_clientAdminViewModel, null, settings);
        }

        public void RadioSecurityToggle()
        {
            var newSetting = RadioSecurityText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, newSetting);
            NotifyOfPropertyChange(() => RadioSecurityText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void SpectatorAudioToggle()
        {
            var newSetting = SpectatorAudioText != $"{Properties.Resources.BtnDisabled}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, newSetting);
            NotifyOfPropertyChange(() => SpectatorAudioText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void ExportListToggle()
        {
            var newSetting = ExportListText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, newSetting);
            NotifyOfPropertyChange(() => ExportListText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void LOSToggle()
        {
            var newSetting = LOSText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.LOS_ENABLED, newSetting);
            NotifyOfPropertyChange(() => LOSText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void DistanceLimitToggle()
        {
            var newSetting = DistanceLimitText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED, newSetting);
            NotifyOfPropertyChange(() => DistanceLimitText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void RealRadioToggle()
        {
            var newSetting = RealRadioText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX, newSetting);
            NotifyOfPropertyChange(() => RealRadioText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void IRLRadioRxBehaviourToggle()
        {
            var newSetting = IRLRadioRxText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, newSetting);
            NotifyOfPropertyChange(() => IRLRadioRxText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void RadioExpansionToggle()
        {
            var newSetting = RadioExpansion != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION, newSetting);
            NotifyOfPropertyChange(() => RadioExpansion);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void AllowRadioEncryptionToggle()
        {
            var newSetting = AllowRadioEncryption != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, newSetting);
            NotifyOfPropertyChange(() => AllowRadioEncryption);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void StrictRadioEncryptionToggle()
        {
            var newSetting = StrictRadioEncryption != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, newSetting);
            NotifyOfPropertyChange(() => StrictRadioEncryption);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void CheckForBetaUpdatesToggle()
        {
            var newSetting = CheckForBetaUpdates != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, newSetting);
            NotifyOfPropertyChange(() => CheckForBetaUpdates);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void OverrideEffectsOnGlobalToggle()
        {
            var newSetting = OverrideEffectsOnGlobal != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, newSetting);
            NotifyOfPropertyChange(() => OverrideEffectsOnGlobal);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void ExternalAWACSModeToggle()
        {
            var newSetting = ExternalAWACSMode != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, newSetting);

            IsExternalAWACSModeEnabled = newSetting;

            NotifyOfPropertyChange(() => ExternalAWACSMode);
            NotifyOfPropertyChange(() => IsExternalAWACSModeEnabled);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        private void PasswordDebounceTimerTick(object sender, EventArgs e)
        {
            ServerSettingsStore.Instance.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, _externalAWACSModeBluePassword);
            ServerSettingsStore.Instance.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, _externalAWACSModeRedPassword);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());

            _passwordDebounceTimer.Stop();
            _passwordDebounceTimer.Tick -= PasswordDebounceTimerTick;
            _passwordDebounceTimer = null;
        }


        private void TestFrequenciesDebounceTimerTick(object sender, EventArgs e)
        {
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES, _testFrequencies);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged()
            {
                TestFrequencies = _testFrequencies
            });

            _testFrequenciesDebounceTimer.Stop();
            _testFrequenciesDebounceTimer.Tick -= TestFrequenciesDebounceTimerTick;
            _testFrequenciesDebounceTimer = null;
        }

        private void GlobalLobbyFrequenciesDebounceTimerTick(object sender, EventArgs e)
        {
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, _globalLobbyFrequencies);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged()
            {
                GlobalLobbyFrequencies = _globalLobbyFrequencies
            });

            _globalLobbyFrequenciesDebounceTimer.Stop();
            _globalLobbyFrequenciesDebounceTimer.Tick -= GlobalLobbyFrequenciesDebounceTimerTick;
            _globalLobbyFrequenciesDebounceTimer = null;
        }

        public void TunedCountToggle()
        {
            var newSetting = TunedCountText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, newSetting);
            NotifyOfPropertyChange(() => TunedCountText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void LotATCExportToggle()
        {
            var newSetting = LotATCExportText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, newSetting);
            NotifyOfPropertyChange(() => LotATCExportText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void ShowTransmitterNameToggle()
        {
            var newSetting = ShowTransmitterNameText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, newSetting);
            NotifyOfPropertyChange(() => ShowTransmitterNameText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void TransmissionLogEnabledToggle()
        {
            var newSetting = TransmissionLogEnabledText != $"{Properties.Resources.BtnOn}";
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, newSetting);
            NotifyOfPropertyChange(() => TransmissionLogEnabledText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public int ArchiveLimit
        {
            get => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue;
            set
            {
                ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION,
                    value.ToString());

                _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
            }
        }
    }
}