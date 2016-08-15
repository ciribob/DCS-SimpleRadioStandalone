using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Server;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for RadioControlGroup.xaml
    /// </summary>
    public partial class RadioControlGroup : UserControl
    {
        private const double MHz = 1000000;
        private bool _dragging;

        public int RadioId { private get; set; }

        public RadioControlGroup()
        {
            InitializeComponent();
        }

        private void Up001_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz/100);
            FocusDCS();
        }
        private void Up01_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz / 10);
            FocusDCS();
        }

        private void Up1_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz);
            FocusDCS();
        }

        private void Up10_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz*10);
            FocusDCS();
        }

        private void Down10_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz*-10);
            FocusDCS();
        }

        private void Down1_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz*-1);
            FocusDCS();
        }

        private void Down01_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz/10*-1);
            FocusDCS();
        }
        private void Down001_Click(object sender, RoutedEventArgs e)
        {
            SendFrequencyChange(MHz / 100 * -1);
            FocusDCS();
        }

        private void FocusDCS()
        {
            var localByName = Process.GetProcessesByName("dcs");

            if (localByName != null && localByName.Length > 0)
            {
                //    WindowHelper.BringProcessToFront(localByName[0]);
            }
        }

        private void SendFrequencyChange(double frequency)
        {

            if (RadioDCSSyncServer.DcsPlayerRadioInfo.radioType == DCSPlayerRadioInfo.AircraftRadioType.NO_COCKPIT_INTEGRATION
               && RadioId >= 0
               && RadioId < RadioDCSSyncServer.DcsPlayerRadioInfo.radios.Length)
            {
                //sort out the frequencies
                var clientRadio = RadioDCSSyncServer.DcsPlayerRadioInfo.radios[RadioId];
                clientRadio.frequency += frequency;

                //make sure we're not over or under a limit
                if (clientRadio.frequency > clientRadio.freqMax)
                {
                    clientRadio.frequency = clientRadio.freqMax;
                }
                else if (clientRadio.frequency < clientRadio.freqMin)
                {
                    clientRadio.frequency = clientRadio.freqMin;
                }

                //make radio data stale to force resysnc
                RadioDCSSyncServer.LastSent = 0;

            }
        }

        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            if (RadioDCSSyncServer.DcsPlayerRadioInfo.radioType != DCSPlayerRadioInfo.AircraftRadioType.FULL_COCKPIT_INTEGRATION)
            {
                RadioDCSSyncServer.DcsPlayerRadioInfo.selected = (short)RadioId;
            }

            FocusDCS();
        }

        private void RadioFrequencyText_Click(object sender, MouseButtonEventArgs e)
        {
            if (RadioDCSSyncServer.DcsPlayerRadioInfo.radioType != DCSPlayerRadioInfo.AircraftRadioType.FULL_COCKPIT_INTEGRATION)
            {
                RadioDCSSyncServer.DcsPlayerRadioInfo.selected = (short)RadioId;
            }

            FocusDCS();
        }

        private void RadioFrequencyText_RightClick(object sender, MouseButtonEventArgs e)
        {

            if (RadioDCSSyncServer.DcsPlayerRadioInfo.radioType == DCSPlayerRadioInfo.AircraftRadioType.NO_COCKPIT_INTEGRATION)
            {
                //sort out the frequencies
                var clientRadio = RadioDCSSyncServer.DcsPlayerRadioInfo.radios[RadioId];
                if (clientRadio.secondaryFrequency > 0)
                {
                    clientRadio.secondaryFrequency = 0; // 0 indicates we want it overridden + disabled
                }
                else
                {
                    clientRadio.secondaryFrequency = 1; //indicates we want it back
                }

                //make radio data stale to force resysnc
                RadioDCSSyncServer.LastSent = 0;
            }
            FocusDCS();
        }

        private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
        {
            _dragging = true;
        }


        private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
        {

            if (RadioDCSSyncServer.DcsPlayerRadioInfo.radioType == DCSPlayerRadioInfo.AircraftRadioType.NO_COCKPIT_INTEGRATION)
            {
                var clientRadio = RadioDCSSyncServer.DcsPlayerRadioInfo.radios[RadioId];

                clientRadio.volume = (float)radioVolume.Value / 100.0f;
            }

            _dragging = false;

            FocusDCS();
        }

        private void ToggleButtons(bool enable)
        {
            if (enable)
            {
                up10.Visibility = Visibility.Visible;
                up1.Visibility = Visibility.Visible;
                up01.Visibility = Visibility.Visible;
                up001.Visibility = Visibility.Visible;

                down10.Visibility = Visibility.Visible;
                down1.Visibility = Visibility.Visible;
                down01.Visibility = Visibility.Visible;
                down001.Visibility = Visibility.Visible;

                up10.IsEnabled = true;
                up1.IsEnabled = true;
                up01.IsEnabled = true;
                up001.IsEnabled = true;

                down10.IsEnabled = true;
                down1.IsEnabled = true;
                down01.IsEnabled = true;
                down001.IsEnabled = true;
            }
            else
            {
                up10.Visibility = Visibility.Hidden;
                up1.Visibility = Visibility.Hidden;
                up01.Visibility = Visibility.Hidden;
                up001.Visibility = Visibility.Hidden;

                down10.Visibility = Visibility.Hidden;
                down1.Visibility = Visibility.Hidden;
                down01.Visibility = Visibility.Hidden;
                down001.Visibility = Visibility.Hidden;

            }
           
        }

        internal void RepaintRadioStatus()
        {
            SetupEncryption();

            var dcsPlayerRadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo  == null || !dcsPlayerRadioInfo.IsCurrent())
            {
                radioActive.Fill = new SolidColorBrush(Colors.Red);
                radioLabel.Text = "No Radio";
                radioFrequency.Text = "Unknown";

                radioVolume.IsEnabled = false;

                ToggleButtons(false);
               
                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                if (RadioId == dcsPlayerRadioInfo.selected)
                {
                    var transmitting = UdpVoiceHandler.RadioSendingState;

                    if (transmitting.IsSending && transmitting.SendingOn == RadioId)
                    {
                        radioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
                    }
                    else
                    {
                        radioActive.Fill = new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                    radioActive.Fill = new SolidColorBrush(Colors.Orange);
                }

                var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

                if (currentRadio.modulation == 3) // disabled
                {
                    radioActive.Fill = new SolidColorBrush(Colors.Red);
                    radioLabel.Text = "No Radio";
                    radioFrequency.Text = "Unknown";

                    radioVolume.IsEnabled = false;

                    ToggleButtons(false);
                    return;
                }
                if (currentRadio.modulation == 2) //intercom
                {
                    radioFrequency.Text = "INTERCOM";
                }
                else
                {
                    radioFrequency.Text = (currentRadio.frequency/MHz).ToString("0.000") +
                                          (currentRadio.modulation == 0 ? "AM" : "FM");
                    if (currentRadio.secondaryFrequency > 100)
                    {
                        radioFrequency.Text += " G";
                    }
                    if (currentRadio.enc && currentRadio.encKey > 0)
                    {
                        radioFrequency.Text += " E" + currentRadio.encKey; // ENCRYPTED
                    }
                }
                radioLabel.Text = dcsPlayerRadioInfo.radios[RadioId].name;

                if (dcsPlayerRadioInfo.radioType == DCSPlayerRadioInfo.AircraftRadioType.FULL_COCKPIT_INTEGRATION)
                {
                    radioVolume.IsEnabled = false;

                    ToggleButtons(false);

                    //reset dragging just incase
                    _dragging = false;
                }
                else if (dcsPlayerRadioInfo.radioType == DCSPlayerRadioInfo.AircraftRadioType.PARTIAL_COCKPIT_INTEGRATION)
                {
                    radioVolume.IsEnabled = false;

                    ToggleButtons(false);

                    //reset dragging just incase
                    _dragging = false;
                }
                else
                {
                    radioVolume.IsEnabled = true;
                    ToggleButtons(true);
                }

                if (_dragging == false)
                {
                    radioVolume.Value = currentRadio.volume*100.0;
                }
            }

         
        }

        private void SetupEncryption()
        {
            var dcsPlayerRadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo != null || dcsPlayerRadioInfo.IsCurrent())
            {
                var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

                EncryptionKeySpinner.Value = currentRadio.encKey;

                //update stuff
                if (currentRadio.encMode == RadioInformation.EncryptionMode.NO_ENCRYPTION 
                    || currentRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_FULL 
                    || currentRadio.modulation == 2)
                {
                    //Disable everything
                    EncryptionKeySpinner.IsEnabled = false;
                    EncryptionButton.IsEnabled = false;
                    EncryptionButton.Content = "Enable";

                }else if (currentRadio.encMode ==
                            RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                {
                    //allow spinner
                    EncryptionKeySpinner.IsEnabled = true;

                    //disallow encryption toggle
                    EncryptionButton.IsEnabled = false;
                    EncryptionButton.Content = "Enable";
                }
                else if(currentRadio.encMode ==
                            RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                {
                    EncryptionKeySpinner.IsEnabled = true;
                    EncryptionButton.IsEnabled = true;

                    if (currentRadio.enc)
                    {
                        EncryptionButton.Content = "Disable";
                    }
                    else
                    {
                        EncryptionButton.Content = "Enable";
                    }
                      
                }

            }
            else
            {
                //Disable everything
                EncryptionKeySpinner.IsEnabled = false;
                EncryptionButton.IsEnabled = false;
                EncryptionButton.Content = "Enable";
            }
        }


        internal void RepaintRadioReceive()
        {
            var dcsPlayerRadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo;
            if (dcsPlayerRadioInfo == null)
            {
                radioFrequency.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
            }
            else
            {
                var receiveState = UdpVoiceHandler.RadioReceivingState[RadioId];
                //check if current

                if (receiveState == null || !receiveState.IsReceiving())
                {
                    radioFrequency.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
                else if (receiveState != null && receiveState.IsReceiving())
                {
                    if (receiveState.IsSecondary)
                    {
                        radioFrequency.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        radioFrequency.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
                else
                {
                    radioFrequency.Foreground =
                            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                }
            }
        }


        private void Encryption_ButtonClick(object sender, RoutedEventArgs e)
        {
            var dcsPlayerRadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo != null || dcsPlayerRadioInfo.IsCurrent())
            {
                var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

                if (currentRadio.modulation != 3) // disabled
                {
                    //update stuff
                    if (currentRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                    {
                        if (currentRadio.enc)
                        {
                            currentRadio.enc = false;
                            EncryptionButton.Content = "Enable";
                        }
                        else
                        {
                            currentRadio.enc = true;
                            EncryptionButton.Content = "Disable";
                        }
                    }
                }
            }
        }

        private void EncryptionKeySpinner_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var dcsPlayerRadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo != null || dcsPlayerRadioInfo.IsCurrent())
            {
                var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

                if (currentRadio.modulation != 3) // disabled
                {
                    //update stuff
                    if (currentRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE || currentRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                    {
                        if (EncryptionKeySpinner.Value != null)
                        {
                            currentRadio.encKey = (byte)EncryptionKeySpinner.Value;
                        }
                    }
                }
            }
        }
    }
}