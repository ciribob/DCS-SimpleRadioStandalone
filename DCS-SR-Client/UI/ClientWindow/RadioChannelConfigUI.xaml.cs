using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for RadioChannelConfigUI.xaml
    /// </summary>
    public partial class RadioChannelConfigUi : UserControl
    {
        public RadioChannelConfigUi()
        {
            InitializeComponent();

            //I do this because at this point ProfileSettingKey hasn't been set
            //but it has when this is called
            ChannelSelector.Loaded += InitBalanceSlider;
        }

        // Todo, eliminate this data passing method.
        // Data binding to a property would be preferred. 
        public ProfileSettingsKeys ProfileSettingKey { get; set; }

        private void InitBalanceSlider(object sender, RoutedEventArgs e)
        {
            ChannelSelector.IsEnabled = false;
            Reload();

            ChannelSelector.ValueChanged += ChannelSelector_SelectionChanged;
        }

        public void Reload()
        {
            ChannelSelector.IsEnabled = false;

            ChannelSelector.Value = GlobalSettingsStore.Instance.ProfileSettingsStore.SpecialCaseForAudioBalanceUI_GetFloat(ProfileSettingKey);

            ChannelSelector.IsEnabled = true;
        }

        private void ChannelSelector_SelectionChanged(object sender, EventArgs eventArgs)
        {
            //the selected value changes when 
            if (ChannelSelector.IsEnabled)
            {
                GlobalSettingsStore.Instance.ProfileSettingsStore.SpecialCaseForAudioBalanceUI_SetFloat(ProfileSettingKey,(float) ChannelSelector.Value);
            }
        }
    }
}