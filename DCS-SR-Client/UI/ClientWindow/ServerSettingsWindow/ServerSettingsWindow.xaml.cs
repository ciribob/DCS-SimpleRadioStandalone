using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using CommunityToolkit.Mvvm.DependencyInjection;
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
        private ServerSettingsModel _serverSettings { get; } =
            Ioc.Default.GetRequiredService<ISrsSettings>().CurrentServerSettings;

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
                SpectatorAudio.Content = settings.IsSpectatorsAudioDisabled
                    ? Properties.Resources.ValueDISABLED
                    : Properties.Resources.ValueENABLED;

                CoalitionSecurity.Content = settings.IsCoalitionAudioSeperated
                    ? Properties.Resources.ValueON
                    : Properties.Resources.ValueOFF;

                LineOfSight.Content = settings.IsLineOfSightCheckingEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                Distance.Content = settings.IsDistanceCheckingEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RealRadio.Content = settings.IsRadioTxEffectsEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RadioRXInterference.Content = settings.IsRadioRxInterferenceEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                RadioExpansion.Content = settings.IsExpandedRadiosAllowed ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ExternalAWACSMode.Content = settings.IsExternalModeAllowed ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                AllowRadioEncryption.Content = settings.IsRadioEncryptionAllowed ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                StrictRadioEncryption.Content = settings.IsStrictRadioEncryptionEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                TunedClientCount.Content = settings.IsShowTunedListenerCount ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ShowTransmitterName.Content = settings.IsShowTransmitterNameEnabled ? Properties.Resources.ValueON : Properties.Resources.ValueOFF;

                ServerVersion.Content = SRSClientSyncHandler.ServerVersion;

                NodeLimit.Content = settings.RetransmissionNodeLimit;
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