using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using SharpDX.DirectInput;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.InputSettingsControl;

/// <summary>
///     Interaction logic for InputBindingControl.xaml
/// </summary>
public partial class InputBindingControl : UserControl, IHandle<ProfileChangedMessage>
{
    public static readonly DependencyProperty ControlInputDependencyPropertyProperty =
        DependencyProperty.Register(nameof(ControlInputBinding), typeof(InputBinding), typeof(InputBindingControl),
            new PropertyMetadata(null)
        );

    public static readonly DependencyProperty ControlInputNameDependencyPropertyProperty =
        DependencyProperty.Register(nameof(InputName), typeof(string), typeof(InputBindingControl),
            new PropertyMetadata("None")
        );


    public InputBindingControl()
    {
        InitializeComponent();

        Loaded += (sender, args) => LoadInputSettings();

        EventBus.Instance.SubscribeOnUIThread(this);
    }


    public InputBinding ControlInputBinding
    {
        set => SetValue(ControlInputDependencyPropertyProperty, value);
        get
        {
            var val = (InputBinding)GetValue(ControlInputDependencyPropertyProperty);
            return val;
        }
    }

    public InputBinding ModifierBinding { get; set; }

    public string InputName
    {
        set => SetValue(ControlInputNameDependencyPropertyProperty, value);
        get
        {
            var val = (string)GetValue(ControlInputNameDependencyPropertyProperty);
            return val;
        }
    }

    public Task HandleAsync(ProfileChangedMessage message, CancellationToken cancellationToken)
    {
        LoadInputSettings();

        return Task.CompletedTask;
    }

    public void LoadInputSettings()
    {
        DeviceLabel.Content = InputName;
        ModifierLabel.Content = InputName + " " + Properties.Resources.InputModifier;
        ModifierBinding = (InputBinding)(int)ControlInputBinding + 100; //add 100 gets the enum of the modifier

        var currentInputProfile = GlobalSettingsStore.Instance.ProfileSettingsStore.GetCurrentInputProfile();

        if (currentInputProfile != null)
        {
            var devices = currentInputProfile;
            if (currentInputProfile.ContainsKey(ControlInputBinding))
            {
                var button = devices[ControlInputBinding].Button;
                DeviceText.Text =
                    GetDeviceText(button, devices[ControlInputBinding].DeviceName);
                Device.Text = devices[ControlInputBinding].DeviceName;
            }
            else
            {
                DeviceText.Text = Properties.Resources.InputTextNone;
                Device.Text = Properties.Resources.InputTextNone;
            }

            if (currentInputProfile.ContainsKey(ModifierBinding))
            {
                var button = devices[ModifierBinding].Button;
                ModifierText.Text =
                    GetDeviceText(button, devices[ModifierBinding].DeviceName);
                ModifierDevice.Text = devices[ModifierBinding].DeviceName;
            }
            else
            {
                ModifierText.Text = Properties.Resources.InputTextNone;
                ModifierDevice.Text = Properties.Resources.InputTextNone;
            }
        }
    }

    private string GetDeviceText(int button, string name)
    {
        if (name.ToLowerInvariant() == "keyboard")
            try
            {
                var key = (Key)button;
                return key.ToString();
            }
            catch
            {
            }

        return button < 128 ? (button + 1).ToString() : "POV " + (button - 127);
    }

    private void Device_Click(object sender, RoutedEventArgs e)
    {
        DeviceClear.IsEnabled = false;
        DeviceButton.IsEnabled = false;

        InputDeviceManager.Instance.AssignButton(device =>
        {
            DeviceClear.IsEnabled = true;
            DeviceButton.IsEnabled = true;

            Device.Text = device.DeviceName;
            DeviceText.Text = GetDeviceText(device.Button, device.DeviceName);

            device.InputBind = ControlInputBinding;

            GlobalSettingsStore.Instance.ProfileSettingsStore.SetControlSetting(device);
        });
    }


    private void DeviceClear_Click(object sender, RoutedEventArgs e)
    {
        GlobalSettingsStore.Instance.ProfileSettingsStore.RemoveControlSetting(ControlInputBinding);

        Device.Text = Properties.Resources.InputTextNone;
        DeviceText.Text = Properties.Resources.InputTextNone;
    }

    private void Modifier_Click(object sender, RoutedEventArgs e)
    {
        ModifierButtonClear.IsEnabled = false;
        ModifierButton.IsEnabled = false;

        InputDeviceManager.Instance.AssignButton(device =>
        {
            ModifierButtonClear.IsEnabled = true;
            ModifierButton.IsEnabled = true;

            ModifierDevice.Text = device.DeviceName;
            ModifierText.Text = GetDeviceText(device.Button, device.DeviceName);
            device.InputBind = ModifierBinding;

            GlobalSettingsStore.Instance.ProfileSettingsStore.SetControlSetting(device);
        });
    }


    private void ModifierClear_Click(object sender, RoutedEventArgs e)
    {
        GlobalSettingsStore.Instance.ProfileSettingsStore.RemoveControlSetting(ModifierBinding);
        ModifierDevice.Text = Properties.Resources.InputTextNone;
        ModifierText.Text = Properties.Resources.InputTextNone;
    }
}