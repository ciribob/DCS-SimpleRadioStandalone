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
    public partial class RadioChannelConfigUi : Slider
    {
        public float SliderValue { get; set; }
        
        public RadioChannelConfigUi()
        {
            InitializeComponent();
        }
    }
}