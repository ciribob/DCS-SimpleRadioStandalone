using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Overlay;
using CommunityToolkit.Mvvm.DependencyInjection;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NLog;
using WPFCustomMessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public delegate void ReceivedAutoConnect(string address, int port);

        public delegate void ToggleOverlayCallback(bool uiButton, bool awacs);

        private readonly AudioManager _audioManager;

        private readonly string _guid;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private AudioPreview _audioPreview;
        private SRSClientSyncHandler _client;
        private DCSAutoConnectHandler _dcsAutoConnectListener;
        private int _port = 5002;

        private Overlay.RadioOverlayWindow _radioOverlayWindow;
        private AwacsRadioOverlayWindow.RadioOverlayWindow _awacsRadioOverlay;

        private IPAddress _resolvedIp;
        private ServerSettingsWindow _serverSettingsWindow;

        private ClientListWindow _clientListWindow;

        //used to debounce toggle
        private long _toggleShowHide;

        private readonly DispatcherTimer _updateTimer;
        private ServerAddress _serverAddress;
        private readonly DelegateCommand _connectCommand;

        public ISrsSettings GlobalSettings => Ioc.Default.GetRequiredService<ISrsSettings>();

        /// <remarks>Used in the XAML for DataBinding many things</remarks>
        public ClientStateSingleton ClientState { get; } = ClientStateSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding the connected client count</remarks>
        public ConnectedClientsSingleton Clients { get; } = ConnectedClientsSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding input audio related UI elements</remarks>
        public AudioInputSingleton AudioInput { get; } = AudioInputSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding output audio related UI elements</remarks>
        public AudioOutputSingleton AudioOutput { get; } = AudioOutputSingleton.Instance;
        
        public MainWindow()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            InitializeComponent();

            // Initialize ToolTip controls
            ToolTips.Init();

            // Initialize images/icons
            Images.Init();

            // Initialise sounds
            Sounds.Init();

            // Set up tooltips that are always defined
            InitToolTips();

            DataContext = this;

            var client = ClientStateSingleton.Instance;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = GlobalSettings.ClientSettings.ClientX;
            Top = GlobalSettings.ClientSettings.ClientY;

            Title = Title + " - " + UpdaterChecker.VERSION;

            CheckWindowVisibility();

            if (GlobalSettings.ClientSettings.StartMinimised)
            {
                Hide();
                WindowState = WindowState.Minimized;

                Logger.Info("Started DCS-SimpleRadio Client " + UpdaterChecker.VERSION + " minimized");
            }
            else
            {
                Logger.Info("Started DCS-SimpleRadio Client " + UpdaterChecker.VERSION);
            }

            _guid = ClientStateSingleton.Instance.ShortGUID;
            Analytics.Log("Client", "Startup", GlobalSettings.ClientSettings.ClientIdLong);

            InitSettingsScreen();

            InitSettingsProfiles();
            ReloadProfile();

            InitInput();

            _connectCommand = new DelegateCommand(Connect, () => ServerAddress != null);
            FavouriteServersViewModel = new FavouriteServersViewModel(new CsvFavouriteServerStore());

            InitDefaultAddress();

            SpeakerBoost.Value = GlobalSettings.ClientSettings.SpeakerBoost;

            Speaker_VU.Value = -100;
            Mic_VU.Value = -100;

            ExternalAWACSModeName.Text = GlobalSettings.ClientSettings.LastSeenName;
            UpdatePresetsFolderLabel();

            _audioManager = new AudioManager(AudioOutput.WindowsN);
            _audioManager.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost.Value);

            if ((SpeakerBoostLabel != null) && (SpeakerBoost != null))
            {
                SpeakerBoostLabel.Content = VolumeConversionHelper.ConvertLinearDiffToDB(_audioManager.SpeakerBoost);
            }

            UpdaterChecker.CheckForUpdate(GlobalSettings.ClientSettings.CheckForBetaUpdates);

            InitFlowDocument();

            _dcsAutoConnectListener = new DCSAutoConnectHandler(AutoConnect);

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _updateTimer.Tick += UpdatePlayerLocationAndVUMeters;
            _updateTimer.Start();

        }

        private void CheckWindowVisibility()
        {
            if (GlobalSettings.ClientSettings.DisableWindowVisibilityCheck)
            {
                Logger.Info("Window visibility check is disabled, skipping");
                return;
            }

            bool mainWindowVisible = false;
            bool radioWindowVisible = false;
            bool awacsWindowVisible = false;

            int mainWindowX = (int)GlobalSettings.ClientSettings.ClientX;
            int mainWindowY = (int)GlobalSettings.ClientSettings.ClientY;
            int radioWindowX = (int)GlobalSettings.ClientSettings.RadioX;
            int radioWindowY = (int)GlobalSettings.ClientSettings.RadioY;
            int awacsWindowX = (int)GlobalSettings.ClientSettings.AwacsX;
            int awacsWindowY = (int)GlobalSettings.ClientSettings.AwacsY;

            Logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");
            Logger.Info($"Checking window visibility for radio overlay {{X={radioWindowX},Y={radioWindowY}}}");
            Logger.Info($"Checking window visibility for AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}}");

            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                Logger.Info($"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

                if (screen.Bounds.Contains(mainWindowX, mainWindowY))
                {
                    Logger.Info($"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    mainWindowVisible = true;
                }
                if (screen.Bounds.Contains(radioWindowX, radioWindowY))
                {
                    Logger.Info($"Radio overlay {{X={radioWindowX},Y={radioWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    radioWindowVisible = true;
                }
                if (screen.Bounds.Contains(awacsWindowX, awacsWindowY))
                {
                    Logger.Info($"AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    awacsWindowVisible = true;
                }
            }

            if (!mainWindowVisible)
            {
                MessageBox.Show(this,
                    Properties.Resources.MsgBoxNotVisibleText,
                    Properties.Resources.MsgBoxNotVisible,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"Main client window outside visible area of monitors, resetting position ({mainWindowX},{mainWindowY}) to defaults");

                GlobalSettings.ClientSettings.ClientX = 200;
                GlobalSettings.ClientSettings.ClientY = 200;

                Left = 200;
                Top = 200;
            }

            if (!radioWindowVisible)
            {
                MessageBox.Show(this,
                    Properties.Resources.MsgBoxNotVisibleText,
                    Properties.Resources.MsgBoxNotVisible,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"Radio overlay window outside visible area of monitors, resetting position ({radioWindowX},{radioWindowY}) to defaults");

                GlobalSettings.ClientSettings.RadioX = 300;
                GlobalSettings.ClientSettings.RadioY = 300;

                if (_radioOverlayWindow != null)
                {
                    _radioOverlayWindow.Left = 300;
                    _radioOverlayWindow.Top = 300;
                }
            }

            if (!awacsWindowVisible)
            {
                MessageBox.Show(this,
                    Properties.Resources.MsgBoxNotVisibleText,
                    Properties.Resources.MsgBoxNotVisible,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"AWACS overlay window outside visible area of monitors, resetting position ({awacsWindowX},{awacsWindowY}) to defaults");

                GlobalSettings.ClientSettings.AwacsX = 300;
                GlobalSettings.ClientSettings.AwacsY = 300;

                if (_awacsRadioOverlay != null)
                {
                    _awacsRadioOverlay.Left = 300;
                    _awacsRadioOverlay.Top = 300;
                }
            }
        }

        private void InitFlowDocument()
        {
            //make hyperlinks work
            var hyperlinks = WPFElementHelper.GetVisuals(AboutFlowDocument).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler((sender, args) =>
                {
                    Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri));
                    args.Handled = true;
                });
        }

        private void InitDefaultAddress()
        {
            // legacy setting migration
            if (!string.IsNullOrEmpty(GlobalSettings.ClientSettings.LastServer) &&
                FavouriteServersViewModel.Addresses.Count == 0)
            {
                var oldAddress = new ServerAddress(GlobalSettings.ClientSettings.LastServer,
                    GlobalSettings.ClientSettings.LastServer, null, true);
                FavouriteServersViewModel.Addresses.Add(oldAddress);
            }

            ServerAddress = FavouriteServersViewModel.DefaultServerAddress;
        }

        private void InitSettingsProfiles()
        {
            ControlsProfile.IsEnabled = false;
            ControlsProfile.Items.Clear();
            foreach (var profile in GlobalSettings.ProfileNames)
            {
                ControlsProfile.Items.Add(profile);
            }
            ControlsProfile.IsEnabled = true;
            ControlsProfile.SelectedIndex = 0;

            CurrentProfile.Content = GlobalSettings.CurrentProfileName;

        }

        void ReloadProfile()
        {
            //switch profiles
            Logger.Info(ControlsProfile.SelectedValue as string + " - Profile now in use");
            GlobalSettings.CurrentProfileName = ControlsProfile.SelectedValue as string;

            //redraw UI
            ReloadInputBindings();
            ReloadProfileSettings();

            CurrentProfile.Content = GlobalSettings.CurrentProfileName;
        }

        private void InitInput()
        {
            InputManager = new InputDeviceManager(this, ToggleOverlay);

            InitSettingsProfiles();

            ControlsProfile.SelectionChanged += OnProfileDropDownChanged;

            RadioStartTransmitEffect.SelectionChanged += OnRadioStartTransmitEffectChanged;
            RadioEndTransmitEffect.SelectionChanged += OnRadioEndTransmitEffectChanged;

            IntercomStartTransmitEffect.SelectionChanged += OnIntercomStartTransmitEffectChanged;
            IntercomEndTransmitEffect.SelectionChanged += OnIntercomEndTransmitEffectChanged;

            Radio1.InputName = Properties.Resources.InputRadio1;
            Radio1.InputDeviceManager = InputManager;

            Radio2.InputName = Properties.Resources.InputRadio2;
            Radio2.InputDeviceManager = InputManager;

            Radio3.InputName = Properties.Resources.InputRadio3;
            Radio3.InputDeviceManager = InputManager;

            PTT.InputName = Properties.Resources.InputPTT;
            PTT.InputDeviceManager = InputManager;

            Intercom.InputName = Properties.Resources.InputIntercom;
            Intercom.InputDeviceManager = InputManager;

            IntercomPTT.InputName = Properties.Resources.InputIntercomPTT;
            IntercomPTT.InputDeviceManager = InputManager;

            RadioOverlay.InputName = Properties.Resources.InputRadioOverlay;
            RadioOverlay.InputDeviceManager = InputManager;

            AwacsOverlayToggle.InputName = Properties.Resources.InputAwacsOverlay;
            AwacsOverlayToggle.InputDeviceManager = InputManager;

            Radio4.InputName = Properties.Resources.InputRadio4;
            Radio4.InputDeviceManager = InputManager;

            Radio5.InputName = Properties.Resources.InputRadio5;
            Radio5.InputDeviceManager = InputManager;

            Radio6.InputName = Properties.Resources.InputRadio6;
            Radio6.InputDeviceManager = InputManager;

            Radio7.InputName = Properties.Resources.InputRadio7;
            Radio7.InputDeviceManager = InputManager;

            Radio8.InputName = Properties.Resources.InputRadio8;
            Radio8.InputDeviceManager = InputManager;

            Radio9.InputName = Properties.Resources.InputRadio9;
            Radio9.InputDeviceManager = InputManager;

            Radio10.InputName = Properties.Resources.InputRadio10;
            Radio10.InputDeviceManager = InputManager;

            Up100.InputName = Properties.Resources.InputUp100;
            Up100.InputDeviceManager = InputManager;

            Up10.InputName = Properties.Resources.InputUp10;
            Up10.InputDeviceManager = InputManager;

            Up1.InputName = Properties.Resources.InputUp1;
            Up1.InputDeviceManager = InputManager;

            Up01.InputName = Properties.Resources.InputUp01;
            Up01.InputDeviceManager = InputManager;

            Up001.InputName = Properties.Resources.InputUp001;
            Up001.InputDeviceManager = InputManager;

            Up0001.InputName = Properties.Resources.InputUp0001;
            Up0001.InputDeviceManager = InputManager;


            Down100.InputName = Properties.Resources.InputDown100;
            Down100.InputDeviceManager = InputManager;

            Down10.InputName = Properties.Resources.InputDown10;
            Down10.InputDeviceManager = InputManager;

            Down1.InputName = Properties.Resources.InputDown1;
            Down1.InputDeviceManager = InputManager;

            Down01.InputName = Properties.Resources.InputDown01;
            Down01.InputDeviceManager = InputManager;

            Down001.InputName = Properties.Resources.InputDown001;
            Down001.InputDeviceManager = InputManager;

            Down0001.InputName = Properties.Resources.InputDown0001;
            Down0001.InputDeviceManager = InputManager;

            ToggleGuard.InputName = Properties.Resources.InputToggleGuard;
            ToggleGuard.InputDeviceManager = InputManager;

            NextRadio.InputName = Properties.Resources.InputNextRadio;
            NextRadio.InputDeviceManager = InputManager;

            PreviousRadio.InputName = Properties.Resources.InputPreviousRadio;
            PreviousRadio.InputDeviceManager = InputManager;

            ToggleEncryption.InputName = Properties.Resources.InputToggleEncryption;
            ToggleEncryption.InputDeviceManager = InputManager;

            EncryptionKeyIncrease.InputName = Properties.Resources.InputEncryptionIncrease;
            EncryptionKeyIncrease.InputDeviceManager = InputManager;

            EncryptionKeyDecrease.InputName = Properties.Resources.InputEncryptionDecrease;
            EncryptionKeyDecrease.InputDeviceManager = InputManager;

            RadioChannelUp.InputName = Properties.Resources.InputRadioChannelUp;
            RadioChannelUp.InputDeviceManager = InputManager;

            RadioChannelDown.InputName = Properties.Resources.InputRadioChannelDown;
            RadioChannelDown.InputDeviceManager = InputManager;

            TransponderIDENT.InputName = Properties.Resources.InputTransponderIDENT;
            TransponderIDENT.InputDeviceManager = InputManager;

            RadioVolumeUp.InputName = Properties.Resources.InputRadioVolumeUp;
            RadioVolumeUp.InputDeviceManager = InputManager;

            RadioVolumeDown.InputName = Properties.Resources.InputRadioVolumeDown;
            RadioVolumeDown.InputDeviceManager = InputManager;
        }

        private void OnProfileDropDownChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ControlsProfile.IsEnabled)
                ReloadProfile();
        }

        private void OnRadioStartTransmitEffectChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioStartTransmitEffect.IsEnabled)
            {
                GlobalSettings.CurrentProfile.RadioTransmissionStartEffectName = ((CachedAudioEffect)RadioStartTransmitEffect.SelectedItem).FileName;
            }
        }

        private void OnRadioEndTransmitEffectChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioEndTransmitEffect.IsEnabled)
            {
                GlobalSettings.CurrentProfile.RadioTransmissionEndEffectName = ((CachedAudioEffect)RadioEndTransmitEffect.SelectedItem).FileName;
            }
        }

        private void OnIntercomStartTransmitEffectChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntercomStartTransmitEffect.IsEnabled)
            {
                GlobalSettings.CurrentProfile.IntercomTransmissionStartEffectName = ((CachedAudioEffect)IntercomStartTransmitEffect.SelectedItem).FileName;
            }
        }

        private void OnIntercomEndTransmitEffectChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntercomEndTransmitEffect.IsEnabled)
            {
                GlobalSettings.CurrentProfile.IntercomTransmissionEndEffectName = ((CachedAudioEffect)IntercomEndTransmitEffect.SelectedItem).FileName;
            }
        }

        private void ReloadInputBindings()
        {
            Radio1.LoadInputSettings();
            Radio2.LoadInputSettings();
            Radio3.LoadInputSettings();
            PTT.LoadInputSettings();
            Intercom.LoadInputSettings();
            IntercomPTT.LoadInputSettings();
            RadioOverlay.LoadInputSettings();
            Radio4.LoadInputSettings();
            Radio5.LoadInputSettings();
            Radio6.LoadInputSettings();
            Radio7.LoadInputSettings();
            Radio8.LoadInputSettings();
            Radio9.LoadInputSettings();
            Radio10.LoadInputSettings();
            Up100.LoadInputSettings();
            Up10.LoadInputSettings();
            Up1.LoadInputSettings();
            Up01.LoadInputSettings();
            Up001.LoadInputSettings();
            Up0001.LoadInputSettings();
            Down100.LoadInputSettings();
            Down10.LoadInputSettings();
            Down1.LoadInputSettings();
            Down01.LoadInputSettings();
            Down001.LoadInputSettings();
            Down0001.LoadInputSettings();
            ToggleGuard.LoadInputSettings();
            NextRadio.LoadInputSettings();
            PreviousRadio.LoadInputSettings();
            ToggleEncryption.LoadInputSettings();
            EncryptionKeyIncrease.LoadInputSettings();
            EncryptionKeyDecrease.LoadInputSettings();
            RadioChannelUp.LoadInputSettings();
            RadioChannelDown.LoadInputSettings();
            RadioVolumeUp.LoadInputSettings();
            RadioVolumeDown.LoadInputSettings();
            AwacsOverlayToggle.LoadInputSettings();
        }

        private void InitToolTips()
        {
            ExternalAWACSModePassword.ToolTip = ToolTips.ExternalAWACSModePassword;
            ExternalAWACSModeName.ToolTip = ToolTips.ExternalAWACSModeName;
            ConnectExternalAWACSMode.ToolTip = ToolTips.ExternalAWACSMode;
        }

        public InputDeviceManager InputManager { get; set; }

        public FavouriteServersViewModel FavouriteServersViewModel { get; }

        public ServerAddress ServerAddress
        {
            get { return _serverAddress; }
            set
            {
                _serverAddress = value;
                if (value != null)
                {
                    ServerIp.Text = value.Address;
                    ExternalAWACSModePassword.Password = string.IsNullOrWhiteSpace(value.EAMCoalitionPassword) ? "" : value.EAMCoalitionPassword;
                }

                _connectCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand ConnectCommand => _connectCommand;

        private void UpdatePlayerLocationAndVUMeters(object sender, EventArgs e)
        {
            if (_audioPreview != null)
            {
                // Only update mic volume output if an audio input device is available - sometimes the value can still change, leaving the user with the impression their mic is working after all
                if (AudioInput.MicrophoneAvailable)
                {
                    Mic_VU.Value = _audioPreview.MicMax;
                }
                Speaker_VU.Value = _audioPreview.SpeakerMax;
            }
            else if (_audioManager != null)
            {
                // Only update mic volume output if an audio input device is available - sometimes the value can still change, leaving the user with the impression their mic is working after all
                if (AudioInput.MicrophoneAvailable)
                {
                    Mic_VU.Value = _audioManager.MicMax;
                }
                Speaker_VU.Value = _audioManager.SpeakerMax;
            }
            else
            {
                Mic_VU.Value = -100;
                Speaker_VU.Value = -100;
            }

            try
            {
                var pos = ClientState.PlayerCoaltionLocationMetadata.LngLngPosition;
                CurrentPosition.Text = $"Lat/Lng: {pos.lat:0.###},{pos.lng:0.###} - Alt: {pos.alt:0}";
                CurrentUnit.Text = $"{ClientState?.DcsPlayerRadioInfo?.unit}";
            }
            catch { }

            ConnectedClientsSingleton.Instance.NotifyAll();

        }

        private void InitSettingsScreen()
        {
            AutoConnectEnabledToggle.IsChecked = GlobalSettings.ClientSettings.AutoConnect;
            AutoConnectPromptToggle.IsChecked = GlobalSettings.ClientSettings.AutoConnectPrompt;
            AutoConnectMismatchPromptToggle.IsChecked = GlobalSettings.ClientSettings.AutoConnectMismatchPrompt;
            RadioOverlayTaskbarItem.IsChecked =
                GlobalSettings.ClientSettings.RadioOverlayTaskbarHide;
            RefocusDCS.IsChecked = GlobalSettings.ClientSettings.RefocusDcs;
            ExpandInputDevices.IsChecked = GlobalSettings.ClientSettings.ExpandControls;

            MinimiseToTray.IsChecked = GlobalSettings.ClientSettings.MinimiseToTray;
            StartMinimised.IsChecked = GlobalSettings.ClientSettings.StartMinimised;

            MicAGC.IsChecked = GlobalSettings.ClientSettings.AutomaticGainControl;
            MicDenoise.IsChecked = GlobalSettings.ClientSettings.Denoise;

            CheckForBetaUpdates.IsChecked = GlobalSettings.ClientSettings.CheckForBetaUpdates;
            PlayConnectionSounds.IsChecked = GlobalSettings.ClientSettings.PlayConnectionSounds;

            RequireAdminToggle.IsChecked = GlobalSettings.ClientSettings.RequireAdmin;

            AutoSelectInputProfile.IsChecked = GlobalSettings.ClientSettings.AutoSelectSettingsProfile;

            VAICOMTXInhibitEnabled.IsChecked = GlobalSettings.ClientSettings.VaicomTxInhibitEnabled;

            ShowTransmitterName.IsChecked = GlobalSettings.ClientSettings.ShowTransmitterName;

            AllowTransmissionsRecord.IsChecked = GlobalSettings.ClientSettings.IsAllowRecordingEnabled;
            RecordTransmissions.IsChecked = GlobalSettings.ClientSettings.IsRecordAudioEnabled;
            SingleFileMixdown.IsChecked = GlobalSettings.ClientSettings.IsSingleFileMixdownEnabled;
            DisallowedAudioTone.IsChecked = GlobalSettings.ClientSettings.IsDisallowedAudioToneEnabled;
            RecordTransmissions_IsEnabled();

            RecordingQuality.IsEnabled = false;
            RecordingQuality.ValueChanged += RecordingQuality_ValueChanged;
            RecordingQuality.Value = GlobalSettings.ClientSettings.RecordingQuality;
            RecordingQuality.IsEnabled = true;

            var objValue = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut", "FALSE");
            if (objValue == null || (string)objValue == "TRUE")
            {
                AllowAnonymousUsage.IsChecked = false;
            }
            else
            {
                AllowAnonymousUsage.IsChecked = true;
            }

            VOXEnabled.IsChecked = GlobalSettings.ClientSettings.IsVoxEnabled;

            VOXMode.IsEnabled = false;
            VOXMode.Value = GlobalSettings.ClientSettings.VoxMode;
            VOXMode.ValueChanged += VOXMode_ValueChanged;
            VOXMode.IsEnabled = true;

            VOXMinimimumTXTime.IsEnabled = false;
            VOXMinimimumTXTime.Value = GlobalSettings.ClientSettings.VoxMinimumTime;
            VOXMinimimumTXTime.ValueChanged += VOXMinimumTime_ValueChanged;
            VOXMinimimumTXTime.IsEnabled = true;

            VOXMinimumRMS.IsEnabled = false;
            VOXMinimumRMS.Value = GlobalSettings.ClientSettings.VoxMinimumDb;
            VOXMinimumRMS.ValueChanged += VOXMinimumRMS_ValueChanged;
            VOXMinimumRMS.IsEnabled = true;

            AllowXInputController.IsChecked = GlobalSettings.ClientSettings.AllowXInputController;
        }

        private void ReloadProfileSettings()
        {
            RadioEncryptionEffectsToggle.IsChecked = GlobalSettings.CurrentProfile.RadioEncryptionEffects;
            RadioSwitchIsPTT.IsChecked = GlobalSettings.CurrentProfile.RadioSwitchIsPtt;

            RadioTxStartToggle.IsChecked = GlobalSettings.CurrentProfile.IsRadioTxStartEffectsEnabled;
            RadioTxEndToggle.IsChecked = GlobalSettings.CurrentProfile.IsRadioTxEndEffectsEnabled;

            RadioRxStartToggle.IsChecked = GlobalSettings.CurrentProfile.IsRadioRxStartEffectsEnabled;
            RadioRxEndToggle.IsChecked = GlobalSettings.CurrentProfile.IsRadioRxEndEffectsEnabled;

            RadioMIDSToggle.IsChecked = GlobalSettings.CurrentProfile.MidsRadioEffect;

            RadioSoundEffects.IsChecked = GlobalSettings.CurrentProfile.RadioEffects;
            RadioSoundEffectsClipping.IsChecked = GlobalSettings.CurrentProfile.RadioEffectsClipping;
            NATORadioToneToggle.IsChecked = GlobalSettings.CurrentProfile.NatoFmTone;
            HQEffectToggle.IsChecked = GlobalSettings.CurrentProfile.IsHaveQuickToneEnabled;
            BackgroundRadioNoiseToggle.IsChecked = GlobalSettings.CurrentProfile.IsRadioBackgroundNoiseEffectEnabled;

            AutoSelectChannel.IsChecked = GlobalSettings.CurrentProfile.AutoSelectPresetChannel;

            AlwaysAllowHotas.IsChecked = GlobalSettings.CurrentProfile.AlwaysAllowHotasControls;
            AllowDCSPTT.IsChecked = GlobalSettings.CurrentProfile.AllowDcsPtt;
            AllowRotaryIncrement.IsChecked = GlobalSettings.CurrentProfile.RotaryStyleIncrement;
            AlwaysAllowTransponderOverlay.IsChecked = GlobalSettings.CurrentProfile.AlwaysAllowTransponderOverlay;

            //disable to set without triggering onchange
            PTTReleaseDelay.IsEnabled = false;
            PTTReleaseDelay.ValueChanged += PushToTalkReleaseDelay_ValueChanged;
            PTTReleaseDelay.Value = GlobalSettings.CurrentProfile.PttReleaseDelay;
            PTTReleaseDelay.IsEnabled = true;

            DisableExpansionRadios.IsChecked = GlobalSettings.CurrentProfile.IsExpansionRadiosDisabled;

            PTTStartDelay.IsEnabled = false;
            PTTStartDelay.ValueChanged += PushToTalkStartDelay_ValueChanged;
            PTTStartDelay.Value = GlobalSettings.CurrentProfile.PttStartDelay;
            PTTStartDelay.IsEnabled = true;

            RadioEndTransmitEffect.IsEnabled = false;
            RadioEndTransmitEffect.ItemsSource = CachedAudioEffectProvider.Instance.RadioTransmissionEnd;
            RadioEndTransmitEffect.SelectedItem = CachedAudioEffectProvider.Instance.SelectedRadioTransmissionEndEffect;
            RadioEndTransmitEffect.IsEnabled = true;

            RadioStartTransmitEffect.IsEnabled = false;
            RadioStartTransmitEffect.SelectedIndex = 0;
            RadioStartTransmitEffect.ItemsSource = CachedAudioEffectProvider.Instance.RadioTransmissionStart;
            RadioStartTransmitEffect.SelectedItem = CachedAudioEffectProvider.Instance.SelectedRadioTransmissionStartEffect;
            RadioStartTransmitEffect.IsEnabled = true;

            IntercomStartTransmitEffect.IsEnabled = false;
            IntercomStartTransmitEffect.ItemsSource = CachedAudioEffectProvider.Instance.IntercomTransmissionStart;
            IntercomStartTransmitEffect.SelectedItem = CachedAudioEffectProvider.Instance.SelectedIntercomTransmissionStartEffect;
            IntercomStartTransmitEffect.IsEnabled = true;

            IntercomEndTransmitEffect.IsEnabled = false;
            IntercomEndTransmitEffect.SelectedIndex = 0;
            IntercomEndTransmitEffect.ItemsSource = CachedAudioEffectProvider.Instance.IntercomTransmissionEnd;
            IntercomEndTransmitEffect.SelectedItem = CachedAudioEffectProvider.Instance.SelectedIntercomTransmissionEndEffect;
            IntercomEndTransmitEffect.IsEnabled = true;

            NATOToneVolume.IsEnabled = false;
            NATOToneVolume.ValueChanged += (sender, e) =>
            {
                if (NATOToneVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.NatoFmToneVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.NatoFmToneVolume = (float)vol;
                }

            };
            NATOToneVolume.Value = (GlobalSettings.CurrentProfile.NatoFmToneVolume
                                    / GlobalSettings.CurrentProfile.NatoFmToneVolume) * 100;
            NATOToneVolume.IsEnabled = true;

            HQToneVolume.IsEnabled = false;
            HQToneVolume.ValueChanged += (sender, e) =>
            {
                if (HQToneVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.HaveQuickToneVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.HaveQuickToneVolume = (float)vol;
                }

            };
            HQToneVolume.Value = (GlobalSettings.CurrentProfile.HaveQuickToneVolume
                                  / GlobalSettings.CurrentProfile.HaveQuickToneVolume) * 100;
            HQToneVolume.IsEnabled = true;

            FMEffectVolume.IsEnabled = false;
            FMEffectVolume.ValueChanged += (sender, e) =>
            {
                if (FMEffectVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.FmNoiseVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.FmNoiseVolume = (float)vol;
                }

            };
            FMEffectVolume.Value = (GlobalSettings.CurrentProfile.FmNoiseVolume
                                    / GlobalSettings.CurrentProfile.FmNoiseVolume) * 100;
            FMEffectVolume.IsEnabled = true;

            VHFEffectVolume.IsEnabled = false;
            VHFEffectVolume.ValueChanged += (sender, e) =>
            {
                if (VHFEffectVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.VhfNoiseVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.VhfNoiseVolume = (float)vol;
                }

            };
            VHFEffectVolume.Value = (GlobalSettings.CurrentProfile.VhfNoiseVolume
                                     / GlobalSettings.CurrentProfile.VhfNoiseVolume) * 100;
            VHFEffectVolume.IsEnabled = true;

            UHFEffectVolume.IsEnabled = false;
            UHFEffectVolume.ValueChanged += (sender, e) =>
            {
                if (UHFEffectVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.UhfNoiseVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.UhfNoiseVolume = (float)vol;
                }

            };
            UHFEffectVolume.Value = (GlobalSettings.CurrentProfile.UhfNoiseVolume
                                     / GlobalSettings.CurrentProfile.UhfNoiseVolume) * 100;
            UHFEffectVolume.IsEnabled = true;

            HFEffectVolume.IsEnabled = false;
            HFEffectVolume.ValueChanged += (sender, e) =>
            {
                if (HFEffectVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.HfNoiseVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.HfNoiseVolume = (float)vol;
                }

            };
            HFEffectVolume.Value = (GlobalSettings.CurrentProfile.HfNoiseVolume
                                    / GlobalSettings.CurrentProfile.HfNoiseVolume) * 100;
            HFEffectVolume.IsEnabled = true;


            AmbientCockpitEffectToggle.IsChecked = GlobalSettings.CurrentProfile.IsAmbientCockpitNoiseEffectEnabled;
            AmbientIntercomEffectToggle.IsChecked = GlobalSettings.CurrentProfile.IsAmbientCockpitIntercomNoiseEffectEnabled;

            AmbientCockpitEffectVolume.IsEnabled = false;
            AmbientCockpitEffectVolume.ValueChanged += (sender, e) =>
            {
                if (AmbientCockpitEffectVolume.IsEnabled)
                {
                    var orig = GlobalSettings.CurrentProfile.AmbientCockpitNoiseEffectVolume;

                    var vol = orig * (e.NewValue / 100);

                    GlobalSettings.CurrentProfile.AmbientCockpitNoiseEffectVolume = (float)vol;
                }

            };
            AmbientCockpitEffectVolume.Value = (GlobalSettings.CurrentProfile.AmbientCockpitNoiseEffectVolume
                                                / GlobalSettings.CurrentProfile.AmbientCockpitNoiseEffectVolume) * 100;
            AmbientCockpitEffectVolume.IsEnabled = true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Connect()
        {
            if (ClientState.IsConnected)
            {
                Stop();
            }
            else
            {
                SaveSelectedInputAndOutput();

                try
                {
                    //process hostname
                    var resolvedAddresses = Dns.GetHostAddresses(GetAddressFromTextBox());
                    var ip = resolvedAddresses.FirstOrDefault(xa => xa.AddressFamily == AddressFamily.InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

                    if (ip != null)
                    {
                        _resolvedIp = ip;
                        _port = GetPortFromTextBox();

                        try
                        {
                            _client?.Disconnect();
                        }
                        catch (Exception ex)
                        {
                        }

                        if (_client == null)
                        {
                            _client = new SRSClientSyncHandler(_guid, UpdateUICallback, delegate(string name, int seat)
                            {
                                try
                                {
                                    //on MAIN thread
                                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                        new ThreadStart(() =>
                                        {
                                            //Handle Aircraft Name - find matching profile and select if you can
                                            name = Regex.Replace(name.Trim().ToLower(), "[^a-zA-Z0-9]", "");
                                            //add one to seat so seat_2 is copilot
                                            var nameSeat = $"_{seat + 1}";

                                            foreach (var profileName in GlobalSettings.ProfileNames)
                                            {
                                                //find matching seat
                                                var splitName = profileName.Trim().ToLowerInvariant().Split('_')
                                                    .First();
                                                if (name.StartsWith(Regex.Replace(splitName, "[^a-zA-Z0-9]", "")) &&
                                                    profileName.Trim().EndsWith(nameSeat))
                                                {
                                                    ControlsProfile.SelectedItem = profileName;
                                                    return;
                                                }
                                            }

                                            foreach (var profileName in GlobalSettings.ProfileNames)
                                            {
                                                //find matching seat
                                                if (name.StartsWith(Regex.Replace(profileName.Trim().ToLower(),
                                                        "[^a-zA-Z0-9_]", "")))
                                                {
                                                    ControlsProfile.SelectedItem = profileName;
                                                    return;
                                                }
                                            }

                                            ControlsProfile.SelectedIndex = 0;

                                        }));
                                }
                                catch (Exception)
                                {
                                }

                            });
                        }

                        _client.TryConnect(new IPEndPoint(_resolvedIp, _port), ConnectCallback);

                        StartStop.Content = Properties.Resources.StartStopConnecting;
                        StartStop.IsEnabled = false;
                        Mic.IsEnabled = false;
                        Speakers.IsEnabled = false;
                        MicOutput.IsEnabled = false;
                        Preview.IsEnabled = false;

                        if (_audioPreview != null)
                        {
                            Preview.Content = Properties.Resources.PreviewAudio;
                            _audioPreview.StopEncoding();
                            _audioPreview = null;
                        }
                    }
                    else
                    {
                        //invalid ID
                        MessageBox.Show(Properties.Resources.MsgBoxInvalidIPText, Properties.Resources.MsgBoxInvalidIP, MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        ClientState.IsConnected = false;
                        ToggleServerSettings.IsEnabled = false;
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    MessageBox.Show(Properties.Resources.MsgBoxInvalidIPText, Properties.Resources.MsgBoxInvalidIP, MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ClientState.IsConnected = false;
                    ToggleServerSettings.IsEnabled = false;
                }
            }
        }

        private string GetAddressFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (addr.Contains(":"))
            {
                return addr.Split(':')[0];
            }

            return addr;
        }

        private int GetPortFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (addr.Contains(":"))
            {
                int port;
                if (int.TryParse(addr.Split(':')[1], out port))
                {
                    return port;
                }
                throw new ArgumentException("specified port is not valid");
            }

            return 5002;
        }

        private void Stop(bool connectionError = false)
        {
            if (ClientState.IsConnected && GlobalSettings.ClientSettings.PlayConnectionSounds)
            {
                try
                {
                    Sounds.BeepDisconnected.Play();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to play disconnect sound");
                }
            }

            ClientState.IsConnectionErrored = connectionError;

            StartStop.Content = Properties.Resources.StartStop;
            StartStop.IsEnabled = true;
            Mic.IsEnabled = true;
            Speakers.IsEnabled = true;
            MicOutput.IsEnabled = true;
            Preview.IsEnabled = true;
            ClientState.IsConnected = false;
            ToggleServerSettings.IsEnabled = false;

            ConnectExternalAWACSMode.IsEnabled = false;
            ConnectExternalAWACSMode.Content = Properties.Resources.ConnectExternalAWACSMode;

            if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
                GlobalSettings.ClientSettings.LastSeenName != ClientState.LastSeenName)
            {
                GlobalSettings.ClientSettings.LastSeenName = ClientState.LastSeenName;
            }

            try
            {
                _audioManager.StopEncoding();
            }
            catch (Exception)
            {
            }

            _client?.Disconnect();

            ClientState.DcsPlayerRadioInfo.Reset();
            ClientState.PlayerCoaltionLocationMetadata.Reset();
        }

        private void SaveSelectedInputAndOutput()
        {
            //save app settings
            // Only save selected microphone if one is actually available, resulting in a crash otherwise
            if (AudioInput.MicrophoneAvailable)
            {
                if (AudioInput.SelectedAudioInput.Value == null)
                {
                    GlobalSettings.ClientSettings.AudioInputDeviceId = "default";

                }
                else
                {
                    var input = ((MMDevice)AudioInput.SelectedAudioInput.Value).ID;
                    GlobalSettings.ClientSettings.AudioInputDeviceId = input;
                }
            }

            if (AudioOutput.SelectedAudioOutput.Value == null)
            {
                GlobalSettings.ClientSettings.AudioOutputDeviceId = "default";
            }
            else
            {
                var output = ((MMDevice)AudioOutput.SelectedAudioOutput.Value).ID;
                GlobalSettings.ClientSettings.AudioOutputDeviceId = output;
            }

            //check if we have optional output
            if (AudioOutput.SelectedMicAudioOutput.Value != null)
            {
                var micOutput = (MMDevice)AudioOutput.SelectedMicAudioOutput.Value;
                GlobalSettings.ClientSettings.SideToneDeviceId = micOutput.ID;
            }
            else
            {
                GlobalSettings.ClientSettings.SideToneDeviceId = "";
            }

            ShowMicPassthroughWarning();
        }

        private void ShowMicPassthroughWarning()
        {
            if (GlobalSettings.ClientSettings.SideToneDeviceId.Equals(GlobalSettings.ClientSettings.AudioOutputDeviceId))
            {
                MessageBox.Show(Properties.Resources.MsgBoxMicPassthruText, Properties.Resources.MsgBoxMicPassthru, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ConnectCallback(bool result, bool connectionError, string connection)
        {
            string currentConnection = ServerIp.Text.Trim();
            if (!currentConnection.Contains(":"))
            {
                currentConnection += ":5002";
            }

            if (result)
            {
                if (!ClientState.IsConnected)
                {
                    try
                    {
                        StartStop.Content = Properties.Resources.StartStopDisconnect;
                        StartStop.IsEnabled = true;

                        ClientState.IsConnected = true;
                        ClientState.IsVoipConnected = false;

                        if (GlobalSettings.ClientSettings.PlayConnectionSounds)
                        {
                            try
                            {
                                Sounds.BeepConnected.Play();
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, "Failed to play connect sound");
                            }
                        }

                        GlobalSettings.ClientSettings.LastServer = ServerIp.Text;

                        _audioManager.StartEncoding(_guid, InputManager,
                            _resolvedIp, _port);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,
                            "Unable to get audio device - likely output device error - Pick another. Error:" +
                            ex.Message);
                        Stop();

                        var messageBoxResult = CustomMessageBox.ShowYesNo(
                            Properties.Resources.MsgBoxAudioErrorText,
                            Properties.Resources.MsgBoxAudioError,
                            "OPEN PRIVACY SETTINGS",
                            "JOIN DISCORD SERVER",
                            MessageBoxImage.Error);

                        if (messageBoxResult == MessageBoxResult.Yes) Process.Start("https://discord.gg/baw7g3t");
                    }
                }
            }
            else if (string.Equals(currentConnection, connection, StringComparison.OrdinalIgnoreCase))
            {
                // Only stop connection/reset state if connection is currently active
                // Autoconnect mismatch will quickly disconnect/reconnect, leading to double-callbacks
                Stop(connectionError);
            }
            else
            {
                if (!ClientState.IsConnected)
                {
                    Stop(connectionError);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            GlobalSettings.ClientSettings.ClientX = Left;
            GlobalSettings.ClientSettings.ClientY = Top;

            if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
                GlobalSettings.ClientSettings.LastSeenName != ClientState.LastSeenName)
            {
                GlobalSettings.ClientSettings.LastSeenName = ClientState.LastSeenName;
            }

            //save window position
            base.OnClosing(e);

            //stop timer
            _updateTimer?.Stop();

            Stop();

            _audioPreview?.StopEncoding();
            _audioPreview = null;

            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            _awacsRadioOverlay?.Close();
            _awacsRadioOverlay = null;

            _dcsAutoConnectListener?.Stop();
            _dcsAutoConnectListener = null;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && GlobalSettings.ClientSettings.MinimiseToTray)
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void PreviewAudio(object sender, RoutedEventArgs e)
        {
            if (_audioPreview == null)
            {
                if (!AudioInput.MicrophoneAvailable)
                {
                    Logger.Info("Unable to preview audio, no valid audio input device available or selected");
                    return;
                }

                //get device
                try
                {
                    SaveSelectedInputAndOutput();

                    _audioPreview = new AudioPreview();
                    _audioPreview.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost.Value);
                    _audioPreview.StartPreview(AudioOutput.WindowsN);

                    Preview.Content = Properties.Resources.PreviewAudioStop;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        "Unable to preview audio - likely output device error - Pick another. Error:" + ex.Message);

                }
            }
            else
            {
                Preview.Content = Preview.Content = Properties.Resources.PreviewAudio;
                _audioPreview.StopEncoding();
                _audioPreview = null;
            }
        }

        private void UpdateUICallback()
        {
            if (ClientState.IsConnected)
            {
                ToggleServerSettings.IsEnabled = true;

                bool eamEnabled = GlobalSettings.CurrentServerSettings.IsExternalModeAllowed;

                ConnectExternalAWACSMode.IsEnabled = eamEnabled;
                ConnectExternalAWACSMode.Content = ClientState.ExternalAWACSModelSelected ? Properties.Resources.DisconnectExternalAWACSMode : Properties.Resources.ConnectExternalAWACSMode;
            }
            else
            {
                ToggleServerSettings.IsEnabled = false;
                ConnectExternalAWACSMode.IsEnabled = false;
                ConnectExternalAWACSMode.Content = Properties.Resources.ConnectExternalAWACSMode;
            }
        }

        private void SpeakerBoost_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var convertedValue = VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost.Value);

            if (_audioPreview != null)
            {
                _audioPreview.SpeakerBoost = convertedValue;
            }
            if (_audioManager != null)
            {
                _audioManager.SpeakerBoost = convertedValue;
            }

            GlobalSettings.ClientSettings.SpeakerBoost = SpeakerBoost.Value;


            if ((SpeakerBoostLabel != null) && (SpeakerBoost != null))
            {
                SpeakerBoostLabel.Content = VolumeConversionHelper.ConvertLinearDiffToDB(convertedValue);
            }
        }

        private void RadioEncryptionEffects_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.RadioEncryptionEffects = (bool)RadioEncryptionEffectsToggle.IsChecked;
        }

        private void NATORadioTone_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.NatoFmTone = (bool)NATORadioToneToggle.IsChecked;
        }

        private void RadioSwitchPTT_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.RadioSwitchIsPtt = (bool)RadioSwitchIsPTT.IsChecked;
        }

        private void ShowOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleOverlay(true, false);
        }

        private void ToggleOverlay(bool uiButton, bool awacs)
        {
            //debounce show hide (1 tick = 100ns, 6000000 ticks = 600ms debounce)
            if ((DateTime.Now.Ticks - _toggleShowHide > 6000000) || uiButton)
            {
                _toggleShowHide = DateTime.Now.Ticks;

                if (awacs)
                {
                    ShowAwacsOverlay_OnClick(null, null);
                }
                else
                {
                    if ((_radioOverlayWindow == null) || !_radioOverlayWindow.IsVisible ||
                        (_radioOverlayWindow.WindowState == WindowState.Minimized))
                    {
                        //hide awacs panel
                        _awacsRadioOverlay?.Close();
                        _awacsRadioOverlay = null;

                        _radioOverlayWindow?.Close();

                        _radioOverlayWindow = new Overlay.RadioOverlayWindow();


                        _radioOverlayWindow.ShowInTaskbar =
                            !GlobalSettings.ClientSettings.RadioOverlayTaskbarHide;
                        _radioOverlayWindow.Show();
                    }
                    else
                    {
                        _radioOverlayWindow?.Close();
                        _radioOverlayWindow = null;
                    }
                }
                
            }
        }

        private void ShowAwacsOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_awacsRadioOverlay == null) || !_awacsRadioOverlay.IsVisible ||
                (_awacsRadioOverlay.WindowState == WindowState.Minimized))
            {
                //close normal overlay
                _radioOverlayWindow?.Close();
                _radioOverlayWindow = null;

                _awacsRadioOverlay?.Close();

                _awacsRadioOverlay = new AwacsRadioOverlayWindow.RadioOverlayWindow();
                _awacsRadioOverlay.ShowInTaskbar =
                    !GlobalSettings.ClientSettings.RadioOverlayTaskbarHide;
                _awacsRadioOverlay.Show();
            }
            else
            {
                _awacsRadioOverlay?.Close();
                _awacsRadioOverlay = null;
            }
        }

        private void AutoConnect(string address, int port)
        {
            string connection = $"{address}:{port}";

            Logger.Info($"Received AutoConnect DCS-SRS @ {connection}");

            var enabled = GlobalSettings.ClientSettings.AutoConnect;

            if (!enabled)
            {
                Logger.Info($"Ignored Autoconnect - not Enabled");
                return;
            }

            if (ClientState.IsConnected)
            {
                // Always show prompt about active/advertised SRS connection mismatch if client is already connected
                string[] currentConnectionParts = ServerIp.Text.Trim().Split(':');
                string currentAddress = currentConnectionParts[0];
                int currentPort = 5002;
                if (currentConnectionParts.Length >= 2)
                {
                    if (!int.TryParse(currentConnectionParts[1], out currentPort))
                    {
                        Logger.Warn($"Failed to parse port {currentConnectionParts[1]} of current connection, falling back to 5002 for autoconnect comparison");
                        currentPort = 5002;
                    }
                }
                string currentConnection = $"{currentAddress}:{currentPort}";

                if (string.Equals(address, currentAddress, StringComparison.OrdinalIgnoreCase) && port == currentPort)
                {
                    // Current connection matches SRS server advertised by DCS, all good
                    Logger.Info($"Current SRS connection {currentConnection} matches advertised server {connection}, ignoring autoconnect");
                    return;
                }
                else if (port != currentPort)
                {
                    // Port mismatch, will always be a different server, no need to perform hostname lookups
                    HandleAutoConnectMismatch(currentConnection, connection);
                    return;
                }

                // Perform DNS lookup of advertised and current hostnames to find hostname/resolved IP matches
                List<string> currentIPs = new List<string>();

                if (IPAddress.TryParse(currentAddress, out IPAddress currentIP))
                {
                    currentIPs.Add(currentIP.ToString());
                }
                else
                {
                    try
                    {
                        foreach (IPAddress ip in Dns.GetHostAddresses(currentConnectionParts[0]))
                        {
                            // SRS currently only supports IPv4 (due to address/port parsing)
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                            {
                                currentIPs.Add(ip.ToString());
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, $"Failed to resolve current SRS host {currentConnectionParts[0]} to IP addresses, ignoring autoconnect advertisement");
                    }
                }

                if (currentIPs.Count == 0)
                {
                    Logger.Warn( $"Failed to resolve current SRS host {currentConnectionParts[0]} to IP addresses, ignoring autoconnect advertisement");
                    return;
                }

                List<string> advertisedIPs = new List<string>();

                if (IPAddress.TryParse(address, out IPAddress advertisedIP))
                {
                    advertisedIPs.Add(advertisedIP.ToString());
                }
                else
                {
                    try
                    {
                        foreach (IPAddress ip in Dns.GetHostAddresses(connection))
                        {
                            // SRS currently only supports IPv4 (due to address/port parsing)
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                            {
                                advertisedIPs.Add(ip.ToString());
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, $"Failed to resolve advertised SRS host {address} to IP addresses, ignoring autoconnect advertisement");
                        return;
                    }
                }

                if (!currentIPs.Intersect(advertisedIPs).Any())
                {
                    // No resolved IPs match, display mismatch warning
                    HandleAutoConnectMismatch(currentConnection, connection);
                }
            }
            else
            {
                // Show auto connect prompt if client is not connected yet and setting has been enabled, otherwise automatically connect
                bool showPrompt = GlobalSettings.ClientSettings.AutoConnectPrompt;

                bool connectToServer = !showPrompt;
                if (GlobalSettings.ClientSettings.AutoConnectPrompt)
                {
                    WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                    var result = MessageBox.Show(this,
                        $"{Properties.Resources.MsgBoxAutoConnectText} {address}:{port}? ", "Auto Connect",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    connectToServer = (result == MessageBoxResult.Yes) && (StartStop.Content.ToString().ToLower() == "connect");
                }

                if (connectToServer)
                {
                    ServerIp.Text = connection;
                    Connect();
                }
            }
        }

        private async void HandleAutoConnectMismatch(string currentConnection, string advertisedConnection)
        {
            // Show auto connect mismatch prompt if setting has been enabled (default), otherwise automatically switch server
            bool showPrompt = GlobalSettings.ClientSettings.AutoConnectMismatchPrompt;

            Logger.Info($"Current SRS connection {currentConnection} does not match advertised server {advertisedConnection}, {(showPrompt ? "displaying mismatch prompt" : "automatically switching server")}");

            bool switchServer = !showPrompt;
            if (showPrompt)
            {
                WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                var result = MessageBox.Show(this,
                    $"{Properties.Resources.MsgBoxMismatchText1} {advertisedConnection} {Properties.Resources.MsgBoxMismatchText2} {currentConnection} {Properties.Resources.MsgBoxMismatchText3}\n\n" +
                    $"{Properties.Resources.MsgBoxMismatchText4}",
                    Properties.Resources.MsgBoxMismatch,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                switchServer = result == MessageBoxResult.Yes;
            }

            if (switchServer)
            {
                Stop();

                StartStop.IsEnabled = false;
                StartStop.Content = Properties.Resources.StartStopConnecting;
                await Task.Delay(2000);
                StartStop.IsEnabled = true;
                ServerIp.Text = advertisedConnection;
                Connect();
            }
        }

        private void ResetRadioWindow_Click(object sender, RoutedEventArgs e)
        {
            //close overlay
            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            GlobalSettings.ClientSettings.RadioX = 300;
            GlobalSettings.ClientSettings.RadioY = 300;

            GlobalSettings.ClientSettings.RadioWidth = 122;
            GlobalSettings.ClientSettings.RadioHeight = 270;

            GlobalSettings.ClientSettings.RadioOpacity = 1.0;
        }

        private void ToggleServerSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_serverSettingsWindow == null) || !_serverSettingsWindow.IsVisible ||
                (_serverSettingsWindow.WindowState == WindowState.Minimized))
            {
                _serverSettingsWindow?.Close();

                _serverSettingsWindow = new ServerSettingsWindow();
                _serverSettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _serverSettingsWindow.Owner = this;
                _serverSettingsWindow.Show();
            }
            else
            {
                _serverSettingsWindow?.Close();
                _serverSettingsWindow = null;
            }
        }



        private void AutoConnectToggle_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.AutoConnect = (bool)AutoConnectEnabledToggle.IsChecked;
        }

        private void AutoConnectPromptToggle_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.AutoConnectPrompt = (bool)AutoConnectPromptToggle.IsChecked;
        }

        private void AutoConnectMismatchPromptToggle_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.AutoConnectMismatchPrompt = (bool)AutoConnectMismatchPromptToggle.IsChecked;
        }

        private void RadioOverlayTaskbarItem_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.RadioOverlayTaskbarHide = (bool)RadioOverlayTaskbarItem.IsChecked;

            if (_radioOverlayWindow != null)
                _radioOverlayWindow.ShowInTaskbar = !GlobalSettings.ClientSettings.RadioOverlayTaskbarHide;
            else if (_awacsRadioOverlay != null) _awacsRadioOverlay.ShowInTaskbar = !GlobalSettings.ClientSettings.RadioOverlayTaskbarHide;
        }

        private void DCSRefocus_OnClick_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.RefocusDcs = (bool)RefocusDCS.IsChecked;
        }

        private void ExpandInputDevices_OnClick_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                Properties.Resources.MsgBoxRestartExpandText,
                Properties.Resources.MsgBoxRestart, MessageBoxButton.OK,
                MessageBoxImage.Warning);

            GlobalSettings.ClientSettings.ExpandControls = (bool)ExpandInputDevices.IsChecked;
        }

        private void AllowXInputController_OnClick_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                Properties.Resources.MsgBoxRestartXInputText,
                Properties.Resources.MsgBoxRestart, MessageBoxButton.OK,
                MessageBoxImage.Warning);

            GlobalSettings.ClientSettings.AllowXInputController = (bool)AllowXInputController.IsChecked;
        }

        private void LaunchAddressTab(object sender, RoutedEventArgs e)
        {
            TabControl.SelectedItem = FavouritesSeversTab;
        }

        private void MicAGC_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.AutomaticGainControl = (bool)MicAGC.IsChecked;
        }

        private void MicDenoise_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.Denoise = (bool)MicDenoise.IsChecked;
        }

        private void RadioSoundEffects_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.RadioEffects = (bool)RadioSoundEffects.IsChecked;
        }

        private void RadioTxStart_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsRadioTxStartEffectsEnabled = (bool)RadioTxStartToggle.IsChecked;
        }

        private void RadioTxEnd_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsRadioTxEndEffectsEnabled = (bool)RadioTxEndToggle.IsChecked;
        }

        private void RadioRxStart_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsRadioRxStartEffectsEnabled = (bool)RadioRxStartToggle.IsChecked;
        }

        private void RadioRxEnd_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsRadioRxEndEffectsEnabled = (bool)RadioRxEndToggle.IsChecked;
        }

        private void RadioMIDS_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.MidsRadioEffect = (bool)RadioMIDSToggle.IsChecked;
        }

        private void AudioSelectChannel_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.AutoSelectPresetChannel = (bool)AutoSelectChannel.IsChecked;
        }

        private void RadioSoundEffectsClipping_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.RadioEffectsClipping = (bool)RadioSoundEffectsClipping.IsChecked;

        }

        private void MinimiseToTray_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.MinimiseToTray = (bool)MinimiseToTray.IsChecked;
        }

        private void StartMinimised_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.StartMinimised = (bool)StartMinimised.IsChecked;
        }

        private void AllowDCSPTT_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.AllowDcsPtt = (bool)AllowDCSPTT.IsChecked;
        }

        private void AllowRotaryIncrement_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.RotaryStyleIncrement = (bool)AllowRotaryIncrement.IsChecked;
        }

        private void AlwaysAllowHotas_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.AlwaysAllowHotasControls = (bool)AlwaysAllowHotas.IsChecked;
        }

        private void CheckForBetaUpdates_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.CheckForBetaUpdates = (bool)CheckForBetaUpdates.IsChecked;
        }

        private void PlayConnectionSounds_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.PlayConnectionSounds = (bool)PlayConnectionSounds.IsChecked;
        }

        private void ConnectExternalAWACSMode_OnClick(object sender, RoutedEventArgs e)
        {
            if (_client == null ||
                !ClientState.IsConnected || !GlobalSettings.CurrentServerSettings.IsExternalModeAllowed ||
                (!ClientState.ExternalAWACSModelSelected &&
                string.IsNullOrWhiteSpace(ExternalAWACSModePassword.Password)))
            {
                return;
            }

            // Already connected, disconnect
            if (ClientState.ExternalAWACSModelSelected)
            {
                _client.DisconnectExternalAWACSMode();
            }
            else if (!ClientState.IsGameExportConnected) //only if we're not in game
            {
                ClientState.LastSeenName = ExternalAWACSModeName.Text;
                _client.ConnectExternalAWACSMode(ExternalAWACSModePassword.Password.Trim(), ExternalAWACSModeConnectionChanged);
            }
        }

        private void ExternalAWACSModeConnectionChanged(bool result, int coalition)
        {
            if (result)
            {
                ClientState.ExternalAWACSModelSelected = true;
                ClientState.PlayerCoaltionLocationMetadata.side = coalition;
                ClientState.PlayerCoaltionLocationMetadata.name = ClientState.LastSeenName;
                ClientState.DcsPlayerRadioInfo.name = ClientState.LastSeenName;

                ConnectExternalAWACSMode.Content = Properties.Resources.DisconnectExternalAWACSMode;
            }
            else
            {
                ClientState.ExternalAWACSModelSelected = false;
                ClientState.PlayerCoaltionLocationMetadata.side = 0;
                ClientState.PlayerCoaltionLocationMetadata.name = "";
                ClientState.DcsPlayerRadioInfo.name = "";
                ClientState.DcsPlayerRadioInfo.LastUpdate = 0;
                ClientState.LastSent = 0;

                ConnectExternalAWACSMode.Content = Properties.Resources.ConnectExternalAWACSMode;
                ExternalAWACSModePassword.IsEnabled = GlobalSettings.CurrentServerSettings.IsExternalModeAllowed;
                ExternalAWACSModeName.IsEnabled = GlobalSettings.CurrentServerSettings.IsExternalModeAllowed;
            }
        }

        private void RescanInputDevices(object sender, RoutedEventArgs e)
        {
            InputManager.InitDevices();
            MessageBox.Show(this,
                Properties.Resources.MsgBoxRescanText,
                Properties.Resources.MsgBoxRescan,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void SetSRSPath_Click(object sender, RoutedEventArgs e)
        {
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRPathStandalone", Directory.GetCurrentDirectory());

            MessageBox.Show(this,
                Properties.Resources.MsgBoxSetSRSPathText + Directory.GetCurrentDirectory(),
                Properties.Resources.MsgBoxSetSRSPath,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RequireAdminToggle_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.RequireAdmin = (bool)RequireAdminToggle.IsChecked;
            MessageBox.Show(this,
                Properties.Resources.MsgBoxAdminText,
                Properties.Resources.MsgBoxAdmin, MessageBoxButton.OK, MessageBoxImage.Warning);

        }

        private void CreateProfile(object sender, RoutedEventArgs e)
        {
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        GlobalSettings.CreateProfileCommand.Execute(name);
                        InitSettingsProfiles();

                    }
                });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = this;
            inputProfileWindow.ShowDialog();
        }

        private void DeleteProfile(object sender, RoutedEventArgs e)
        {
            var current = ControlsProfile.SelectedValue as string;

            if (current.Equals("default"))
            {
                MessageBox.Show(this,
                    Properties.Resources.MsgBoxErrorInputText,
                    Properties.Resources.MsgBoxError,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var result = MessageBox.Show(this,
                    $"{Properties.Resources.MsgBoxConfirmDeleteText} {current} ?",
                    Properties.Resources.MsgBoxConfirm,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ControlsProfile.SelectedIndex = 0;
                    GlobalSettings.DeleteProfileCommand.Execute(current);
                    InitSettingsProfiles();
                }

            }

        }

        private void RenameProfile(object sender, RoutedEventArgs e)
        {

            var current = ControlsProfile.SelectedValue as string;
            if (current.Equals("default"))
            {
                MessageBox.Show(this,
                    Properties.Resources.MsgBoxErrorRenameText,
                    Properties.Resources.MsgBoxError,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var oldName = current;
                var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        GlobalSettings.RenameProfileCommand.Execute(name);
                        InitSettingsProfiles();
                    }
                }, true, oldName);
                inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                inputProfileWindow.Owner = this;
                inputProfileWindow.ShowDialog();
            }

        }

        private void UpdatePresetsFolderLabel()
        {
            var presetsFolder = GlobalSettings.ClientSettings.LastPresetsFolder;
            if (!string.IsNullOrWhiteSpace(presetsFolder))
            {
                PresetsFolderLabel.Content = Path.GetFileName(presetsFolder);
                PresetsFolderLabel.ToolTip = presetsFolder;
            }
            else
            {
                PresetsFolderLabel.Content = "(default)";
                PresetsFolderLabel.ToolTip = Directory.GetCurrentDirectory();
            }
        }

        private void AutoSelectInputProfile_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.AutoSelectSettingsProfile = (bool)AutoSelectInputProfile.IsChecked;
        }

        private void CopyProfile(object sender, RoutedEventArgs e)
        {
            var current = ControlsProfile.SelectedValue as string;
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    GlobalSettings.DuplicateProfileCommand.Execute(name);
                    InitSettingsProfiles();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = this;
            inputProfileWindow.ShowDialog();
        }

        private void VAICOMTXInhibit_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.VaicomTxInhibitEnabled = (bool)VAICOMTXInhibitEnabled.IsChecked;
        }

        private void AlwaysAllowTransponderOverlay_OnClick(object sender, RoutedEventArgs e)
        {

            GlobalSettings.CurrentProfile.AlwaysAllowTransponderOverlay = (bool)AlwaysAllowTransponderOverlay.IsChecked;
        }

        private void CurrentPosition_OnClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var pos = ClientState.PlayerCoaltionLocationMetadata.LngLngPosition;

                Process.Start($"https://maps.google.com/maps?q=loc:{pos.lat},{pos.lng}");
            }
            catch { }

        }

        private void ShowClientList_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_clientListWindow == null) || !_clientListWindow.IsVisible ||
                (_clientListWindow.WindowState == WindowState.Minimized))
            {
                _clientListWindow?.Close();

                _clientListWindow = new ClientListWindow();
                _clientListWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _clientListWindow.Owner = this;
                _clientListWindow.Show();
            }
            else
            {
                _clientListWindow?.Close();
                _clientListWindow = null;
            }
        }

        private void ShowTransmitterName_OnClick_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.ShowTransmitterName = (bool)ShowTransmitterName.IsChecked;
        }

        private void PushToTalkReleaseDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PTTReleaseDelay.IsEnabled) GlobalSettings.CurrentProfile.PttReleaseDelay = (float)e.NewValue;
        }

        private void PushToTalkStartDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PTTStartDelay.IsEnabled) GlobalSettings.CurrentProfile.PttStartDelay = (float)e.NewValue;
        }

        private void Donate_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(
                    "https://www.patreon.com/ciribob");
            }
            catch (Exception)
            {
            }
        }

        private void BackgroundRadioNoiseToggle_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsRadioBackgroundNoiseEffectEnabled = (bool)BackgroundRadioNoiseToggle.IsChecked;
        }

        private void HQEffect_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsHaveQuickToneEnabled = (bool)HQEffectToggle.IsChecked;
        }

        private void AllowAnonymousUsage_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(bool)AllowAnonymousUsage.IsChecked)
            {
                MessageBox.Show(
                    Properties.Resources.MsgBoxPleaseTickText,
                    Properties.Resources.MsgBoxPleaseTick, MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut", "TRUE");
            }
            else
            {
                MessageBox.Show(
                    Properties.Resources.MsgBoxThankYouText,
                    Properties.Resources.MsgBoxThankYou, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut", "FALSE");
            }
        }

        private void AllowTransmissionsRecord_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.IsAllowRecordingEnabled = (bool)AllowTransmissionsRecord.IsChecked;
        }

        private void RecordTransmissions_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.IsRecordAudioEnabled = (bool)RecordTransmissions.IsChecked;
            RecordTransmissions_IsEnabled();
        }

        private void RecordTransmissions_IsEnabled()
        {
            if ((bool)RecordTransmissions.IsChecked)
            {
                SingleFileMixdown.IsEnabled = false;
                RecordingQuality.IsEnabled = false;
            }
            else
            {
                SingleFileMixdown.IsEnabled = true;
                RecordingQuality.IsEnabled = true;
            }
        }

        private void SingleFileMixdown_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.IsSingleFileMixdownEnabled = (bool)SingleFileMixdown.IsChecked;
        }

        private void RecordingQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            GlobalSettings.ClientSettings.RecordingQuality = (int)e.NewValue;
        }

        private void DisallowedAudioTone_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.IsDisallowedAudioToneEnabled = (bool)DisallowedAudioTone.IsChecked;
        }

        private void VoxEnabled_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.IsVoxEnabled = (bool)VOXEnabled.IsChecked;
        }

        private void VOXMode_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(VOXMode.IsEnabled) GlobalSettings.ClientSettings.VoxMode = (int)e.NewValue;
        }

        private void VOXMinimumTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VOXMinimimumTXTime.IsEnabled)
                GlobalSettings.ClientSettings.VoxMinimumTime = (int)e.NewValue;
        }

        private void VOXMinimumRMS_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VOXMinimumRMS.IsEnabled)
                GlobalSettings.ClientSettings.VoxMinimumDb = (double)e.NewValue;
        }

        private void AmbientCockpitEffectToggle_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsAmbientCockpitNoiseEffectEnabled = (bool)AmbientCockpitEffectToggle.IsChecked;
        }

        private void AmbientCockpitEffectIntercomToggle_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsAmbientCockpitIntercomNoiseEffectEnabled = (bool)AmbientIntercomEffectToggle.IsChecked;
        }

        private void DisableExpansionRadios_OnClick(object sender, RoutedEventArgs e)
        {
            GlobalSettings.CurrentProfile.IsExpansionRadiosDisabled = (bool)DisableExpansionRadios.IsChecked;
        }

        private void PresetsFolderBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var selectPresetsFolder = new System.Windows.Forms.FolderBrowserDialog();
            selectPresetsFolder.SelectedPath = PresetsFolderLabel.ToolTip.ToString();
            if (selectPresetsFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                GlobalSettings.ClientSettings.LastPresetsFolder = selectPresetsFolder.SelectedPath;
                UpdatePresetsFolderLabel();
            }
        }

        private void PresetsFolderResetButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettings.ClientSettings.LastPresetsFolder = string.Empty;
            UpdatePresetsFolderLabel();
        }
    }
}