using System.Windows;
using System.Windows.Controls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for InputBindingControl.xaml
    /// </summary>
    public partial class InputBindingControl : UserControl
    {
        private InputDeviceManager _inputDeviceManager;
        public InputDeviceManager InputDeviceManager
        {
            get { return _inputDeviceManager; }
            set
            {
                _inputDeviceManager = value;
                LoadInputSettings();
            }
        }
        public string InputName { get; set; }

        public InputBindingControl()
        {
            InitializeComponent();
        }
        
        public void LoadInputSettings()
        {
            PrimaryDeviceLabel.Content = InputName;
            ModifierDeviceLabel.Content = InputName + " " + Properties.Resources.InputModifier;
        }

        private void PrimarySet_Click(object sender, RoutedEventArgs e)
        {
            PrimaryClearButton.IsEnabled = false;
            PrimarySetButton.IsEnabled = false;

            InputDeviceManager.AssignButton(foundDevice =>
            {
                PrimaryClearButton.IsEnabled = true;
                PrimarySetButton.IsEnabled = true;

                ((InputBindingModel)DataContext).Primary = foundDevice;
            });
        }

        private void PrimaryClear_Click(object sender, RoutedEventArgs e)
        {
            ((InputBindingModel)DataContext).Primary = new InputDevice();
        }

        private void ModifierSet_Click(object sender, RoutedEventArgs e)
        {
            ModifierSetButton.IsEnabled = false;
            ModifierClearButton.IsEnabled = false;

            InputDeviceManager.AssignButton(foundDevice =>
            {
                ModifierSetButton.IsEnabled = true;
                ModifierClearButton.IsEnabled = true;

                ((InputBindingModel)DataContext).Modifier = foundDevice;
            });
        }

        private void ModifierClear_Click(object sender, RoutedEventArgs e)
        {
            ((InputBindingModel)DataContext).Modifier = new InputDevice();
        }
    }
}