using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using CommunityToolkit.Mvvm.DependencyInjection;
using DCS_SR_Client;
using NLog;
using SharpDX.DirectInput;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
    public class InputDeviceManager : IDisposable
    {
        public delegate void DetectButton(InputDevice inputDevice);

        public delegate void DetectPttCallback(List<InputBindingModel> buttonStates);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static HashSet<Guid> _blacklistedDevices = new HashSet<Guid>
        {
            new Guid("1b171b1c-0000-0000-0000-504944564944"),
            //Corsair K65 Gaming keyboard  It reports as a Joystick when its a keyboard...
            new Guid("1b091b1c-0000-0000-0000-504944564944"), // Corsair K70R Gaming Keyboard
            new Guid("1b1e1b1c-0000-0000-0000-504944564944"), //Corsair Gaming Scimitar RGB Mouse
            new Guid("16a40951-0000-0000-0000-504944564944"), //HyperX 7.1 Audio
            new Guid("b660044f-0000-0000-0000-504944564944"), // T500 RS Gear Shift
            new Guid("00f2068e-0000-0000-0000-504944564944") //CH PRO PEDALS USB
        };

        //devices that report incorrectly but SHOULD work?
        public static HashSet<Guid> _whitelistDevices = new HashSet<Guid>
        {
            new Guid("1105231d-0000-0000-0000-504944564944"), //GTX Throttle
            new Guid("b351044f-0000-0000-0000-504944564944"), //F16 MFD 1 Usage: Generic Type: Supplemental
            new Guid("b352044f-0000-0000-0000-504944564944"), //F16 MFD 2 Usage: Generic Type: Supplemental
            new Guid("b353044f-0000-0000-0000-504944564944"), //F16 MFD 3 Usage: Generic Type: Supplemental
            new Guid("b354044f-0000-0000-0000-504944564944"), //F16 MFD 4 Usage: Generic Type: Supplemental
            new Guid("b355044f-0000-0000-0000-504944564944"), //F16 MFD 5 Usage: Generic Type: Supplemental
            new Guid("b356044f-0000-0000-0000-504944564944"), //F16 MFD 6 Usage: Generic Type: Supplemental
            new Guid("b357044f-0000-0000-0000-504944564944"), //F16 MFD 7 Usage: Generic Type: Supplemental
            new Guid("b358044f-0000-0000-0000-504944564944"), //F16 MFD 8 Usage: Generic Type: Supplemental
            new Guid("11401dd2-0000-0000-0000-504944564944"), //Leo Bodnar BUtton Box
            new Guid("204803eb-0000-0000-0000-504944564944"), // VPC Throttle
            new Guid("204303eb-0000-0000-0000-504944564944"), // VPC Stick
            new Guid("205403eb-0000-0000-0000-504944564944"), // VPC Throttle
            new Guid("205603eb-0000-0000-0000-504944564944"), // VPC Throttle
            new Guid("205503eb-0000-0000-0000-504944564944"),  // VPC Throttle
            new Guid("82c43344-0000-0000-0000-504944564944"),  //  LEFT VPC Rotor TCS
            new Guid("c2ab046d-0000-0000-0000-504944564944"),  // Logitech G13 Joystick
            new Guid("aaaa3344-0000-0000-0000-504944564944") //K51, it uses VPC board
            

        };

        private readonly DirectInput _directInput;
        private readonly Dictionary<Guid, dynamic> _inputDevices = new Dictionary<Guid, dynamic>();
        private readonly MainWindow.ToggleOverlayCallback _toggleOverlayCallback;

        private volatile bool _detectPtt;

        //used to trigger the update to a frequency
        private InputBindingModel _lastActiveBinding; 
        //intercom used to represent null as we cant

        private ISrsSettings GlobalSettings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>();
        private ProfileSettingsModel ProfileSettings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>().CurrentProfile;

        public InputDeviceManager(Window window, MainWindow.ToggleOverlayCallback _toggleOverlayCallback)
        {
            _directInput = new DirectInput();


            WindowHelper =
                new WindowInteropHelper(window);

            this._toggleOverlayCallback = _toggleOverlayCallback;

            LoadWhiteList();

            LoadBlackList();

            InitDevices();
        }

        public void InitDevices()
        {
            var allowXInput = GlobalSettings.ClientSettings.AllowXInputController;
            Logger.Info("Starting Device Search. Expand Search: " +
            (GlobalSettings.ClientSettings.ExpandControls) +
            ". Use XInput (for Xbox controllers): " + allowXInput);


            var deviceInstances = _directInput.GetDevices();

            _inputDevices.Clear();

            if (allowXInput)
            {
                _inputDevices.Add(XInputController.DeviceGuid, new XInputController());
            }
            

            foreach (var deviceInstance in deviceInstances)
            {
                //Workaround for Bad Devices that pretend to be joysticks
                if (IsBlackListed(deviceInstance.ProductGuid))
                {
                    Logger.Info("Found but ignoring blacklist device  " + deviceInstance.ProductGuid + " Instance: " +
                        deviceInstance.InstanceGuid + " " +
                        deviceInstance.ProductName.Trim().Replace("\0", "") + " Type: " + deviceInstance.Type);
                    continue;
                }

                Logger.Info("Found Device ID:" + deviceInstance.ProductGuid +
                            " " +
                            deviceInstance.ProductName.Trim().Replace("\0", "") + " Usage: " +
                            deviceInstance.UsagePage + " Type: " +
                            deviceInstance.Type);
                if (_inputDevices.ContainsKey(deviceInstance.InstanceGuid))
                {
                    Logger.Info("Already have device:" + deviceInstance.ProductGuid +
                                " " +
                                deviceInstance.ProductName.Trim().Replace("\0", ""));
                    continue;
                }


                if (deviceInstance.Type == DeviceType.Keyboard)
                {

                    Logger.Info("Adding Device ID:" + deviceInstance.ProductGuid +
                                " " +
                                deviceInstance.ProductName.Trim().Replace("\0", ""));
                    var device = new Keyboard(_directInput);

                    device.SetCooperativeLevel(WindowHelper.Handle,
                        CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                    device.Acquire();

                    _inputDevices.Add(deviceInstance.InstanceGuid, device);
                }
                else if (deviceInstance.Type == DeviceType.Mouse)
                {
                    Logger.Info("Adding Device ID:" + deviceInstance.ProductGuid + " " +
                                deviceInstance.ProductName.Trim().Replace("\0", ""));
                    var device = new Mouse(_directInput);

                    device.SetCooperativeLevel(WindowHelper.Handle,
                        CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                    device.Acquire();

                    _inputDevices.Add(deviceInstance.InstanceGuid, device);
                }
                else if (((deviceInstance.Type >= DeviceType.Joystick) &&
                            (deviceInstance.Type <= DeviceType.FirstPerson)) ||
                            IsWhiteListed(deviceInstance.ProductGuid))
                {
                    var device = new Joystick(_directInput, deviceInstance.InstanceGuid);

                    Logger.Info("Adding ID:" + deviceInstance.ProductGuid + " " +
                                deviceInstance.ProductName.Trim().Replace("\0", ""));

                    device.SetCooperativeLevel(WindowHelper.Handle,
                        CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                    device.Acquire();

                    _inputDevices.Add(deviceInstance.InstanceGuid, device);
                }
                else if (GlobalSettings.ClientSettings.ExpandControls)
                {
                    Logger.Info("Adding (Expanded Devices) ID:" + deviceInstance.ProductGuid + " " +
                                deviceInstance.ProductName.Trim().Replace("\0", ""));

                    var device = new Joystick(_directInput, deviceInstance.InstanceGuid);

                    device.SetCooperativeLevel(WindowHelper.Handle,
                        CooperativeLevel.Background | CooperativeLevel.NonExclusive);
                    device.Acquire();

                    _inputDevices.Add(deviceInstance.InstanceGuid, device);

                    Logger.Info("Added (Expanded Device) ID:" + deviceInstance.ProductGuid + " " +
                                deviceInstance.ProductName.Trim().Replace("\0", ""));
                }
            }
        }

        private void LoadWhiteList()
        {
            var path = Environment.CurrentDirectory + "\\whitelist.txt";
            Logger.Info("Attempt to Load Whitelist from " + path);

            LoadGuidFromPath(path, _whitelistDevices);
        }

        private void LoadBlackList()
        {
            var path = Environment.CurrentDirectory + "\\blacklist.txt";
            Logger.Info("Attempt to Load Blacklist from " + path);

            LoadGuidFromPath(path, _blacklistedDevices);
        }

        private void LoadGuidFromPath(string path, HashSet<Guid> _hashSet)
        {
            if (!File.Exists(path))
            {
                Logger.Info("File doesnt exist: " + path);
                return;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines?.Length <= 0)
            {
                return;

            }

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    try
                    {
                        _hashSet.Add(new Guid(trimmed));
                        Logger.Info("Added " + trimmed);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private WindowInteropHelper WindowHelper { get; }


        public void Dispose()
        {
            StopPtt();
            foreach (var kpDevice in _inputDevices)
            {
                if (kpDevice.Value != null)
                {
                    if (!kpDevice.Value.IsDisposed)
                    {
                        kpDevice.Value.Unacquire();
                        kpDevice.Value.Dispose();
                    }
                }
            }
        }

        public bool IsBlackListed(Guid device)
        {
            return _blacklistedDevices.Contains(device);
        }

        public bool IsWhiteListed(Guid device)
        {
            return _whitelistDevices.Contains(device);
        }

        private void PollAllDevices()
        {
            var deviceList = _inputDevices.Values.ToList();

            for (var i = 0; i < deviceList.Count; i++)
            {
                if (deviceList[i] == null || deviceList[i].IsDisposed)
                {
                    continue;
                }

                try
                {
                    deviceList[i].Poll();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                                    $"(ID: {deviceList[i].Information.ProductGuid}) while assigning button, ignoring until next restart/rediscovery");
                    try
                    {
                        deviceList[i].Unacquire();
                        deviceList[i].Dispose();
                        deviceList[i] = null;
                    }
                    catch (Exception)
                    {
                    }

                }
            }
        }

        public void AssignButton(DetectButton callback)
        {
            //detect the state of all current buttons
            Task.Run(() =>
            {
                var deviceList = _inputDevices.Values.ToList();

                var initial = new int[deviceList.Count, 128 + 4]; // for POV

                PollAllDevices();

                for (var i = 0; i < deviceList.Count; i++)
                {
                    if (deviceList[i] == null || deviceList[i].IsDisposed)
                    {
                        continue;
                    }

                    try
                    {
                        if (deviceList[i] is XInputController)
                        {
                            var state = (deviceList[i] as XInputController).GetCurrentState();
                            var values = (SharpDX.XInput.GamepadButtonFlags[])Enum.GetValues(typeof(SharpDX.XInput.GamepadButtonFlags));
                            for (var j = 0; j < values.Length; j++)
                            {
                                initial[i, j] = state.HasFlag(values[j]) ? 1 : 0;
                            }
                        }
                        else if (deviceList[i] is Joystick)
                        {

                            var state = (deviceList[i] as Joystick).GetCurrentState();

                            for (var j = 0; j < state.Buttons.Length; j++)
                            {
                                initial[i, j] = state.Buttons[j] ? 1 : 0;
                            }
                            var pov = state.PointOfViewControllers;

                            for (var j = 0; j < pov.Length; j++)
                            {
                                initial[i, j + 128] = pov[j];
                            }
                        }
                        else if (deviceList[i] is Keyboard)
                        {
                            var keyboard = deviceList[i] as Keyboard;
                            var state = keyboard.GetCurrentState();

                            for (var j = 0; j < 128; j++)
                            {
                                initial[i, j] = state.IsPressed(state.AllKeys[j]) ? 1 : 0;
                            }
                        }
                        else if (deviceList[i] is Mouse)
                        {
                            var mouse = deviceList[i] as Mouse;

                            var state = mouse.GetCurrentState();

                            for (var j = 0; j < state.Buttons.Length; j++)
                            {
                                initial[i, j] = state.Buttons[j] ? 1 : 0;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                            $"(ID: {deviceList[i].Information.ProductGuid}) while assigning button, ignoring until next restart/rediscovery");

                        deviceList[i].Unacquire();
                        deviceList[i].Dispose();
                        deviceList[i] = null;
                    }
                }

                var device = string.Empty;
                var button = 0;
                var deviceGuid = Guid.Empty;
                var buttonValue = -1;
                var found = false;

                while (!found)
                {
                    Thread.Sleep(100);

                    PollAllDevices();

                    for (var i = 0; i < _inputDevices.Count; i++)
                    {
                        if (deviceList[i] == null || deviceList[i].IsDisposed)
                        {
                            continue;
                        }

                        try
                        {
                            if (deviceList[i] is XInputController)
                            {
                                var state = (deviceList[i] as XInputController).GetCurrentState();
                                var values = (SharpDX.XInput.GamepadButtonFlags[])Enum.GetValues(typeof(SharpDX.XInput.GamepadButtonFlags));
                                for (var j = 0; j < values.Length; j++)
                                {
                                    var buttonState = state.HasFlag(values[j]) ? 1 : 0;
                                    if (buttonState != initial[i, j])
                                    {
                                        found = true;
                                        var inputDevice = new InputDevice
                                        {
                                            DeviceName = "XInputController",
                                            Button = (int)values[j],
                                            Guid = deviceList[i].Information.InstanceGuid,
                                            ButtonValue = buttonState
                                        };

                                        Application.Current.Dispatcher.Invoke(
                                            () => { callback(inputDevice); });

                                        return;
                                    }
                                }
                            }
                            else if (deviceList[i] is Joystick)
                            {
                                var state = (deviceList[i] as Joystick).GetCurrentState();

                                for (var j = 0; j < 128 + 4; j++)
                                {
                                    if (j >= 128)
                                    {
                                        //handle POV
                                        var pov = state.PointOfViewControllers;

                                        if (pov[j - 128] != initial[i, j])
                                        {
                                            found = true;

                                            var inputDevice = new InputDevice
                                            {
                                                DeviceName =
                                                    deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                                Button = j,
                                                Guid = deviceList[i].Information.InstanceGuid,
                                                ButtonValue = pov[j - 128]
                                            };
                                            Application.Current.Dispatcher.Invoke(
                                                () => { callback(inputDevice); });
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        var buttonState = state.Buttons[j] ? 1 : 0;

                                        if (buttonState == 1 && buttonState != initial[i, j])
                                        {
                                            found = true;

                                            var inputDevice = new InputDevice
                                            {
                                                DeviceName =
                                                    deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                                Button = j,
                                                Guid = deviceList[i].Information.InstanceGuid,
                                                ButtonValue = buttonState
                                            };

                                            Application.Current.Dispatcher.Invoke(
                                                () => { callback(inputDevice); });


                                            return;
                                        }
                                    }
                                }
                            }
                            else if (deviceList[i] is Keyboard)
                            {
                                var keyboard = deviceList[i] as Keyboard;
                                var state = keyboard.GetCurrentState();

                                for (var j = 0; j < 128; j++)
                                {
                                    if (initial[i, j] != (state.IsPressed(state.AllKeys[j]) ? 1 : 0))
                                    {
                                        found = true;

                                        var inputDevice = new InputDevice
                                        {
                                            DeviceName =
                                                deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                            Button = j,
                                            Guid = deviceList[i].Information.InstanceGuid,
                                            ButtonValue = 1
                                        };

                                        Application.Current.Dispatcher.Invoke(
                                            () => { callback(inputDevice); });


                                        return;
                                    }

                                    //                                if (initial[i, j] == 1)
                                    //                                {
                                    //                                    Console.WriteLine("Pressed: "+j);
                                    //                                    MessageBox.Show("Keyboard!");
                                    //                                }
                                }
                            }
                            else if (deviceList[i] is Mouse)
                            {
                                var state = (deviceList[i] as Mouse).GetCurrentState();

                                //skip left mouse button - start at 1 with j 0 is left, 1 is right, 2 is middle
                                for (var j = 1; j < state.Buttons.Length; j++)
                                {
                                    var buttonState = state.Buttons[j] ? 1 : 0;

                                    if (buttonState != initial[i, j])
                                    {
                                        found = true;

                                        var inputDevice = new InputDevice
                                        {
                                            DeviceName =
                                                deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                            Button = j,
                                            Guid = deviceList[i].Information.InstanceGuid,
                                            ButtonValue = buttonState
                                        };

                                        Application.Current.Dispatcher.Invoke(
                                            () => { callback(inputDevice); });
                                        return;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                                $"(ID: {deviceList[i].Information.ProductGuid}) while discovering button press while assigning, ignoring until next restart/rediscovery");

                            deviceList[i].Unacquire();
                            deviceList[i].Dispose();
                            deviceList[i] = null;
                        }
                    }
                }
            });
        }
        

        private void PollDevices(List<InputBindingModel> states)
        {
            //generate a unique list of devices - only poll once around the loop - not for each keybind
            var uniqueDevices = new HashSet<Guid>();

            foreach (var inputBindState in states)
            {
                if (inputBindState.Primary != null)
                {
                    uniqueDevices.Add(inputBindState.Primary.Guid);
                }
                if (inputBindState.Modifier != null)
                {
                    uniqueDevices.Add(inputBindState.Modifier.Guid);
                }
            }

            foreach (var deviceGuid in uniqueDevices)
            {
                foreach (var kpDevice in _inputDevices)
                {
                    var device = kpDevice.Value;
                    if (device == null ||
                        device.IsDisposed ||
                        !device.Information.InstanceGuid.Equals(deviceGuid))
                    {
                        continue;
                    }

                    try {
                        //poll the device as it has a bind
                        device.Poll();
                    }
                    catch (Exception e)
                    {
                        // ignored
                        DeviceError(device,e);
                    }
                }
            }
        }

        public void StartDetectPtt(DetectPttCallback callback)
        {
            _detectPtt = true;
            //detect the state of all current buttons
            var pttInputThread = new Thread(() =>
            {
                while (_detectPtt)
                {
                    var allBinding = ProfileSettings.InputBindingsList;
                    
                    //Poll devices with all current binds
                    PollDevices(allBinding);
                    
                    foreach (var bindState in allBinding)
                    {
                        bindState.IsPrimaryPressed = GetButtonState(bindState.Primary);
                        bindState.IsModifiedPressed = GetButtonState(bindState.Modifier);
                        
                        //now check this is the best binding and no previous ones are better
                        //Means you can have better binds like PTT  = Space and Radio 1 is Space +1 - holding space +1 will actually trigger radio 1 not PTT
                        if (bindState.IsBindingPressed)
                        {
                            foreach (var otherBindState in allBinding.Where(x => x.IsBindingPressed))
                            {
                                if(otherBindState == bindState) break;
                                otherBindState.IsPrimaryPressed = false;
                            }
                        }
                    }

                    callback(allBinding);
                    
                    //handle overlay
                    var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;
                    if (ProfileSettings.OverlayToggle.IsBindingPressed)
                    {
                        //run on main
                        Application.Current.Dispatcher.Invoke(
                            () => { _toggleOverlayCallback(false,false); });
                    }
                    if (ProfileSettings.AwacsOverlayToggle.IsBindingPressed)
                    {
                        //run on main
                        Application.Current.Dispatcher.Invoke(
                            () => { _toggleOverlayCallback(false, true); });
                    }
                    if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent())
                    {
                        if(ProfileSettings.InputUp100.IsBindingPressed) RadioHelper.UpdateRadioFrequency(100, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputUp10.IsBindingPressed) RadioHelper.UpdateRadioFrequency(10, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputUp1.IsBindingPressed) RadioHelper.UpdateRadioFrequency(1, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputUp01.IsBindingPressed) RadioHelper.UpdateRadioFrequency(0.1, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputUp001.IsBindingPressed) RadioHelper.UpdateRadioFrequency(0.01, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputUp0001.IsBindingPressed) RadioHelper.UpdateRadioFrequency(0.001, dcsPlayerRadioInfo.selected);
                        
                        if(ProfileSettings.InputDown100.IsBindingPressed) RadioHelper.UpdateRadioFrequency(-100, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputDown10.IsBindingPressed) RadioHelper.UpdateRadioFrequency(-10, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputDown1.IsBindingPressed) RadioHelper.UpdateRadioFrequency(-1, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputDown01.IsBindingPressed) RadioHelper.UpdateRadioFrequency(-0.1, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputDown001.IsBindingPressed) RadioHelper.UpdateRadioFrequency(-0.01, dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.InputDown0001.IsBindingPressed) RadioHelper.UpdateRadioFrequency(-0.001, dcsPlayerRadioInfo.selected);
                        
                        if(ProfileSettings.GuardToggle.IsBindingPressed) RadioHelper.ToggleGuard(dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.EncryptionToggle.IsBindingPressed) RadioHelper.ToggleEncryption(dcsPlayerRadioInfo.selected);
                        
                        if(ProfileSettings.RadioNext.IsBindingPressed) RadioHelper.SelectNextRadio();
                        if(ProfileSettings.RadioPrevious.IsBindingPressed) RadioHelper.SelectPreviousRadio();
                        
                        if(ProfileSettings.EncryptionKeyUp.IsBindingPressed) RadioHelper.IncreaseEncryptionKey(dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.EncryptionKeyDown.IsBindingPressed) RadioHelper.DecreaseEncryptionKey(dcsPlayerRadioInfo.selected);
                        
                        if(ProfileSettings.RadioChannelUp.IsBindingPressed) RadioHelper.RadioChannelUp(dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.RadioChannelDown.IsBindingPressed) RadioHelper.RadioChannelDown(dcsPlayerRadioInfo.selected);
                        
                        if(ProfileSettings.TransponderIdent.IsBindingPressed) TransponderHelper.ToggleIdent();
                        
                        if(ProfileSettings.RadioVolumeUp.IsBindingPressed) RadioHelper.RadioVolumeUp(dcsPlayerRadioInfo.selected);
                        if(ProfileSettings.RadioVolumeDown.IsBindingPressed) RadioHelper.RadioVolumeDown(dcsPlayerRadioInfo.selected);
                    } 
                    Thread.Sleep(100);
                }
            });
            pttInputThread.Start();
        }


        public void StopPtt()
        {
            _detectPtt = false;
        }

        private bool GetButtonState(InputDevice inputDeviceBinding)
        {
            foreach (var kpDevice in _inputDevices)
            {
                var device = kpDevice.Value;
                if (device == null ||
                    device.IsDisposed ||
                    !device.Information.InstanceGuid.Equals(inputDeviceBinding.Guid))
                {
                    continue;
                }

                try
                {
                    if (device is XInputController)
                    {
                        var state = (device as XInputController).GetCurrentState();
                        return state.HasFlag((SharpDX.XInput.GamepadButtonFlags)inputDeviceBinding.Button);
                    }
                    else if (device is Joystick)
                    {
                        //device.Poll();
                        var state = (device as Joystick).GetCurrentState();

                        if (inputDeviceBinding.Button >= 128) //its a POV!
                        {
                            var pov = state.PointOfViewControllers;
                            //-128 to get POV index
                            return pov[inputDeviceBinding.Button - 128] == inputDeviceBinding.ButtonValue;
                        }
                        else
                        {
                            return state.Buttons[inputDeviceBinding.Button];
                        }
                    }
                    else if (device is Keyboard)
                    {
                        var keyboard = device as Keyboard;
                       // keyboard.Poll();
                        var state = keyboard.GetCurrentState();
                        return
                            state.IsPressed(state.AllKeys[inputDeviceBinding.Button]);
                    }
                    else if (device is Mouse)
                    {
                       // device.Poll();
                        var state = (device as Mouse).GetCurrentState();

                        //just incase mouse changes number of buttons, like logitech can?
                        if (inputDeviceBinding.Button < state.Buttons.Length)
                        {
                            return state.Buttons[inputDeviceBinding.Button];
                        }
                    }
                }
                catch (Exception e)
                {
                   DeviceError(device, e);
                }

            }
            return false;
        }

        private void DeviceError(Device device, Exception e)
        {
            var deviceName = device.Information.ProductName.Trim().Replace("\0", "");
            Logger.Error(e, $"Failed to get current state of input device {deviceName} " +
                            $"(ID: {device.Information.ProductGuid}) while retrieving button state, ignoring until next restart/rediscovery");

            
            try
            {
                device.Unacquire();
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                device.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }

            Application.Current.Dispatcher.Invoke(() => {
                MessageBox.Show(
                    $"An error occurred while querying your {deviceName} input device.\nThis could for example be caused by unplugging " +
                    $"your joystick or disabling it in the Windows settings.\n\nAll controls bound to this input device will not work anymore until your press 'Rescan Controller Input' in the SRS controls section or restart SRS",
                    "Input device error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }
}