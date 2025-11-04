using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI.MainWindow;

public sealed class MainViewModel : Screen, IHandle<ServerStateMessage>
{
    private readonly ClientAdminViewModel _clientAdminViewModel;
    private readonly IEventAggregator _eventAggregator;
    private readonly IWindowManager _windowManager;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private ServerSettingsStore _serverSettings => ServerSettingsStore.Instance;
    
        private DispatcherTimer _passwordDebounceTimer = null;

    public MainViewModel(IWindowManager windowManager, IEventAggregator eventAggregator,
        ClientAdminViewModel clientAdminViewModel)
    {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
        _windowManager = windowManager;
        _eventAggregator = eventAggregator;
        _clientAdminViewModel = clientAdminViewModel;
        _eventAggregator.SubscribeOnUIThread(this);

        DisplayName = $"{Properties.Resources.TitleServer} - {UpdaterChecker.VERSION} - {ListeningPort}" ;

        Logger.Info("DCS-SRS Server Running - " + UpdaterChecker.VERSION);
    }

    public bool IsServerRunning { get; private set; } = true;

        public string ServerButtonText => IsServerRunning ? $"{Properties.Resources.BtnStopServer}" : $"{Properties.Resources.BtnStartServer}";

        public int NodeLimit
        {
            get => _serverSettings.SynchronizedSettings.RetransmissionNodeLimit;
            set
            {
                _serverSettings.SynchronizedSettings.RetransmissionNodeLimit = value;
                _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
            }
        }

    public int ClientsCount { get; private set; }

        public string RadioSecurityText
            =>
                _serverSettings.SynchronizedSettings.IsCoalitionAudioSecurityEnabled
                    ? $"{Properties.Resources.BtnOn}"
                    : $"{Properties.Resources.BtnOff}";

        public string SpectatorAudioText
            =>
                _serverSettings.SynchronizedSettings.IsSpectatorAudioDisabled
                    ? $"{Properties.Resources.BtnDisabled}"
                    : $"{Properties.Resources.BtnEnabled}";

        public string ExportListText
            =>
                _serverSettings.SynchronizedSettings.IsClientExportEnabled
                    ? $"{Properties.Resources.BtnOn}"
                    : $"{Properties.Resources.BtnOff}";

        public string LOSText
            => _serverSettings.SynchronizedSettings.IsLineOfSightEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string DistanceLimitText
            => _serverSettings.SynchronizedSettings.IsDistanceLimitEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

    public string RealRadioText
            => _serverSettings.SynchronizedSettings.IsIrlRadioTxEffectsEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string IRLRadioRxText
            => _serverSettings.SynchronizedSettings.IsIrlRadioRxEffectsEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string RadioExpansion
            => _serverSettings.SynchronizedSettings.IsRadioExpansionAllowed ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string CheckForBetaUpdates
            => _serverSettings.ServerSettings.IsCheckForBetaUpdatesEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string ExternalAWACSMode
            => _serverSettings.SynchronizedSettings.IsExternalModeEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string AllowRadioEncryption
            => _serverSettings.SynchronizedSettings.IsRadioEncryptionAllowed ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string StrictRadioEncryption
            => _serverSettings.SynchronizedSettings.IsStrictRadioEncryptionEnabled ? $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public bool IsExternalAWACSModeEnabled => _serverSettings.SynchronizedSettings.IsExternalModeEnabled;
        
        public string ExternalAWACSModeBluePassword
        {
            get => _serverSettings.ExternalModeSettings.ExternalModePassBlue;
            set
            {
                _serverSettings.ExternalModeSettings.ExternalModePassBlue = value.Trim();
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
        
        public string ExternalAWACSModeRedPassword
        {
            get => _serverSettings.ExternalModeSettings.ExternalModePassRed;
            set
            {
                _serverSettings.ExternalModeSettings.ExternalModePassRed = value.Trim();
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

        private DispatcherTimer _testFrequenciesDebounceTimer;

    public string TestFrequencies
    {
        get => String.Join(",",_serverSettings.SynchronizedSettings.TestFrequencies);
        set
        {
            _serverSettings.SynchronizedSettings.TestFrequencies = 
                new List<double>(value.Trim().Split(',').Select(double.Parse).ToList());
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
    
        private DispatcherTimer _globalLobbyFrequenciesDebounceTimer;

        public string GlobalLobbyFrequencies
        {
            get => String.Join(",",_serverSettings.SynchronizedSettings.GlobalLobbyFrequencies);
            set
            {
                _serverSettings.SynchronizedSettings.GlobalLobbyFrequencies = 
                    new List<double>(value.Trim().Split(',').Select(double.Parse).ToList());
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
            => _serverSettings.SynchronizedSettings.IsRadioEffectOverrideOnGlobalEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string TunedCountText
            => _serverSettings.SynchronizedSettings.IsShowTunedCountEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string LotATCExportText
            => _serverSettings.SynchronizedSettings.IsLotAtcExportEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string ShowTransmitterNameText
            => _serverSettings.SynchronizedSettings.IsShowTransmitterNameEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string TransmissionLogEnabledText
            => _serverSettings.SynchronizedSettings.IsTransmissionLogEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";

        public string ServerPresetsEnabledText
            => _serverSettings.SynchronizedSettings.IsServerPresetsEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";
        
        public string ServerEAMRadioPresetEnabledText
            => _serverSettings.SynchronizedSettings.IsExternalModeEnabled ? 
                $"{Properties.Resources.BtnOn}" : $"{Properties.Resources.BtnOff}";
        
    public string ListeningPort => _serverSettings.ServerSettings.ServerPort.ToString();

    public Task HandleAsync(ServerStateMessage message, CancellationToken token)
    {
        IsServerRunning = message.IsRunning;
        ClientsCount = message.Count;
            return Task.CompletedTask;
    }

    public void ServerStartStop()
    {
        if (IsServerRunning)
            _eventAggregator.PublishOnBackgroundThreadAsync(new StopServerMessage());
        else
            _eventAggregator.PublishOnBackgroundThreadAsync(new StartServerMessage());
    }

    public void ShowClientList()
    {
        IDictionary<string, object> settings = new Dictionary<string, object>
        {
            { "Icon", new BitmapImage(new Uri("pack://application:,,,/SRS-Server;component/server-10.ico")) },
            { "ResizeMode", ResizeMode.CanMinimize }
        };
        _windowManager.ShowWindowAsync(_clientAdminViewModel, null, settings);
    }

        public void RadioSecurityToggle()
        {
            var newSetting = RadioSecurityText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsCoalitionAudioSecurityEnabled = newSetting;
            NotifyOfPropertyChange(() => RadioSecurityText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void SpectatorAudioToggle()
        {
            var newSetting = SpectatorAudioText != $"{Properties.Resources.BtnDisabled}";
            _serverSettings.SynchronizedSettings.IsSpectatorAudioDisabled = newSetting;
            NotifyOfPropertyChange(() => SpectatorAudioText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void ExportListToggle()
        {
            var newSetting = ExportListText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsClientExportEnabled = newSetting;
            NotifyOfPropertyChange(() => ExportListText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void LOSToggle()
        {
            var newSetting = LOSText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsLineOfSightEnabled = newSetting;
            NotifyOfPropertyChange(() => LOSText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void DistanceLimitToggle()
        {
            var newSetting = DistanceLimitText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsDistanceLimitEnabled = newSetting;
            NotifyOfPropertyChange(() => DistanceLimitText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

    public void RealRadioToggle()
    {
            var newSetting = RealRadioText != $"{Properties.Resources.BtnOn}";
        _serverSettings.SynchronizedSettings.IsIrlRadioTxEffectsEnabled = newSetting;
        NotifyOfPropertyChange(() => RealRadioText);

        _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
    }

        public void IRLRadioRxBehaviourToggle()
        {
            var newSetting = IRLRadioRxText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsIrlRadioRxEffectsEnabled = newSetting;
            NotifyOfPropertyChange(() => IRLRadioRxText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void RadioExpansionToggle()
        {
            var newSetting = RadioExpansion != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsRadioExpansionAllowed = newSetting;
            NotifyOfPropertyChange(() => RadioExpansion);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void AllowRadioEncryptionToggle()
        {
            var newSetting = AllowRadioEncryption != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsRadioEncryptionAllowed = newSetting;
            NotifyOfPropertyChange(() => AllowRadioEncryption);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void StrictRadioEncryptionToggle()
        {
            var newSetting = StrictRadioEncryption != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsStrictRadioEncryptionEnabled = newSetting;
            NotifyOfPropertyChange(() => StrictRadioEncryption);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void CheckForBetaUpdatesToggle()
        {
            var newSetting = CheckForBetaUpdates != $"{Properties.Resources.BtnOn}";
            _serverSettings.ServerSettings.IsCheckForBetaUpdatesEnabled = newSetting;
            NotifyOfPropertyChange(() => CheckForBetaUpdates);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void OverrideEffectsOnGlobalToggle()
        {
            var newSetting = OverrideEffectsOnGlobal != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsRadioEffectOverrideOnGlobalEnabled = newSetting;
            NotifyOfPropertyChange(() => OverrideEffectsOnGlobal);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void ExternalAWACSModeToggle()
        {
            var newSetting = ExternalAWACSMode != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsExternalModeEnabled = newSetting;

            NotifyOfPropertyChange(() => ExternalAWACSMode);
            NotifyOfPropertyChange(() => IsExternalAWACSModeEnabled);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        private void PasswordDebounceTimerTick(object sender, EventArgs e)
        {
            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());

            _passwordDebounceTimer.Stop();
            _passwordDebounceTimer.Tick -= PasswordDebounceTimerTick;
            _passwordDebounceTimer = null;
        }


    private void TestFrequenciesDebounceTimerTick(object sender, EventArgs e)
    {
        _eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged
        {
            TestFrequencies = String.Join(',', _serverSettings.SynchronizedSettings.TestFrequencies)
        });

        _testFrequenciesDebounceTimer.Stop();
        _testFrequenciesDebounceTimer.Tick -= TestFrequenciesDebounceTimerTick;
        _testFrequenciesDebounceTimer = null;
    }

        private void GlobalLobbyFrequenciesDebounceTimerTick(object sender, EventArgs e)
        {
            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged()
            {
                GlobalLobbyFrequencies = String.Join(',', _serverSettings.SynchronizedSettings.GlobalLobbyFrequencies)
            });

            _globalLobbyFrequenciesDebounceTimer.Stop();
            _globalLobbyFrequenciesDebounceTimer.Tick -= GlobalLobbyFrequenciesDebounceTimerTick;
            _globalLobbyFrequenciesDebounceTimer = null;
        }

        public void TunedCountToggle()
        {
            var newSetting = TunedCountText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsShowTunedCountEnabled = newSetting;
            NotifyOfPropertyChange(() => TunedCountText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void LotATCExportToggle()
        {
            var newSetting = LotATCExportText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsLotAtcExportEnabled = newSetting;
            NotifyOfPropertyChange(() => LotATCExportText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void ShowTransmitterNameToggle()
        {
            var newSetting = ShowTransmitterNameText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsShowTransmitterNameEnabled = newSetting;
            NotifyOfPropertyChange(() => ShowTransmitterNameText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public void TransmissionLogEnabledToggle()
        {
            var newSetting = TransmissionLogEnabledText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsTransmissionLogEnabled = newSetting;
            NotifyOfPropertyChange(() => TransmissionLogEnabledText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }

        public int ArchiveLimit
        {
            get => _serverSettings.SynchronizedSettings.TransmissionLogRetentionLimit;
            set
            {
                _serverSettings.SynchronizedSettings.TransmissionLogRetentionLimit = value;

                _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
            }
        }
        
        public void ServerPresetsEnabledToggle()
        {
            var newSetting = ServerPresetsEnabledText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsServerPresetsEnabled = newSetting;
            NotifyOfPropertyChange(() => ServerPresetsEnabledText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }
        
        public void ServerEAMRadioPresetEnabledToggle()
        {
            var newSetting = ServerEAMRadioPresetEnabledText != $"{Properties.Resources.BtnOn}";
            _serverSettings.SynchronizedSettings.IsServerPresetsEnabled = newSetting;
            NotifyOfPropertyChange(() => ServerEAMRadioPresetEnabledText);

            _eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
        }
}