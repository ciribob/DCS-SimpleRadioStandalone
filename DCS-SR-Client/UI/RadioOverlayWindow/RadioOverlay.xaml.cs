using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for RadioOverlayWindow.xaml
    /// </summary>
    public partial class RadioOverlayWindow : Window
    {
        private readonly double _aspectRatio;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Client.UI.RadioOverlayWindow.RadioControlGroup[] radioControlGroup =
            new Client.UI.RadioOverlayWindow.RadioControlGroup[3];

        private readonly DispatcherTimer _updateTimer;

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        private readonly SettingsStore _settings = SettingsStore.Instance;

        public RadioOverlayWindow()
        {
            //load opacity before the intialising as the slider changed
            //method fires after initialisation
            InitializeComponent();

            this.WindowStartupLocation = WindowStartupLocation.Manual;

            _aspectRatio = MinWidth / MinHeight;

            AllowsTransparency = true;
            Opacity = _settings.GetPositionSetting(SettingsKeys.RadioOpacity).DoubleValue;
            WindowOpacitySlider.Value = Opacity;

            radioControlGroup[0] = Radio1;
            radioControlGroup[1] = Radio2;
            radioControlGroup[2] = Radio3;

            //allows click and drag anywhere on the window
            ContainerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;

            Top = _settings.GetPositionSetting(SettingsKeys.RadioX).DoubleValue;
            Left = _settings.GetPositionSetting(SettingsKeys.RadioY).DoubleValue;

            Width = _settings.GetPositionSetting(SettingsKeys.RadioWidth).DoubleValue;
            Height = _settings.GetPositionSetting(SettingsKeys.RadioHeight).DoubleValue;

            //  Window_Loaded(null, null);
            CalculateScale();

            LocationChanged += Location_Changed;

            RadioRefresh(null, null);

            //init radio refresh
            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(80)};
            _updateTimer.Tick += RadioRefresh;
            _updateTimer.Start();
        }

        private void Location_Changed(object sender, EventArgs e)
        {
        }

        private void RadioRefresh(object sender, EventArgs eventArgs)
        {
            foreach (var radio in radioControlGroup)
            {
                radio.RepaintRadioReceive();
                radio.RepaintRadioStatus();
            }

            Intercom.RepaintRadioStatus();

            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            if ((dcsPlayerRadioInfo != null) && dcsPlayerRadioInfo.IsCurrent())
            {
                var avalilableRadios = 0;

                for (var i = 0; i < dcsPlayerRadioInfo.radios.Length; i++)
                {
                    if (dcsPlayerRadioInfo.radios[i].modulation != RadioInformation.Modulation.DISABLED)
                    {
                        avalilableRadios++;
                    }
                }

                if (avalilableRadios > 1)
                {
                    if (dcsPlayerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                    {
                        ControlText.Text = "HOTAS Controls";
                    }
                    else
                    {
                        ControlText.Text = "Cockpit Controls";
                    }
                }
                else
                {
                    ControlText.Text = "";
                }
            }
            else
            {
                ControlText.Text = "";
            }

            FocusDCS();
        }

        private long _lastFocus;

        private void FocusDCS()
        {
            if (_settings.GetClientSetting(SettingsKeys.RefocusDCS).BoolValue)
            {
                var overlayWindow = new WindowInteropHelper(this).Handle;

                //focus DCS if needed 
                var foreGround = WindowHelper.GetForegroundWindow();

                Process[] localByName = Process.GetProcessesByName("dcs");

                if (localByName != null && localByName.Length > 0)
                {
                    //either DCS is in focus OR Overlay window is not in focus
                    if (foreGround == localByName[0].MainWindowHandle || overlayWindow != foreGround ||
                        this.IsMouseOver)
                    {
                        _lastFocus = DateTime.Now.Ticks;
                    }
                    else if (DateTime.Now.Ticks > _lastFocus + 20000000 && overlayWindow == foreGround)
                    {
                        WindowHelper.BringProcessToFront(localByName[0]);
                    }
                }
            }
        }

        private void WrapPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _settings.GetPositionSetting(SettingsKeys.RadioWidth).DoubleValue = Width;
            _settings.GetPositionSetting(SettingsKeys.RadioHeight).DoubleValue = Height;
            _settings.GetPositionSetting(SettingsKeys.RadioOpacity).DoubleValue = Opacity;
            _settings.GetPositionSetting(SettingsKeys.RadioX).DoubleValue = Top;
            _settings.GetPositionSetting(SettingsKeys.RadioY).DoubleValue = Left;
            _settings.Save();
            base.OnClosing(e);

            _updateTimer.Stop();
        }

        private void Button_Minimise(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }


        private void Button_Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = e.NewValue;
        }

        private void containerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //force aspect ratio
            CalculateScale();

            WindowState = WindowState.Normal;
        }


        private void CalculateScale()
        {
            var yScale = ActualHeight / RadioOverlayWin.MinWidth;
            var xScale = ActualWidth / RadioOverlayWin.MinWidth;
            var value = Math.Min(xScale, yScale);
            ScaleValue = (double) OnCoerceScaleValue(RadioOverlayWin, value);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            if (sizeInfo.WidthChanged)
                Width = sizeInfo.NewSize.Height * _aspectRatio;
            else
                Height = sizeInfo.NewSize.Width / _aspectRatio;


            // Console.WriteLine(this.Height +" width:"+ this.Width);
        }

        #region ScaleValue Depdency Property //StackOverflow: http://stackoverflow.com/questions/3193339/tips-on-developing-resolution-independent-application/5000120#5000120

        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
            typeof(double), typeof(RadioOverlayWindow),
            new UIPropertyMetadata(1.0, OnScaleValueChanged,
                OnCoerceScaleValue));


        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            var mainWindow = o as RadioOverlayWindow;
            if (mainWindow != null)
                return mainWindow.OnCoerceScaleValue((double) value);
            return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var mainWindow = o as RadioOverlayWindow;
            if (mainWindow != null)
                mainWindow.OnScaleValueChanged((double) e.OldValue, (double) e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0f;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue)
        {
        }

        public double ScaleValue
        {
            get { return (double) GetValue(ScaleValueProperty); }
            set { SetValue(ScaleValueProperty, value); }
        }

        #endregion

        private void RadioOverlayWindow_OnLocationChanged(object sender, EventArgs e)
        {
            //reset last focus so we dont switch back to dcs while dragging
            _lastFocus = DateTime.Now.Ticks;
        }
    }
}