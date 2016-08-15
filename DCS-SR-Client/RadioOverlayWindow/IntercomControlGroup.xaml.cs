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
    ///     Interaction logic for IntercomControlGroup.xaml
    /// </summary>
    public partial class IntercomControlGroup : UserControl
    {
        private bool _dragging;

        public int RadioId { private get; set; }

        public IntercomControlGroup()
        {
            InitializeComponent();
        }
        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            if (RadioDCSSyncServer.DcsPlayerRadioInfo.radioType != DCSPlayerRadioInfo.AircraftRadioType.FULL_COCKPIT_INTEGRATION)
            {
                RadioDCSSyncServer.DcsPlayerRadioInfo.selected = (short)RadioId;
            }
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
        }

        internal void RepaintRadioStatus()
        {
            var dcsPlayerRadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo  == null || !dcsPlayerRadioInfo.IsCurrent())
            {
                radioActive.Fill = new SolidColorBrush(Colors.Red);

                radioVolume.IsEnabled = false;
               
                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                if (RadioId == dcsPlayerRadioInfo.selected)
                {
                    var transmitting = UdpVoiceHandler.RadioSendingState;
                    var receiveState = UdpVoiceHandler.RadioReceivingState[RadioId];

                    if ((transmitting.IsSending && transmitting.SendingOn == RadioId )
                        ||
                        (receiveState!=null && receiveState.IsReceiving()))
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

                if (currentRadio.modulation == 2) //intercom
                {
                    radioLabel.Text = "INTERCOM";
                    radioVolume.IsEnabled = false;
                }
                else
                {
                    radioLabel.Text = "NO INTERCOM";
                    radioActive.Fill = new SolidColorBrush(Colors.Red);
                }
             
                if (_dragging == false)
                {
                    radioVolume.Value = currentRadio.volume*100.0;
                }
            }
        }

    }
}