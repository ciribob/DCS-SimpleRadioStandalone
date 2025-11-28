using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Properties;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NAudio.CoreAudioApi;
using NLog;
using AwaRadioOverlayWindow =
    Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.AwacsRadioOverlayWindow.AwaRadioOverlayWindow;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;

public class MainWindowViewModel : PropertyChangedBaseClass, IHandle<TCPClientStatusMessage>,
    IHandle<VOIPStatusMessage>, IHandle<ProfileChangedMessage>, IHandle<EAMConnectedMessage>,
    IHandle<EAMDisconnectMessage>, IHandle<ServerSettingsUpdatedMessage>, IHandle<AutoConnectMessage>, IHandle<ToogleAwacsRadioOverlayMessage>, IHandle<ToggleSingleStackRadioOverlayMessage>
{
    private static readonly long OVERLAY_DEBOUNCE = 500;
    private readonly AudioManager _audioManager;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    private readonly DispatcherTimer _updateTimer;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private AudioPreview _audioPreview;
    private AwaRadioOverlayWindow _awacsRadioOverlay;
    private TCPClientHandler _client;

    private ClientListWindow _clientListWindow;
    private DCSRadioSyncManager _dcsManager;
    private ServerAddress _selectedServerAddress;

    private ServerSettingsWindow.ServerSettingsWindow _serverSettingsWindow;

    private RadioOverlayWindow.RadioOverlayWindow _singleRadioOverlay;

    public MainWindowViewModel()
    {
        _audioManager = new AudioManager(AudioOutput.WindowsN);

        PositionClickCommand = new DelegateCommand(() =>
        {
            var pos = ClientState.PlayerCoaltionLocationMetadata.LngLngPosition;

            try
            {
                Process.Start(new ProcessStartInfo($"https://maps.google.com/maps?q=loc:{pos.lat},{pos.lng}")
                    { UseShellExecute = true });
            }
            catch (Exception)
            {
                // ignored
            }
        });
        PreviewCommand = new DelegateCommand(PreviewAudio);

        ConnectCommand = new DelegateCommand(Connect);

        TrayIconCommand = new DelegateCommand(() =>
        {
            App.Current.MainWindow.Show();
            App.Current.MainWindow.WindowState = WindowState.Normal;
        });

        SingleStackOverlayCommand = new DelegateCommand(SingleRadioStackOverlay);

        AwacsRadioOverlayCommand = new DelegateCommand(MultiRadioOverlay);

        TrayIconQuitCommand = new DelegateCommand(() => { App.Current.Shutdown(); });

        //TODO might not need to do this - should be triggered by notifyproperty
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _updateTimer.Tick += UpdatePlayerCountAndVUMeters;
        _updateTimer.Start();

        ClientStateSingleton.Instance.DcsPlayerRadioInfo.name = EAMName;

        EventBus.Instance.SubscribeOnUIThread(this);

        ServerSettingsCommand = new DelegateCommand(ToggleServerSettings);

        ClientListCommand = new DelegateCommand(ToggleClientList);

        EAMConnectCommand = new DelegateCommand(() =>
        {
            if (ClientStateSingleton.Instance.ExternalAWACSModeConnected)
            {
                EventBus.Instance.PublishOnUIThreadAsync(new EAMDisconnectMessage());
            }
            else
            {
                EventBus.Instance.PublishOnBackgroundThreadAsync(new EAMConnectRequestMessage()
                {
                    Password = EAMPassword,
                    Name = EAMName
                });
            }
        });

        DonateCommand = new DelegateCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.patreon.com/ciribob")
                    { UseShellExecute = true });
            }
            catch (Exception)
            {
                // ignored
            }
        });
    }


    public ICommand ClientListCommand { get; set; }

    public ICommand ServerSettingsCommand { get; set; }

    public DelegateCommand AwacsRadioOverlayCommand { get; set; }

    public ClientStateSingleton ClientState { get; } = ClientStateSingleton.Instance;
    public ConnectedClientsSingleton Clients { get; } = ConnectedClientsSingleton.Instance;
    public AudioInputSingleton AudioInput { get; } = AudioInputSingleton.Instance;
    public AudioOutputSingleton AudioOutput { get; } = AudioOutputSingleton.Instance;

    public InputDeviceManager InputManager { get; set; }

    public bool IsEAMAvailable => ClientStateSingleton.Instance.IsConnected &&
                                  SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys
                                      .EXTERNAL_AWACS_MODE);

    public bool IsConnected { get; set; }

    //WPF cant invert a binding - this controls the input box for address
    public bool IsNotConnected => !IsConnected;

    public bool IsVoIPConnected { get; set; }

    public DelegateCommand TrayIconQuitCommand { get; set; }

    public DelegateCommand TrayIconCommand { get; set; }

    public DelegateCommand ConnectCommand { get; set; }

    public ICommand PreviewCommand { get; set; }

    public ICommand SingleStackOverlayCommand { get; set; }

    public bool PreviewEnabled => AudioInput.MicrophoneAvailable && !IsConnected;

    public float SpeakerVU
    {
        get
        {
            if (_audioPreview != null && _audioPreview.IsPreviewing)
                return _audioPreview.SpeakerMax;
            if (_audioManager != null) return _audioManager.SpeakerMax;

            return -100;
        }
    }

    public float MicVU
    {
        get
        {
            if (_audioPreview != null && _audioPreview.IsPreviewing)
                return _audioPreview.MicMax;
            if (_audioManager != null) return _audioManager.MicMax;

            return -100;
        }
    }


    public string PreviewText
    {
        get
        {
            if (_audioPreview == null || !_audioPreview.IsPreviewing || IsConnected)
                return "preview audio";
            return "stop preview";
        }
    }

    public string ConnectText
    {
        get
        {
            if (IsConnected)
                return Resources.StartStopDisconnect;
            return Resources.StartStop;
        }
    }

    public double SpeakerBoost
    {
        get
        {
            var boost = _globalSettings.GetClientSetting(GlobalSettingsKeys.SpeakerBoost).DoubleValue;
            _audioManager.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)boost);
            if (_audioPreview != null) _audioPreview.SpeakerBoost = _audioManager.SpeakerBoost;
            return boost;
        }
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SpeakerBoost,
                value.ToString(CultureInfo.InvariantCulture));
            _audioManager.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)value);

            if (_audioPreview != null) _audioPreview.SpeakerBoost = _audioManager.SpeakerBoost;
            // NotifyPropertyChanged();
            // NotifyPropertyChanged("SpeakerBoostText");
        }
    }

    public string SpeakerBoostText =>
        VolumeConversionHelper.ConvertLinearDiffToDB(
            VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost));

    public ServerAddress SelectedServerAddress
    {
        get => _selectedServerAddress;
        set
        {
            ServerAddress = value.Address;
            EAMPassword = value.EAMCoalitionPassword;

            _selectedServerAddress = value;
        }
    }

    public string EAMPassword { get; set; }

    public string ServerAddress
    {
        get
        {
            var savedAddress = _globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer);

            if (savedAddress == null)
                return "127.0.0.1:5002";
            return savedAddress.RawValue;
        }
        set
        {
            if (value != null)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.LastServer, value);
                NotifyPropertyChanged();
            }
        }
    }

    public string EAMName
    {
        get { return ClientState.LastSeenName; }
        set
        {
            if (value != null)
            {
                ClientState.LastSeenName = value;
            }
        }
    }

    public bool AudioSettingsEnabled
    {
        get
        {
            if ((_audioPreview != null && _audioPreview.IsPreviewing) || IsConnected) return false;

            return true;
        }
    }

    public String CurrentProfile
    {
        get { return _globalSettings.ProfileSettingsStore.CurrentProfileName; }
    }

    public bool ConnectIsEnabled { get; set; } = true;
    public FavouriteServersViewModel FavouriteServersViewModel { get; set; }

    public string CurrentUnit => ClientState.DcsPlayerRadioInfo.unit;

    public string LastKnownPosition =>
        ClientState.PlayerCoaltionLocationMetadata.LngLngPosition.ToString();

    public DelegateCommand PositionClickCommand { get; }
    public DelegateCommand EAMConnectCommand { get; }
    public DelegateCommand DonateCommand { get; }

    public string EAMConnectButtonText
    {
        get
        {
            if (ClientStateSingleton.Instance.ExternalAWACSModeConnected)
            {
                return Resources.DisconnectExternalAWACSMode;
            }

            return Resources.ConnectExternalAWACSMode;
        }
    }

    public async Task HandleAsync(AutoConnectMessage message, CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            if (ServerAddress == message.Address)
            {
                Logger.Info(
                    $"Ignoring auto connect message as we are already connected to the server {message.Address}");
            }
            else
            {
                // Show auto connect mismatch prompt if setting has been enabled (default), otherwise automatically switch server
                bool showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);

                bool switchServer = !showPrompt;
                if (showPrompt)
                {
                    WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                    var result = System.Windows.MessageBox.Show(App.Current.MainWindow,
                        $"{Resources.MsgBoxMismatchText1} {message.Address} {Resources.MsgBoxMismatchText2} {ServerAddress} {Resources.MsgBoxMismatchText3}\n\n" +
                        $"{Resources.MsgBoxMismatchText4}",
                        Resources.MsgBoxMismatch,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    switchServer = result == MessageBoxResult.Yes;
                }

                if (switchServer)
                {
                    Logger.Info($"Switching from {ServerAddress} to {message.Address}");
                    ConnectIsEnabled = false;
                    ServerAddress = message.Address;
                    Stop();
                    ConnectIsEnabled = false;
                    await Task.Delay(2000);
                    Connect();
                }
            }
        }
        else
        {
            Logger.Info($"Received auto connect message for {message.Address} - Connecting");
            ServerAddress = message.Address;
            Connect();
        }
    }

    public Task HandleAsync(EAMConnectedMessage message, CancellationToken cancellationToken)
    {
        ClientStateSingleton.Instance.LastSeenName = EAMName;

        //Notify after it started - hack
        Task.Run(async delegate
        {
            await Task.Delay(100);

            NotifyPropertyChanged(nameof(EAMConnectButtonText));
        });

        return Task.CompletedTask;
    }

    public Task HandleAsync(EAMDisconnectMessage message, CancellationToken cancellationToken)
    {
        ClientStateSingleton.Instance.ExternalAWACSModelSelected = false;
        NotifyPropertyChanged(nameof(EAMConnectButtonText));
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProfileChangedMessage message, CancellationToken cancellationToken)
    {
        NotifyPropertyChanged(nameof(CurrentProfile));
        return Task.CompletedTask;
    }

    public Task HandleAsync(ServerSettingsUpdatedMessage message, CancellationToken cancellationToken)
    {
        NotifyPropertyChanged(nameof(IsEAMAvailable));

        return Task.CompletedTask;
    }

    public async Task HandleAsync(TCPClientStatusMessage obj, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            if (obj.Connected)
            {
                ConnectIsEnabled = true;
                //TODO dont let this trigger again
                IsConnected = true;
                //connection sound
                if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
                    try
                    {
                        Sounds.BeepConnected.Play();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to play connect sound");
                    }

                if (obj.Address != null)
                    StartAudio(obj.Address);
                else
                    Logger.Error("TCPClientStatusMessage - Connect sent without address and safely ignored.");

                _dcsManager?.Stop();

                _dcsManager = new DCSRadioSyncManager(ClientStateSingleton.Instance.ShortGUID);
                _dcsManager.Start();
            }
            else
            {
                //disconnect sound
                Stop(obj.Error);
            }

            NotifyPropertyChanged(nameof(IsEAMAvailable));
            NotifyPropertyChanged(nameof(EAMConnectButtonText));
        });
    }

    public Task HandleAsync(VOIPStatusMessage message, CancellationToken cancellationToken)
    {
        IsVoIPConnected = message.Connected;
        return Task.CompletedTask;
    }

    private void UpdatePlayerCountAndVUMeters(object sender, EventArgs e)
    {
        NotifyPropertyChanged(nameof(SpeakerVU));
        NotifyPropertyChanged(nameof(MicVU));
        NotifyPropertyChanged(nameof(CurrentUnit));
        NotifyPropertyChanged(nameof(LastKnownPosition));

        if (ClientStateSingleton.Instance.IsConnected)
            NotifyPropertyChanged(nameof(EAMName));

        ConnectedClientsSingleton.Instance.NotifyAll();
    }

    public void Connect()
    {
        if (IsConnected)
        {
            ConnectIsEnabled = false;
            Stop();
        }
        else
        {
            ConnectIsEnabled = false;
            //stop preview
            _audioPreview?.StopEncoding();
            _audioPreview = null;

            IsConnected = true;
            SaveSelectedInputAndOutput();

            try
            {
                //process hostname
                var resolvedAddresses = Dns.GetHostAddresses(GetAddressFromTextBox());
                var ip = resolvedAddresses.FirstOrDefault(xa =>
                    xa.AddressFamily ==
                    AddressFamily
                        .InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

                if (ip != null)
                {
                    var resolvedIp = ip;
                    var port = GetPortFromTextBox();


                    _client = new TCPClientHandler(ClientState.ShortGUID,
                        new SRClientBase
                        {
                            LatLngPosition = new LatLngPosition(),
                            AllowRecord =
                                GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.AllowRecording),
                            ClientGuid = ClientStateSingleton.Instance.ShortGUID,
                            Coalition = 0,
                            Name = ClientStateSingleton.Instance.LastSeenName,
                            RadioInfo = ClientState.DcsPlayerRadioInfo.ConvertToRadioBase(),
                            Seat = 0
                        });
                    _client.TryConnect(new IPEndPoint(resolvedIp, port));
                }
                else
                {
                    //invalid ID
                    System.Windows.MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    IsConnected = false;
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
            {
                System.Windows.MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                IsConnected = false;
            }
        }

        ConnectIsEnabled = true;
    }

    private void Stop(TCPClientStatusMessage.ErrorCode connectionError = TCPClientStatusMessage.ErrorCode.TIMEOUT)
    {
        if (IsConnected && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
            try
            {
                Sounds.BeepDisconnected.Play();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to play disconnect sound");
            }

        IsConnected = false;

        try
        {
            _audioManager.StopEncoding();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to stop enconding audio");
        }


        _dcsManager?.Stop();
        _dcsManager = null;

        _client?.Disconnect();
        _client = null;

        ClientState.DcsPlayerRadioInfo.Reset();
        ClientState.PlayerCoaltionLocationMetadata.Reset();
        ConnectIsEnabled = true;
    }

    private string GetAddressFromTextBox()
    {
        var addr = ServerAddress.Trim();

        if (addr.Contains(":")) return addr.Split(':')[0];

        return addr;
    }

    private int GetPortFromTextBox()
    {
        var addr = ServerAddress.Trim();

        if (addr.Contains(":"))
        {
            int port;
            if (int.TryParse(addr.Split(':')[1], out port)) return port;

            throw new ArgumentException("specified port is  valid");
        }

        return 5002;
    }


    private void SaveSelectedInputAndOutput()
    {
        //save app settings
        // Only save selected microphone if one is actually available, resulting in a crash otherwise
        if (AudioInput.MicrophoneAvailable)
        {
            if (AudioInput.SelectedAudioInput.Value == null)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, "default");
            }
            else
            {
                var input = ((MMDevice)AudioInput.SelectedAudioInput.Value).ID;
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, input);
            }
        }

        if (AudioOutput.SelectedAudioOutput.Value == null)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId, "default");
        }
        else
        {
            var output = (MMDevice)AudioOutput.SelectedAudioOutput.Value;
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId, output.ID);
        }

        //check if we have optional output
        if (AudioOutput.SelectedMicAudioOutput.Value != null)
        {
            var micOutput = (MMDevice)AudioOutput.SelectedMicAudioOutput.Value;
            _globalSettings.SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, micOutput.ID);
        }
        else
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, "");
        }

        ShowMicPassthroughWarning();
    }

    private void ShowMicPassthroughWarning()
    {
        if (_globalSettings.GetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId).RawValue
            .Equals(_globalSettings.GetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId).RawValue))
            System.Windows.MessageBox.Show(
                "Mic Output and Speaker Output should not be set to the same device!\n\nMic Output is just for recording and not for use as a sidetone. You will hear yourself with a small delay!\n\nHit disconnect and change Mic Output / Passthrough",
                "Warning", MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }

    private void PreviewAudio()
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
                _audioPreview.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost);
                _audioPreview.StartPreview(AudioOutput.WindowsN);
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "Unable to preview audio - likely output device error - Pick another. Error:" + ex.Message);
            }
        }
        else
        {
            _audioPreview.StopEncoding();
            _audioPreview = null;
        }

        NotifyPropertyChanged(nameof(PreviewText));
        NotifyPropertyChanged(nameof(AudioSettingsEnabled));
    }

    private void StartAudio(IPEndPoint endPoint)
    {
        //Must be main thread
        App.Current.Dispatcher.Invoke(delegate
        {
            try
            {
                _audioManager.StartEncoding(ClientState.ShortGUID, InputManager, endPoint);
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "Unable to get audio device - likely output device error - Pick another. Error:" +
                    ex.Message);
                Stop();

                var audioOutputErrorDialog = TaskDialog.ShowDialog(new TaskDialogPage
                {
                    Caption = "Audio Output Error",
                    Heading = "Problem initialising Audio Output!",
                    Text = "Try a different Output device and check privacy settings\n\nIf the problem persists, disable ALL other outputs and restart DCS SRS",
                    Icon = TaskDialogIcon.Error,
                    Buttons =
                    {
                        new TaskDialogButton
                        {
                            Text = "Close",
                            Tag = 3
                        }
                    }
                });
            }
        });
    }

    private void SingleRadioStackOverlay()
    {
        ToggleSingleRadioStack();
    }

    private void ToggleSingleRadioStack()
    {
        if (_singleRadioOverlay == null || !_singleRadioOverlay.IsVisible ||
            _singleRadioOverlay.WindowState == WindowState.Minimized)
        {
            //hide awacs panel
            _awacsRadioOverlay?.Close();
            _awacsRadioOverlay = null;

            _singleRadioOverlay?.Close();

            _singleRadioOverlay = new RadioOverlayWindow.RadioOverlayWindow();


            _singleRadioOverlay.ShowInTaskbar =
                !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
            _singleRadioOverlay.Show();
        }
        else
        {
            _singleRadioOverlay?.Close();
            _singleRadioOverlay = null;
        }
    }

    private void MultiRadioOverlay()
    {
        ToggleMultiRadioOverlay();
    }

    private void ToggleMultiRadioOverlay()
    {
        if (_awacsRadioOverlay == null || !_awacsRadioOverlay.IsVisible ||
            _awacsRadioOverlay.WindowState == WindowState.Minimized)
        {
            //close normal overlay
            _singleRadioOverlay?.Close();
            _singleRadioOverlay = null;

            _awacsRadioOverlay?.Close();

            _awacsRadioOverlay = new AwaRadioOverlayWindow();
            _awacsRadioOverlay.ShowInTaskbar =
                !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
            _awacsRadioOverlay.Show();
        }
        else
        {
            _awacsRadioOverlay?.Close();
            _awacsRadioOverlay = null;
        }
    }

    private void ToggleServerSettings()
    {
        if (_serverSettingsWindow == null || !_serverSettingsWindow.IsVisible ||
            _serverSettingsWindow.WindowState == WindowState.Minimized)
        {
            _serverSettingsWindow?.Close();

            _serverSettingsWindow = new ServerSettingsWindow.ServerSettingsWindow();
            _serverSettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _serverSettingsWindow.Owner = App.Current.MainWindow;
            _serverSettingsWindow.Show();
        }
        else
        {
            _serverSettingsWindow?.Close();
            _serverSettingsWindow = null;
        }
    }

    private void ToggleClientList()
    {
        if (_clientListWindow == null || !_clientListWindow.IsVisible ||
            _clientListWindow.WindowState == WindowState.Minimized)
        {
            _clientListWindow?.Close();

            _clientListWindow = new ClientListWindow();
            _clientListWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _clientListWindow.Owner = App.Current.MainWindow;
            _clientListWindow.Show();
        }
        else
        {
            _clientListWindow?.Close();
            _clientListWindow = null;
        }
    }


    public void OnClosing()
    {
        //stop timer
        _updateTimer?.Stop();

        _client?.Disconnect();
        _client = null;

        Stop();

        _audioPreview?.StopEncoding();
        _audioPreview = null;

        _singleRadioOverlay?.Close();
        _singleRadioOverlay = null;

        _awacsRadioOverlay?.Close();
        _awacsRadioOverlay = null;

        _clientListWindow?.Close();
        _clientListWindow = null;

        _serverSettingsWindow?.Close();
        _serverSettingsWindow = null;
    }

    private long lastAwacsToggleTime;
    private long lastRadioToggleTime;
    public Task HandleAsync(ToogleAwacsRadioOverlayMessage message, CancellationToken cancellationToken)
    {
        if (TimeSpan.FromTicks(DateTime.Now.Ticks - lastAwacsToggleTime).TotalMilliseconds > OVERLAY_DEBOUNCE)
        {
            lastAwacsToggleTime = DateTime.Now.Ticks;
            //Debounce
            // Even though it should be the UI thread - it wasnt working so this forces the UI / STA thread
            App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(MultiRadioOverlay));
        }
     
        return Task.CompletedTask;
    }

    public Task HandleAsync(ToggleSingleStackRadioOverlayMessage message, CancellationToken cancellationToken)
    {
        if (TimeSpan.FromTicks(DateTime.Now.Ticks - lastRadioToggleTime).TotalMilliseconds > OVERLAY_DEBOUNCE)
        {
            lastRadioToggleTime = DateTime.Now.Ticks;
            // Even though it should be the UI thread - it wasnt working so this forces the UI / STA thread
            App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(ToggleSingleRadioStack));
        }

        return Task.CompletedTask;
    }
}