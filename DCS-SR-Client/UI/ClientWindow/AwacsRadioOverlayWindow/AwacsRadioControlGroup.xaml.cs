using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.AwacsRadioOverlayWindow.InstructorMode;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.AwacsRadioOverlayWindow;

/// <summary>
///     Interaction logic for RadioControlGroup.xaml
/// </summary>
public partial class RadioControlGroup : UserControl
{
    private const int MaxSimultaneousTransmissions = 3;
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;
    private bool _dragging;

    private int _radioId;


    public RadioControlGroup()
    {
        DataContext = this; // set data context

        InitializeComponent();

        RadioFrequency.MaxLines = 1;
        RadioFrequency.MaxLength = 7;

        RadioFrequency.LostFocus += RadioFrequencyOnLostFocus;

        RadioFrequency.KeyDown += RadioFrequencyOnKeyDown;

        RadioFrequency.GotFocus += RadioFrequencyOnGotFocus;
    }

    public PresetChannelsViewModel ChannelViewModel { get; set; }

    public InstructorModeViewModel InstructorModeViewModel { get; set; }
    public int RadioId
    {
        private get => _radioId;
        set
        {
            _radioId = value;
            UpdateBinding();
        }
    }

    //updates the binding so the changes are picked up for the linked FixedChannelsModel
    private void UpdateBinding()
    {
        ChannelViewModel = _clientStateSingleton.FixedChannels[_radioId - 1];
        InstructorModeViewModel = new InstructorModeViewModel(_radioId);

        PresetChannelsView.GetBindingExpression(DataContextProperty)?.UpdateTarget();
        InstructorModeView.GetBindingExpression(DataContextProperty)?.UpdateTarget();
        
    }

    private void RadioFrequencyOnGotFocus(object sender, RoutedEventArgs routedEventArgs)
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo == null || !dcsPlayerRadioInfo.IsCurrent() ||
            RadioId > dcsPlayerRadioInfo.radios.Length - 1 || RadioId < 0)
        {
            //remove focus to somewhere else
            RadioVolume.Focus();
            Keyboard.ClearFocus(); //then clear altogether
        }
    }

    private
        void RadioFrequencyOnKeyDown(object sender, KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs.Key == Key.Enter)
        {
            //remove focus to somewhere else
            RadioVolume.Focus();
            Keyboard.ClearFocus(); //then clear altogether
        }
    }

    private void RadioFrequencyOnLostFocus(object sender, RoutedEventArgs routedEventArgs)
    {
        double freq = 0;

        // Some locales/cultures (e.g. German) do not parse "." as decimal points since they use decimal commas ("123,45"), leading to "123.45" being parsed as "12345" and frequencies being set too high
        // Using an invariant culture makes sure the decimal point is parsed properly for all locales - replacing any commas makes sure people entering numbers in a weird format still get correct results
        if (double.TryParse(RadioFrequency.Text.Replace(',', '.').Trim(), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out freq))
            RadioHelper.UpdateRadioFrequency(freq, RadioId, false);
        else
            RadioFrequency.Text = "";
    }


    private void Up0001_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(0.001, RadioId);
    }

    private void Up001_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(0.01, RadioId);
    }

    private void Up01_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(0.1, RadioId);
    }

    private void Up1_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(1, RadioId);
    }

    private void Up10_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(10, RadioId);
    }

    private void Down10_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(-10, RadioId);
    }

    private void Down1_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(-1, RadioId);
    }

    private void Down01_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(-0.1, RadioId);
    }

    private void Down001_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(-0.01, RadioId);
    }

    private void Down0001_Click(object sender, RoutedEventArgs e)
    {
        RadioHelper.UpdateRadioFrequency(-0.001, RadioId);
    }


    private void RadioSelectSwitch(object sender, RoutedEventArgs e)
    {
        RadioHelper.SelectRadio(RadioId);
    }

    private void RadioFrequencyText_Click(object sender, MouseButtonEventArgs e)
    {
        RadioHelper.SelectRadio(RadioId);
    }

    private void RadioFrequencyText_RightClick(object sender, MouseButtonEventArgs e)
    {
        RadioHelper.ToggleGuard(RadioId);
    }

    private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
    {
        _dragging = true;
    }


    private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
    {
        var currentRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

        if (currentRadio.volMode == DCSRadio.VolumeMode.OVERLAY)
        {
            var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

            clientRadio.volume = (float)RadioVolume.Value / 100.0f;
        }

        _dragging = false;
    }

    private void ToggleButtons(bool enable, bool mids = false)
    {
        if (enable)
        {
            if (!mids)
            {
                Up10.Visibility = Visibility.Visible;
                Up1.Visibility = Visibility.Visible;
                Up01.Visibility = Visibility.Visible;
                Up001.Visibility = Visibility.Visible;
                Up0001.Visibility = Visibility.Visible;

                Down10.Visibility = Visibility.Visible;
                Down1.Visibility = Visibility.Visible;
                Down01.Visibility = Visibility.Visible;
                Down001.Visibility = Visibility.Visible;
                Down0001.Visibility = Visibility.Visible;

                Up10.IsEnabled = true;
                Up1.IsEnabled = true;
                Up01.IsEnabled = true;
                Up001.IsEnabled = true;
                Up0001.IsEnabled = true;

                Down10.IsEnabled = true;
                Down1.IsEnabled = true;
                Down01.IsEnabled = true;
                Down001.IsEnabled = true;
                Down0001.IsEnabled = true;
            }
            else
            {
                Up10.Visibility = Visibility.Visible;
                Up1.Visibility = Visibility.Visible;
                Up01.Visibility = Visibility.Visible;

                Down10.Visibility = Visibility.Visible;
                Down1.Visibility = Visibility.Visible;
                Down01.Visibility = Visibility.Visible;
                
                Up10.IsEnabled = true;
                Up1.IsEnabled = true;
                Up01.IsEnabled = true;
                
                Down10.IsEnabled = true;
                Down1.IsEnabled = true;
                Down01.IsEnabled = true;
                
                Up001.Visibility = Visibility.Hidden;
                Up0001.Visibility = Visibility.Hidden;
                
                Down001.Visibility = Visibility.Hidden;
                Down0001.Visibility = Visibility.Hidden;
            }

            PresetChannelsView.IsEnabled = true;

            ChannelTab.Visibility = Visibility.Visible;
            
        
            var currentRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

            if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmissionControl ==
                DCSPlayerRadioInfo.SimultaneousTransmissionControl.ENABLED_INTERNAL_SRS_CONTROLS
                && currentRadio.rxOnly == false)
            {
                if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission
                    && _clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmissionControl == DCSPlayerRadioInfo
                        .SimultaneousTransmissionControl.ENABLED_INTERNAL_SRS_CONTROLS)
                {
                    var simulTransmission = 0;
                    for (var i = 0; i < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length; i++)
                        if (_clientStateSingleton.DcsPlayerRadioInfo.radios[i].simul)
                            if (i != RadioId)
                                simulTransmission++;

                    if (simulTransmission < MaxSimultaneousTransmissions)
                    {
                        ToggleSimultaneousTransmissionButton.IsEnabled = true;
                    }
                    else
                    {
                        ToggleSimultaneousTransmissionButton.IsEnabled = false;
                        ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
                else
                {
                    ToggleSimultaneousTransmissionButton.IsEnabled = false;
                    ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);
                }
            }
            else
            {
                ToggleSimultaneousTransmissionButton.IsEnabled = false;
                ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);
            }
        }
        else
        {
            Up10.Visibility = Visibility.Hidden;
            Up1.Visibility = Visibility.Hidden;
            Up01.Visibility = Visibility.Hidden;
            Up001.Visibility = Visibility.Hidden;
            Up0001.Visibility = Visibility.Hidden;

            Down10.Visibility = Visibility.Hidden;
            Down1.Visibility = Visibility.Hidden;
            Down01.Visibility = Visibility.Hidden;
            Down001.Visibility = Visibility.Hidden;
            Down0001.Visibility = Visibility.Hidden;

            ToggleSimultaneousTransmissionButton.IsEnabled = false;
            ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);

            ChannelTab.Visibility = Visibility.Collapsed;
        }
    }

    internal void RepaintRadioStatus()
    {
        SetupEncryption();
        HandleRetransmitStatus();
        HandleInstructorMode();

        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (!_clientStateSingleton.IsConnected || dcsPlayerRadioInfo == null || !dcsPlayerRadioInfo.IsCurrent() ||
            RadioId > dcsPlayerRadioInfo.radios.Length - 1)
        {
            RadioActive.Fill = new SolidColorBrush(Colors.Red);
            RadioLabel.Text = Properties.Resources.OverlayNoRadio;
            RadioFrequency.Text = Properties.Resources.ValueUnknown;

            RadioMetaData.Text = "";

            RadioVolume.IsEnabled = false;

            ToggleButtons(false);

            ToggleSimultaneousTransmissionButton.IsEnabled = false;
            ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);

            //reset dragging just incase
            _dragging = false;
        }
        else
        {
            var currentRadio = dcsPlayerRadioInfo.radios[RadioId];
            var transmitting = _clientStateSingleton.RadioSendingState;

            if (transmitting.IsSending)
            {
                if (transmitting.SendingOn == RadioId)
                    RadioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
                else if (currentRadio != null && currentRadio.simul)
                    RadioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F86FF"));
            }
            else
            {
                if (RadioId == dcsPlayerRadioInfo.selected)
                    RadioActive.Fill = new SolidColorBrush(Colors.Green);
                else if (currentRadio != null && currentRadio.simul)
                    RadioActive.Fill = new SolidColorBrush(Colors.DarkBlue);
                else
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
            }

            if (currentRadio == null || currentRadio.modulation == Modulation.DISABLED) // disabled
            {
                RadioActive.Fill = new SolidColorBrush(Colors.Red);
                RadioLabel.Text = Properties.Resources.OverlayNoRadio;
                RadioFrequency.Text = Properties.Resources.ValueUnknown;
                RadioMetaData.Text = "";


                RadioVolume.IsEnabled = false;

                ToggleButtons(false);

                ToggleSimultaneousTransmissionButton.IsEnabled = false;
                ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);

                return;
            }

            if (currentRadio.modulation == Modulation.INTERCOM) //intercom
            {
                RadioFrequency.Text = "INT. "+RadioId;
                RadioMetaData.Text = "";
                ToggleButtons(false);
                RadioLabel.Text = "INSTR. INT. "+(RadioId);
                
                return;
            }
            else if (currentRadio.modulation == Modulation.MIDS) //MIDS
            {
                switch (RadioCalculator.Link16.FrequencyToChannel(currentRadio.freq))
                {
                    case > 0:
                        if (!RadioFrequency.IsFocused || currentRadio.freqMode == DCSRadio.FreqMode.COCKPIT) 
                            RadioFrequency.Text = RadioCalculator.Link16.FrequencyToChannel(currentRadio.freq).ToString();
                        RadioMetaData.Text = Properties.Resources.OverlayMIDS;
                        break;
                    default:
                        RadioFrequency.Text = "";
                        RadioMetaData.Text = Properties.Resources.ValueOFF;
                        break;
                }
            }
            else
            {
                if (!RadioFrequency.IsFocused
                    || currentRadio.freqMode == DCSRadio.FreqMode.COCKPIT
                    || currentRadio.modulation == Modulation.DISABLED)
                    RadioFrequency.Text =
                        (currentRadio.freq / RadioCalculator.MHz).ToString("0.000",
                            CultureInfo.InvariantCulture); //make number UK / US style with decimals not commas!

                switch (currentRadio.modulation)
                {
                    case Modulation.AM:
                        RadioMetaData.Text = Properties.Resources.OverlayAM;
                        break;
                    case Modulation.FM:
                        RadioMetaData.Text = Properties.Resources.OverlayFM;
                        break;
                    case Modulation.SINCGARS:
                        RadioMetaData.Text = Properties.Resources.OverlaySG;
                        break;
                    case Modulation.HAVEQUICK:
                        RadioMetaData.Text = Properties.Resources.OverlayHQ;
                        break;
                    default:
                        RadioMetaData.Text += "";
                        break;
                }

                if (currentRadio.secFreq > 100) RadioMetaData.Text += " G";

                if (currentRadio.channel > -1) RadioMetaData.Text += " C" + currentRadio.channel;
                if (currentRadio.enc && currentRadio.encKey > 0)
                    RadioMetaData.Text += " E" + currentRadio.encKey; // ENCRYPTED

                if (currentRadio.rxOnly) RadioFrequency.Text += " RX";
            }

            RadioLabel.Text = dcsPlayerRadioInfo.radios[RadioId].name;

            var count = _clientStateSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);

            if (count > 0) RadioMetaData.Text += " 👤" + count;

            if (currentRadio.volMode == DCSRadio.VolumeMode.OVERLAY)
                RadioVolume.IsEnabled = true;
            //reset dragging just incase
            //    _dragging = false;
            else
                RadioVolume.IsEnabled = false;
            //reset dragging just incase
            //  _dragging = false;
            ToggleButtons(currentRadio.freqMode == DCSRadio.FreqMode.OVERLAY,
                currentRadio.modulation == Modulation.MIDS);

            if (_dragging == false) RadioVolume.Value = currentRadio.volume * 100.0;
        }

        var item = TabControl.SelectedItem as TabItem;

        if (item?.Visibility != Visibility.Visible) TabControl.SelectedIndex = 0;
    }

    private void HandleInstructorMode()
    {
        var serverSettings = SyncedServerSettings.Instance;
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
        if (dcsPlayerRadioInfo != null
            && dcsPlayerRadioInfo.IsCurrent()
            && serverSettings.GetSettingAsBool(ServerSettingsKeys.ALLOW_INSTRUCTOR_MODE)
            && ClientStateSingleton.Instance.ExternalAWACSModeConnected)
        {
            var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

            if (currentRadio.modulation != Modulation.DISABLED)
            {
                InstructorTab.Visibility = Visibility.Visible;
            }
        }
        else
        {
            InstructorTab.Visibility = Visibility.Collapsed;
        }
    }


    public void HandleRetransmitStatus()
    {
        var serverSettings = SyncedServerSettings.Instance;
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent() && serverSettings.RetransmitNodeLimit > 0)
        {
            var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

            if (currentRadio.rtMode == DCSRadio.RetransmitMode.DISABLED)
            {
                Retransmit.Visibility = Visibility.Hidden;
            }
            else if (currentRadio.rtMode == DCSRadio.RetransmitMode.COCKPIT)
            {
                Retransmit.Visibility = Visibility.Visible;
                Retransmit.IsEnabled = false;
            }
            else
            {
                Retransmit.Visibility = Visibility.Visible;
                Retransmit.IsEnabled = true;
            }

            if (currentRadio.retransmit)
                Retransmit.Foreground = new SolidColorBrush(Colors.Red);
            else
                Retransmit.Foreground = new SolidColorBrush(Colors.White);
        }
        else
        {
            Retransmit.Visibility = Visibility.Hidden;
        }
    }

    private void SetupEncryption()
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent()
                                       && RadioId <= dcsPlayerRadioInfo.radios.Length - 1)
        {
            var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

            EncryptionKeySpinner.Value = currentRadio.encKey;

            //update stuff
            if (currentRadio.encMode == DCSRadio.EncryptionMode.NO_ENCRYPTION
                || currentRadio.encMode == DCSRadio.EncryptionMode.ENCRYPTION_FULL
                || currentRadio.modulation == Modulation.INTERCOM)
            {
                //Disable everything
                EncryptionKeySpinner.IsEnabled = false;
                EncryptionButton.IsEnabled = false;
                EncryptionButton.Visibility = Visibility.Hidden;
                EncryptionButton.Content = Properties.Resources.BtnEnable;

                EncryptionTab.Visibility = Visibility.Collapsed;
            }
            else if (currentRadio.encMode ==
                     DCSRadio.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
            {
                //allow spinner
                EncryptionKeySpinner.IsEnabled = true;

                //disallow encryption toggle
                EncryptionButton.IsEnabled = false;
                EncryptionButton.Content = Properties.Resources.BtnEnable;
                EncryptionButton.Visibility = Visibility.Visible;
                EncryptionTab.Visibility = Visibility.Visible;
            }
            else if (currentRadio.encMode ==
                     DCSRadio.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
            {
                EncryptionKeySpinner.IsEnabled = true;
                EncryptionButton.IsEnabled = true;
                EncryptionButton.Visibility = Visibility.Visible;

                if (currentRadio.enc)
                    EncryptionButton.Content = Properties.Resources.BtnDisable;
                else
                    EncryptionButton.Content = Properties.Resources.BtnEnable;
                EncryptionTab.Visibility = Visibility.Visible;
            }
        }
        else
        {
            //Disable everything
            EncryptionKeySpinner.IsEnabled = false;
            EncryptionButton.IsEnabled = false;
            EncryptionButton.Visibility = Visibility.Hidden;
            EncryptionButton.Content = Properties.Resources.BtnEnable;
            EncryptionTab.Visibility = Visibility.Collapsed;
        }
    }


    internal void RepaintRadioReceive()
    {
        TransmitterName.Visibility = Visibility.Collapsed;
        RadioFrequency.Visibility = Visibility.Visible;
        RadioMetaData.Visibility = Visibility.Visible;

        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
        if (dcsPlayerRadioInfo == null)
        {
            RadioFrequency.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
            RadioMetaData.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
        }
        else
        {
            var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
            //check if current

            if (receiveState == null || !receiveState.IsReceiving)
            {
                RadioFrequency.Foreground =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                RadioMetaData.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
            }
            else if (receiveState != null && receiveState.IsReceiving)
            {
                if (receiveState.SentBy.Length > 0)
                {
                    TransmitterName.Text = receiveState.SentBy;

                    TransmitterName.Visibility = Visibility.Visible;
                    RadioFrequency.Visibility = Visibility.Collapsed;
                    RadioMetaData.Visibility = Visibility.Collapsed;
                }

                if (receiveState.IsSecondary)
                {
                    TransmitterName.Foreground = new SolidColorBrush(Colors.Red);
                    RadioFrequency.Foreground = new SolidColorBrush(Colors.Red);
                    RadioMetaData.Foreground = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    TransmitterName.Foreground = new SolidColorBrush(Colors.White);
                    RadioFrequency.Foreground = new SolidColorBrush(Colors.White);
                    RadioMetaData.Foreground = new SolidColorBrush(Colors.White);
                }
            }
            else
            {
                RadioFrequency.Foreground =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                RadioMetaData.Foreground =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
            }
        }
    }


    private void Encryption_ButtonClick(object sender, RoutedEventArgs e)
    {
        var currentRadio = RadioHelper.GetRadio(RadioId);

        if (currentRadio != null &&
            currentRadio.modulation != Modulation.DISABLED) // disabled
            //update stuff
            if (currentRadio.encMode == DCSRadio.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
            {
                RadioHelper.ToggleEncryption(RadioId);

                if (currentRadio.enc)
                    EncryptionButton.Content = Properties.Resources.BtnEnable;
                else
                    EncryptionButton.Content = Properties.Resources.BtnDisable;
            }
    }

    private void EncryptionKeySpinner_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (EncryptionKeySpinner?.Value != null)
            RadioHelper.SetEncryptionKey(RadioId, (byte)EncryptionKeySpinner.Value);
    }

    private void ToggleSimultaneousTransmissionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clientStateSingleton.DcsPlayerRadioInfo != null &&
            _clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
        {
            var currentRadio = RadioHelper.GetRadio(RadioId);

            if (currentRadio != null)
            {
                currentRadio.simul = !currentRadio.simul;

                if (currentRadio.simul)
                    ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.Orange);
                else
                    ToggleSimultaneousTransmissionButton.Foreground = new SolidColorBrush(Colors.White);
            }
        }
    }

    private void RetransmitClick(object sender, RoutedEventArgs e)
    {
        RadioHelper.ToggleRetransmit(RadioId);
    }

    public void OnClose()
    {
        InstructorModeViewModel?.StopInstructorMode();
    }
}