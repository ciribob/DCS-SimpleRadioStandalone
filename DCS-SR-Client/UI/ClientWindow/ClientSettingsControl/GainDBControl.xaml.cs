using System.Windows;
using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

/// <summary>
///     Interaction logic for GainDBControl.xaml
/// </summary>
public partial class GainDBControl : UserControl
{
    public static readonly DependencyProperty GainSliderDependencyProperty =
        DependencyProperty.Register("GainDBValue", typeof(float), typeof(GainDBControl),
            new FrameworkPropertyMetadata((float)0)
        );

    public GainDBControl()
    {
        InitializeComponent();
    }

    public float GainDBValue
    {
        set => SetValue(GainSliderDependencyProperty, value);
        get
        {
            var val = (float)GetValue(GainSliderDependencyProperty);
            return val;
        }
    }
}