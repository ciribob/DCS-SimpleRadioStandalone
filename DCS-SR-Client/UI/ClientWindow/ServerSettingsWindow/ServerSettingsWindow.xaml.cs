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
                SpectatorAudio.Content = settings.SpectatorsAudioDisabled
                    ? Properties.Resources.ValueDISABLED
                    : Properties.Resources.ValueENABLED;

                CoalitionSecurity.Content = settings.CoalitionAudioSecurityEnabled
                    ? Properties.Resources.ValueON
                    : Properties.Resources.ValueOFF;

                LineOfSight.Content = settings.LosCheckingEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                Distance.Content = settings.DistanceCheckingEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RealRadio.Content = settings.IrlRadioTxEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RadioRXInterference.Content = settings.IrlRadioRxInterferenceEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RadioExpansion.Content = settings.RadioExpansionAllowed ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ExternalAWACSMode.Content = settings.ExternalAwacsModeAllowed ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                AllowRadioEncryption.Content = settings.RadioExpansionAllowed ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                StrictRadioEncryption.Content = settings.StrictRadioEncryptionEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                TunedClientCount.Content = settings.ShowTurnedListenersCountEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ShowTransmitterName.Content = settings.ShowTransmitterNameEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ServerVersion.Content = SRSClientSyncHandler.ServerVersion;

                NodeLimit.Content = settings.RetransmissionNodeLimit.ToString();
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