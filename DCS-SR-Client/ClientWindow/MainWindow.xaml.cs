using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Overlay;
using MahApps.Metro.Controls;
using NAudio.Wave;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly AppConfiguration _appConfig;

        private readonly AudioManager _audioManager;
        private AudioPreview _audioPreview;
        private ClientSync _client;

        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();

        private readonly string _guid;

        public InputDeviceManager InputManager { get; set; }

        private IPAddress _resolvedIp;

        private bool _stop = true;

        public delegate void ToggleOverlayCallback(bool uiButton);

        private ToggleOverlayCallback _toggleOverlayCallback;
        private UI.ServerSettingsWindow _serverSettingsWindow;

        public MainWindow()
        {
            InitializeComponent();

            SetupLogging();

            Logger.Info("Started DCS-SimpleRadio Client");

            _toggleOverlayCallback = ToggleOverlay;

            InputManager = new InputDeviceManager(this, _toggleOverlayCallback);

            Radio1.InputName = "Radio 1";
            Radio1.ControlInputBinding = InputBinding.Switch1;
            Radio1.InputDeviceManager = InputManager;

            Radio2.InputName = "Radio 2";
            Radio2.ControlInputBinding = InputBinding.Switch2;
            Radio2.InputDeviceManager = InputManager;

            Radio3.InputName = "Radio 3";
            Radio3.ControlInputBinding = InputBinding.Switch3;
            Radio3.InputDeviceManager = InputManager;

            PTT.InputName = "Common PTT";
            PTT.ControlInputBinding = InputBinding.Ptt;
            PTT.InputDeviceManager = InputManager;

            Intercom.InputName = "Intercom Select";
            Intercom.ControlInputBinding = InputBinding.Intercom;
            Intercom.InputDeviceManager = InputManager;

            RadioOverlay.InputName = "Overlay Toggle";
            RadioOverlay.ControlInputBinding = InputBinding.OverlayToggle;
            RadioOverlay.InputDeviceManager = InputManager;

            _appConfig = AppConfiguration.Instance;
            _guid = ShortGuid.NewGuid().ToString();

            InitAudioInput();

            InitAudioOutput();

            ServerIp.Text = _appConfig.LastServer;
            MicrophoneBoost.Value = _appConfig.MicBoost;
            SpeakerBoost.Value = _appConfig.SpeakerBoost;

            //   this.boostAmount.Content = "Boost: " + this.microphoneBoost.Value;
            _audioManager = new AudioManager(_clients);
            _audioManager.MicBoost = (float) MicrophoneBoost.Value;
            _audioManager.SpeakerBoost = (float) SpeakerBoost.Value;

            if (BoostLabel != null && MicrophoneBoost != null)
            {
                BoostLabel.Content = ((int)(MicrophoneBoost.Value * 100) - 100) + "%";
            }

            if (SpeakerBoostLabel != null && SpeakerBoost != null)
            {
                SpeakerBoostLabel.Content = ((int)(SpeakerBoost.Value * 100) - 100) + "%";
            }

            UpdaterChecker.CheckForUpdate();

            InitRadioEffectsToggle();

            InitRadioSwitchIsPTT();

            InitRadioClickEffectsToggle();
        }


        private void InitAudioInput()
        {
            for (var i = 0; i < WaveIn.DeviceCount; i++)
            {
                Mic.Items.Add(WaveIn.GetCapabilities(i).ProductName);
            }

            if (WaveIn.DeviceCount >= _appConfig.AudioInputDeviceId && WaveIn.DeviceCount > 0)
            {
                Mic.SelectedIndex = _appConfig.AudioInputDeviceId;
            }
            else if (WaveIn.DeviceCount > 0)
            {
                Mic.SelectedIndex = 0;
            }
        }

        private void InitAudioOutput()
        {
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                Speakers.Items.Add(WaveOut.GetCapabilities(i).ProductName);
            }

            if (WaveOut.DeviceCount >= _appConfig.AudioOutputDeviceId && WaveOut.DeviceCount > 0)
            {
                Speakers.SelectedIndex = _appConfig.AudioOutputDeviceId;
            }
            else if (WaveOut.DeviceCount > 0)
            {
                Speakers.SelectedIndex = 0;
            }
        }

        private void InitRadioEffectsToggle()
        {
            var radioEffects = Settings.Instance.UserSettings[(int) SettingType.RadioEffects];
            if (radioEffects == "ON")
            {
                RadioEffectsToggle.IsChecked = true;
            }
            else
            {
                RadioEffectsToggle.IsChecked = false;
            }
        }

        private void InitRadioClickEffectsToggle()
        {
            var radioEffects = Settings.Instance.UserSettings[(int)SettingType.RadioClickEffects];
            if (radioEffects == "ON")
            {
                RadioClicksToggle.IsChecked = true;
            }
            else
            {
                RadioClicksToggle.IsChecked = false;
            }
        }

        private void InitRadioSwitchIsPTT()
        {
            var switchIsPTT = Settings.Instance.UserSettings[(int)SettingType.RadioSwitchIsPTT];
            if (switchIsPTT == "ON")
            {
                RadioSwitchIsPTT.IsChecked = true;
            }
            else
            {
                RadioSwitchIsPTT.IsChecked = false;
            }
        }

        private void SetupLogging()
        {
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";
            fileTarget.FileName = "${basedir}/clientlog.txt";
            fileTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", LogLevel.Debug, consoleTarget);
            config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;
        }

        private void startStop_Click(object sender, RoutedEventArgs e)
        {
            if (!_stop)
            {
                Stop();
            }
            else
            {
                try
                {
                    //process hostname
                    var ipAddr = Dns.GetHostAddresses(ServerIp.Text.Trim());

                    if (ipAddr.Length > 0)
                    {
                        _resolvedIp = ipAddr[0];

                        _client = new ClientSync(_clients, _guid);
                        _client.TryConnect(new IPEndPoint(_resolvedIp, 5002), ConnectCallback);

                        StartStop.Content = "Connecting...";
                        StartStop.IsEnabled = false;
                        Mic.IsEnabled = false;
                        Speakers.IsEnabled = false;
                    }
                    else
                    {
                        //invalid ID
                        MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void Stop()
        {
            StartStop.Content = "Connect";
            StartStop.IsEnabled = true;
            Mic.IsEnabled = true;
            Speakers.IsEnabled = true;
            try
            {
                _audioManager.StopEncoding();
            }
            catch (Exception ex)
            {
            }

            _stop = true;

            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }
        }

        private void ConnectCallback(bool result)
        {
            if (result)
            {
                if (_stop)
                {
                    StartStop.Content = "Disconnect";
                    StartStop.IsEnabled = true;

                    //save app settings
                    _appConfig.LastServer = ServerIp.Text.Trim();
                    _appConfig.AudioInputDeviceId = Mic.SelectedIndex;
                    _appConfig.AudioOutputDeviceId = Speakers.SelectedIndex;

                    _audioManager.StartEncoding(Mic.SelectedIndex, Speakers.SelectedIndex, _guid, InputManager,
                        _resolvedIp);
                    _stop = false;
                }
            }
            else
            {
                Stop();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            Stop();

            if (_audioPreview != null)
            {
                _audioPreview.StopEncoding();
                _audioPreview = null;
            }

            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;
        }

        private void PreviewAudio(object sender, RoutedEventArgs e)
        {
            if (_audioPreview == null)
            {
                _audioPreview = new AudioPreview();
                _audioPreview.StartPreview(Mic.SelectedIndex, Speakers.SelectedIndex);
                _audioPreview.Volume.Volume = (float) MicrophoneBoost.Value * (float)SpeakerBoost.Value;
                Preview.Content = "Stop Preview";
            }
            else
            {
                Preview.Content = "Audio Preview";
                _audioPreview.StopEncoding();
                _audioPreview = null;
            }
        }

        private void MicrophoneBoost_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioPreview != null)
            {
                _audioPreview.Volume.Volume = (float) MicrophoneBoost.Value * (float)SpeakerBoost.Value;
            }
            if (_audioManager != null)
            {
                _audioManager.MicBoost = (float) MicrophoneBoost.Value;
            }
            if (_appConfig != null)
            {
                _appConfig.MicBoost = (float) MicrophoneBoost.Value;
            }

            if (BoostLabel != null && MicrophoneBoost!=null)
            {
                BoostLabel.Content = ((int)(MicrophoneBoost.Value * 100) -100)  + "%";
            }
         
        }

        private void SpeakerBoost_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioPreview != null)
            {
                _audioPreview.Volume.Volume = (float)MicrophoneBoost.Value * (float)SpeakerBoost.Value;
            }
            if (_audioManager != null)
            {
                _audioManager.SpeakerBoost = (float)SpeakerBoost.Value;
            }
            if (_appConfig != null)
            {
                _appConfig.SpeakerBoost = (float)SpeakerBoost.Value;
            }

            if (SpeakerBoostLabel != null && SpeakerBoost != null)
            {
                SpeakerBoostLabel.Content = ((int)(SpeakerBoost.Value * 100) - 100) + "%";
            }

        }

        private void RadioEffects_Click(object sender, RoutedEventArgs e)
        {
            Settings.Instance.WriteSetting(SettingType.RadioEffects, (string) RadioEffectsToggle.Content);
        }

        private void RadioClicks_Click(object sender, RoutedEventArgs e)
        {
            Settings.Instance.WriteSetting(SettingType.RadioClickEffects, (string)RadioClicksToggle.Content);
        }

        private void RadioSwitchPTT_Click(object sender, RoutedEventArgs e)
        {
            Settings.Instance.WriteSetting(SettingType.RadioSwitchIsPTT, (string)RadioSwitchIsPTT.Content);
        }

        private RadioOverlayWindow _radioOverlayWindow;

        private void ShowOverlay_OnClick(object sender, RoutedEventArgs e)
        {
           ToggleOverlay(true);
        }
        
        //used to debounce toggle
        private double _toggleShowHide = 0;

        private void ToggleOverlay(bool uiButton)
        {
            //debounce show hide
            if ((Environment.TickCount - _toggleShowHide > 600) || uiButton)
            {
                _toggleShowHide = Environment.TickCount;
                if (_radioOverlayWindow == null || !_radioOverlayWindow.IsVisible || _radioOverlayWindow.WindowState == WindowState.Minimized)
                {
                    _radioOverlayWindow?.Close();

                    _radioOverlayWindow = new RadioOverlayWindow();
                    _radioOverlayWindow.Show();
                }
                else
                {
                    _radioOverlayWindow?.Close();
                    _radioOverlayWindow = null;
                }
            }
           
        }

        private void ResetRadioWindow_Click(object sender, RoutedEventArgs e)
        {
            //close overlay
            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            AppConfiguration.Instance.RadioX = 100;
            AppConfiguration.Instance.RadioY = 100;

            AppConfiguration.Instance.RadioWidth = 122;
            AppConfiguration.Instance.RadioHeight = 270;

            AppConfiguration.Instance.RadioOpacity = 1.0;
        }

        private void ToggleServerSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (_serverSettingsWindow == null || !_serverSettingsWindow.IsVisible || _serverSettingsWindow.WindowState == WindowState.Minimized)
            {
                _serverSettingsWindow?.Close();

                _serverSettingsWindow = new ServerSettingsWindow();
                _serverSettingsWindow.ShowDialog();
              
            }
            else
            {
                _serverSettingsWindow?.Close();
                _serverSettingsWindow = null;
            }
        }

      
    }

}
