using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using MahApps.Metro.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow
{
    /// <summary>
    /// Interaction logic for RadioCapabilities.xaml
    /// </summary>
    public partial class RadioCapabilities : MetroWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _updateTimer;


        public RadioCapabilities()
        {
            InitializeComponent();

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += UpdateUI;
            _updateTimer.Start();

            UpdateUI(null, null);
        }

        private void UpdateUI(object sender, EventArgs e)
        {
            var radioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

            var profile = GlobalSettingsStore.Instance.ProfileSettingsStore;

            try
            {
                if (radioInfo.IsCurrent())
                {

                    Desc.Text = radioInfo.capabilities.desc;

                    if (radioInfo.capabilities.dcsPtt)
                    {
                        DCSPTT.Content = Properties.Resources.OverlayAvailableCockpit;

                        if (!profile.GetClientSettingBool(ProfileSettingsKeys.AllowDCSPTT))
                        {
                            DCSPTT.Content += " " + Properties.Resources.OverlayDisabledSRS;
                        }
                        
                    }
                    else
                    {
                        DCSPTT.Content = Properties.Resources.OverlayNotAvailable;
                    }

                    if (radioInfo.capabilities.dcsRadioSwitch)
                    {
                        DCSRadioSwitch.Content = Properties.Resources.OverlayAvailableCockpit;

                        if (profile.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls))
                        {
                            DCSRadioSwitch.Content += " " + Properties.Resources.OverlayDisabledSRS;
                        }

                    }
                    else
                    {
                        DCSRadioSwitch.Content = Properties.Resources.OverlayNotAvailable;
                    }

                    if (radioInfo.capabilities.dcsIFF)
                    {
                        DCSIFF.Content = Properties.Resources.OverlayAvailableCockpit;

                        if (profile.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowTransponderOverlay))
                        {
                            DCSIFF.Content += " " + Properties.Resources.OverlayDisabledSRS;
                        }

                    }
                    else
                    {
                        DCSIFF.Content = Properties.Resources.OverlayNotAvailable;
                    }

                    if (radioInfo.capabilities.intercomHotMic)
                    {
                        IntercomHotMic.Content = Properties.Resources.OverlayAvailableCockpit;

                        if (!profile.GetClientSettingBool(ProfileSettingsKeys.AllowDCSPTT) || profile.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls))
                        {
                            IntercomHotMic.Content += " " + Properties.Resources.OverlayDisabledSRS;
                        }

                    }
                    else
                    {
                        IntercomHotMic.Content = Properties.Resources.ValueNotAvailable;
                    }

                }
                else
                {
                    Desc.Text = "";
                    DCSPTT.Content = Properties.Resources.ValueUnknown;
                    DCSRadioSwitch.Content = Properties.Resources.ValueUnknown;
                    DCSIFF.Content = Properties.Resources.ValueUnknown;
                    IntercomHotMic.Content = Properties.Resources.ValueUnknown;

                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing capabilities");
            }
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _updateTimer.Stop();
        }
    }
}