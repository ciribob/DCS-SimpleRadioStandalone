using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

public partial class BackgroundEffectVolumeControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty VolumeSliderDependencyProperty =
        DependencyProperty.Register("VolumeValue", typeof(float), typeof(BackgroundEffectVolumeControl),
            new FrameworkPropertyMetadata((float)0)
        );

    public static readonly DependencyProperty MaximumPercentageProperty =
        DependencyProperty.Register(
            nameof(MaximumPercentage),
            typeof(float),
            typeof(BackgroundEffectVolumeControl),
            new PropertyMetadata(200f, OnMaximumPercentageChanged));

    public BackgroundEffectVolumeControl()
    {
        InitializeComponent();
    }

    public float VolumeValue
    {
        set => SetValue(VolumeSliderDependencyProperty, value);
        get => (float)GetValue(VolumeSliderDependencyProperty);
    }

    public float MaximumPercentage
    {
        get => (float)GetValue(MaximumPercentageProperty);
        set => SetValue(MaximumPercentageProperty, value);
    }

    public float HalfMaximumPercentage => MaximumPercentage / 2f;

    private static void OnMaximumPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BackgroundEffectVolumeControl)d;
        control.OnPropertyChanged(nameof(HalfMaximumPercentage));
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}