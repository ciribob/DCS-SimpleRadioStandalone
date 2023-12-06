using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using MahApps.Metro.Controls;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for ServerSettingsWindow.xaml
    /// </summary>
    public partial class ServerSettingsWindow : MetroWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _updateTimer;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public ServerSettingsWindow()
        {
            InitializeComponent();

            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            _updateTimer.Tick += UpdateUI;
            _updateTimer.Start();

            UpdateUI(null, null);
        }

        private void UpdateUI(object sender, EventArgs e)
        {
            var settings = _serverSettings;

            try
            {
                SpectatorAudio.Content = settings.GetSettingAsBool(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED)
                    ? Properties.Resources.ValueDISABLED
                    : Properties.Resources.ValueENABLED;

                CoalitionSecurity.Content = settings.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY)
                    ? Properties.Resources.ValueON
                    : Properties.Resources.ValueOFF;

                LineOfSight.Content = settings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                Distance.Content = settings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RealRadio.Content = settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RadioRXInterference.Content =
                    settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RadioExpansion.Content = settings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ExternalAWACSMode.Content = settings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                AllowRadioEncryption.Content = settings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                StrictRadioEncryption.Content = settings.GetSettingAsBool(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                TunedClientCount.Content = settings.GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ShowTransmitterName.Content = settings.GetSettingAsBool(ServerSettingsKeys.SHOW_TRANSMITTER_NAME) ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ServerVersion.Content = SRSClientSyncHandler.ServerVersion;

                NodeLimit.Content = settings.RetransmitNodeLimit;
            }
            catch (IndexOutOfRangeException)
            {
                Logger.Warn("Missing Server Option - Connected to old server");
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