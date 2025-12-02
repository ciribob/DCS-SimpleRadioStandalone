using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow;

/// <summary>
///     Interaction logic for RadioControlGroup.xaml
/// </summary>
public partial class RadioControlGroup : UserControl
{
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;
    private bool _dragging;

    private int _radioId;

    public RadioControlGroup()
    {
        DataContext = this; // set data context

        InitializeComponent();
    }

    public PresetChannelsViewModel ChannelViewModel { get; set; }

    public int RadioId
    {
        get => _radioId;
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

        var bindingExpression = PresetChannelsView.GetBindingExpression(DataContextProperty);
        bindingExpression?.UpdateTarget();
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

            //  ReloadButton.IsEnabled = true;
            //LoadFromFileButton.IsEnabled = true;

            PresetChannelsView.IsEnabled = true;

            ChannelTab.Visibility = Visibility.Visible;
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

            PresetChannelsView.IsEnabled = false;

            ChannelTab.Visibility = Visibility.Collapsed;
        }
    }

    internal void RepaintRadioStatus()
    {
        HandleEncryptionStatus();
        HandleRetransmitStatus();

        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo == null || !dcsPlayerRadioInfo.IsCurrent())
        {
            RadioActive.Fill = new SolidColorBrush(Colors.Red);
            RadioLabel.Text = Properties.Resources.OverlayNoRadio;
            RadioFrequency.Text = Properties.Resources.ValueUnknown;

            RadioVolume.IsEnabled = false;

            TunedClients.Visibility = Visibility.Hidden;

            ToggleButtons(false);

            //reset dragging just incase
            _dragging = false;
        }
        else
        {
            var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

            if (currentRadio == null) return;

            var transmitting = _clientStateSingleton.RadioSendingState;
            if (RadioId == dcsPlayerRadioInfo.selected)
            {
                if (transmitting.IsSending && transmitting.SendingOn == RadioId)
                    RadioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
                else
                    RadioActive.Fill = new SolidColorBrush(Colors.Green);
            }
            else
            {
                if (currentRadio.simul && dcsPlayerRadioInfo.simultaneousTransmission)
                    // if (transmitting.IsSending)
                    // {
                    //     RadioActive.Fill = new SolidColorBrush(Colors.LightBlue);
                    // }
                    // else
                    // {
                    RadioActive.Fill = new SolidColorBrush(Colors.DarkBlue);
                // }
                else
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
            }

            if (currentRadio.modulation == Modulation.DISABLED) // disabled
            {
                RadioActive.Fill = new SolidColorBrush(Colors.Red);
                RadioLabel.Text = Properties.Resources.OverlayNoRadio;
                RadioFrequency.Text = Properties.Resources.ValueUnknown;

                RadioVolume.IsEnabled = false;

                TunedClients.Visibility = Visibility.Hidden;

                ToggleButtons(false);

                ChannelTab.Visibility = Visibility.Collapsed;
                return;
            }


            if (currentRadio.modulation == Modulation.INTERCOM) //intercom
            {
                RadioFrequency.Text = Properties.Resources.OverlayIntercom;
            }
            else if (currentRadio.modulation == Modulation.MIDS) //MIDS
            {
                switch (RadioCalculator.Link16.FrequencyToChannel(currentRadio.freq))
                {
                    case > 0:
                        RadioFrequency.Text = RadioCalculator.Link16.FrequencyToChannel(currentRadio.freq).ToString();
                        RadioFrequency.Text += Properties.Resources.OverlayMIDS;
                        break;
                    default:
                        RadioFrequency.Text = "";
                        RadioFrequency.Text += Properties.Resources.ValueOFF;
                        break;
                }
            }
            else
            {
                RadioFrequency.Text =
                    (currentRadio.freq / RadioCalculator.MHz).ToString("0.000",
                        CultureInfo.InvariantCulture); //make number UK / US style with decimals not commas!

                switch (currentRadio.modulation)
                {
                    case Modulation.AM:
                        RadioFrequency.Text += "AM";
                        break;
                    case Modulation.FM:
                        RadioFrequency.Text += "FM";
                        break;
                    case Modulation.SINCGARS:
                        RadioFrequency.Text += "SG";
                        break;
                    case Modulation.HAVEQUICK:
                        RadioFrequency.Text += "HQ";
                        break;
                    default:
                        RadioFrequency.Text += "";
                        break;
                }

                if (currentRadio.secFreq > 100) RadioFrequency.Text += " G";

                if (currentRadio.channel >= 0) RadioFrequency.Text += " C" + currentRadio.channel;

                if (currentRadio.enc && currentRadio.encKey > 0)
                    RadioFrequency.Text += " E" + currentRadio.encKey; // ENCRYPTED

                if (currentRadio.rxOnly) RadioFrequency.Text += " RX";
            }


            var count = _clientStateSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);

            if (count > 0)
            {
                TunedClients.Text = "👤" + count;
                TunedClients.Visibility = Visibility.Visible;
            }
            else
            {
                TunedClients.Visibility = Visibility.Hidden;
            }

            RadioLabel.Text = dcsPlayerRadioInfo.radios[RadioId].name;

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

    private void HandleEncryptionStatus()
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent())
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

    internal void RepaintRadioReceive()
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
        if (dcsPlayerRadioInfo == null)
        {
            RadioFrequency.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
        }
        else
        {
            var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
            //check if current

            if (receiveState == null || !receiveState.IsReceiving)
            {
                RadioFrequency.Foreground =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
            }
            else if (receiveState != null && receiveState.IsReceiving)
            {
                if (receiveState.SentBy.Length > 0) RadioFrequency.Text = receiveState.SentBy;

                if (receiveState.IsSecondary)
                    RadioFrequency.Foreground = new SolidColorBrush(Colors.Red);
                else
                    RadioFrequency.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                RadioFrequency.Foreground =
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

    private void RetransmitClick(object sender, RoutedEventArgs e)
    {
        RadioHelper.ToggleRetransmit(RadioId);
    }
}