using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Input;
using NLog;
using SharpDX.DirectInput;
using SharpDX.XInput;
using DeviceType = SharpDX.DirectInput.DeviceType;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;



public class InputDeviceManager : IDisposable
{
[DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VirtualKeyOffset = 256; // Offset to avoid collision with DirectInput keys
    private const int MaxVirtualKey = 0xFF;   // Highest virtual key code to check
    public delegate void DetectButton(InputDevice inputDevice);

    public delegate void DetectPttCallback(List<InputBindState> buttonStates);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static HashSet<Guid> _blacklistedDevices = new()
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
    public static HashSet<Guid> _whitelistDevices = new()
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
        new Guid("205503eb-0000-0000-0000-504944564944"), // VPC Throttle
        new Guid("82c43344-0000-0000-0000-504944564944"), //  LEFT VPC Rotor TCS
        new Guid("c2ab046d-0000-0000-0000-504944564944"), // Logitech G13 Joystick
        new Guid("aaaa3344-0000-0000-0000-504944564944") //K51, it uses VPC board
    };

    private readonly DirectInput _directInput;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly Dictionary<Guid, dynamic> _inputDevices = new();

    private volatile bool _detectPtt;

    //used to trigger the update to a frequency
    private InputBinding
        _lastActiveBinding = InputBinding.ModifierIntercom; //intercom used to represent null as we cant

    private InputDeviceManager()
    {
        _directInput = new DirectInput();


        WindowHelper =
            new WindowInteropHelper(Application.Current.MainWindow);


        LoadWhiteList();

        LoadBlackList();

        InitDevices();
    }

    private WindowInteropHelper WindowHelper { get; }


    public void Dispose()
    {
        StopListening();
        foreach (var kpDevice in _inputDevices)
            if (kpDevice.Value != null)
                if (!kpDevice.Value.IsDisposed)
                    try
                    {
                        kpDevice.Value.Unacquire();
                        kpDevice.Value.Dispose();
                    }
                    catch (Exception)
                    {
                    }
    }

    public void InitDevices()
    {
        var allowXInput = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowXInputController);
        Logger.Info("Starting Device Search. Expand Search: " +
                    _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ExpandControls) +
                    ". Use XInput (for Xbox controllers): " + allowXInput);

        var deviceInstances = _directInput.GetDevices();

        _inputDevices.Clear();

        if (allowXInput) _inputDevices.Add(XInputController.DeviceGuid, new XInputController());


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
            else if ((deviceInstance.Type >= DeviceType.Joystick &&
                      deviceInstance.Type <= DeviceType.FirstPerson) ||
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
            else if (GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.ExpandControls))
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

        var lines = File.ReadAllLines(path);
        if (lines?.Length <= 0) return;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
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

    public bool IsBlackListed(Guid device)
    {
        return _blacklistedDevices.Contains(device);
    }

    public bool IsWhiteListed(Guid device)
    {
        return _whitelistDevices.Contains(device);
    }

    private static bool IsStandardKeyboardKey(int vKey)
    {
        // Modifier keys
        if (vKey == 0x10 || vKey == 0x11 || vKey == 0x12) // SHIFT, CTRL, ALT
            return true;

        // Standard keys (A-Z, 0-9, F1-F12, etc.)
        if ((vKey >= 0x30 && vKey <= 0x39) || // 0-9
            (vKey >= 0x41 && vKey <= 0x5A) || // A-Z
            (vKey >= 0x70 && vKey <= 0x7B))   // F1-F12
            return true;

        // Add more if needed, or use Keys enum for completeness
        return false;
    }

    public void AssignButton(DetectButton callback)
    {
        //detect the state of all current buttons
        Task.Run(() =>
        {
            var deviceList = _inputDevices.Values.ToList();

            var initial = new int[deviceList.Count, 128 + 4]; // for POV

            for (var i = 0; i < deviceList.Count; i++)
            {
                if (deviceList[i] == null || deviceList[i].IsDisposed) continue;

                try
                {
                    if (deviceList[i] is XInputController)
                    {
                        var state = (deviceList[i] as XInputController).GetCurrentState();
                        var values = (GamepadButtonFlags[])Enum.GetValues(typeof(GamepadButtonFlags));
                        for (var j = 0; j < values.Length; j++) initial[i, j] = state.HasFlag(values[j]) ? 1 : 0;
                    }
                    else if (deviceList[i] is Joystick)
                    {
                        var state = (deviceList[i] as Joystick).GetCurrentState();

                        for (var j = 0; j < state.Buttons.Length; j++) initial[i, j] = state.Buttons[j] ? 1 : 0;
                        var pov = state.PointOfViewControllers;

                        for (var j = 0; j < pov.Length; j++) initial[i, j + 128] = pov[j];
                    }
                    else if (deviceList[i] is Keyboard)
                    {
                        var keyboard = deviceList[i] as Keyboard;
                        var state = keyboard.GetCurrentState();

                        for (var j = 0; j < 128; j++) initial[i, j] = state.IsPressed(state.AllKeys[j]) ? 1 : 0;
                    }
                    else if (deviceList[i] is Mouse)
                    {
                        var mouse = deviceList[i] as Mouse;

                        var state = mouse.GetCurrentState();

                        for (var j = 0; j < state.Buttons.Length; j++) initial[i, j] = state.Buttons[j] ? 1 : 0;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e,
                        $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                        $"(ID: {deviceList[i].Information.ProductGuid}) while assigning button, ignoring until next restart/rediscovery");

                    deviceList[i].Unacquire();
                    deviceList[i].Dispose();
                    deviceList[i] = null;
                }
            }

            var device = string.Empty;
            var deviceGuid = Guid.Empty;
            var found = false;

            while (!found)
            {
                Thread.Sleep(100);

                for (var i = 0; i < _inputDevices.Count; i++)
                {
                    if (deviceList[i] == null || deviceList[i].IsDisposed) continue;

                    try
                    {
                        if (deviceList[i] is XInputController)
                        {
                            var state = (deviceList[i] as XInputController).GetCurrentState();
                            var values = (GamepadButtonFlags[])Enum.GetValues(typeof(GamepadButtonFlags));
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
                                        InstanceGuid = deviceList[i].Information.InstanceGuid,
                                        ButtonValue = buttonState
                                    };

                                    Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });

                                    return;
                                }
                            }
                        }
                        else if (deviceList[i] is Joystick)
                        {
                            var state = (deviceList[i] as Joystick).GetCurrentState();

                            for (var j = 0; j < 128 + 4; j++)
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
                                            InstanceGuid = deviceList[i].Information.InstanceGuid,
                                            ButtonValue = pov[j - 128]
                                        };
                                        Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });
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
                                            InstanceGuid = deviceList[i].Information.InstanceGuid,
                                            ButtonValue = buttonState
                                        };

                                        Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });


                                        return;
                                    }
                                }
                        }

                        // --- Virtual Key Detection (all possible virtual keys) ---
                        for (int vKey = 0x01; vKey <= MaxVirtualKey; vKey++)
                        {
                            bool isPressed = (GetAsyncKeyState(vKey) & 0x8000) != 0;
                            int keyIndex = VirtualKeyOffset + vKey;

                            if (isPressed)
                            {
                                if (IsStandardKeyboardKey(vKey)) {
                                    continue; // Let keyboard logic handle it
                                    }

                                // Handle as VirtualKeyboard
                                found = true;
                                var inputDevice = new InputDevice
                                {
                                    DeviceName = "VirtualKeyboard",
                                    Button = keyIndex,
                                    InstanceGuid = Guid.Empty,
                                    ButtonValue = 1
                                };
                                Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });
                                return;
                            }
                        }

                        if (deviceList[i] is Keyboard)
                        {
                            var keyboard = deviceList[i] as Keyboard;
                            var state = keyboard.GetCurrentState();

                            for (var j = 0; j < 128; j++)
                                if (initial[i, j] != (state.IsPressed(state.AllKeys[j]) ? 1 : 0))
                                {
                                    found = true;

                                    var inputDevice = new InputDevice
                                    {
                                        DeviceName =
                                            deviceList[i].Information.ProductName.Trim().Replace("\0", ""),
                                        Button = j,
                                        InstanceGuid = deviceList[i].Information.InstanceGuid,
                                        ButtonValue = 1
                                    };

                                    Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });


                                    return;
                                }
                            //                                if (initial[i, j] == 1)
                            //                                {
                            //                                    Console.WriteLine("Pressed: "+j);
                            //                                    MessageBox.Show("Keyboard!");
                            //                                }
                        }
                        if (deviceList[i] is Mouse)
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
                                        InstanceGuid = deviceList[i].Information.InstanceGuid,
                                        ButtonValue = buttonState
                                    };

                                    Application.Current.Dispatcher.Invoke(() => { callback(inputDevice); });
                                    return;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e,
                            $"Failed to get current state of input device {deviceList[i].Information.ProductName.Trim().Replace("\0", "")} " +
                            $"(ID: {deviceList[i].Information.ProductGuid}) while discovering button press while assigning, ignoring until next restart/rediscovery");

                        deviceList[i].Unacquire();
                        deviceList[i].Dispose();
                        deviceList[i] = null;
                    }
                }
            }
        });
    }


    public void StartPTTListening(DetectPttCallback callback)
    {
        _detectPtt = true;
        //detect the state of all current buttons
        var pttInputThread = new Thread(() =>
        {
            while (_detectPtt)
            {
                var bindStates = GenerateBindStateList();

                for (var i = 0; i < bindStates.Count; i++)
                {
                    //contains main binding and optional modifier binding + states of each
                    var bindState = bindStates[i];

                    bindState.MainDeviceState = GetButtonState(bindState.MainDevice);

                    if (bindState.ModifierDevice != null)
                    {
                        bindState.ModifierState = GetButtonState(bindState.ModifierDevice);

                        bindState.IsActive = bindState.MainDeviceState && bindState.ModifierState;
                    }
                    else
                    {
                        bindState.IsActive = bindState.MainDeviceState;
                    }

                    //now check this is the best binding and no previous ones are better
                    //Means you can have better binds like PTT  = Space and Radio 1 is Space +1 - holding space +1 will actually trigger radio 1 not PTT
                    if (bindState.IsActive)
                        for (var j = 0; j < i; j++)
                        {
                            //check previous bindings
                            var previousBind = bindStates[j];

                            if (!previousBind.IsActive) continue;

                            if (previousBind.ModifierDevice == null && bindState.ModifierDevice != null)
                            {
                                //set previous bind to off if previous bind Main == main or modifier of bindstate
                                if (previousBind.MainDevice.IsSameBind(bindState.MainDevice))
                                {
                                    previousBind.IsActive = false;
                                    break;
                                }

                                if (previousBind.MainDevice.IsSameBind(bindState.ModifierDevice))
                                {
                                    previousBind.IsActive = false;
                                    break;
                                }
                            }
                            else if (previousBind.ModifierDevice != null && bindState.ModifierDevice == null)
                            {
                                if (previousBind.MainDevice.IsSameBind(bindState.MainDevice))
                                {
                                    bindState.IsActive = false;
                                    break;
                                }

                                if (previousBind.ModifierDevice.IsSameBind(bindState.MainDevice))
                                {
                                    bindState.IsActive = false;
                                    break;
                                }
                            }
                        }
                }

                callback?.Invoke(bindStates);

                //handle overlay

                foreach (var bindState in bindStates)
                {
                    if (bindState.IsActive && bindState.MainDevice.InputBind == InputBinding.OverlayToggle)
                    {
                        EventBus.Instance.PublishOnUIThreadAsync(new ToggleSingleStackRadioOverlayMessage());
                        break;
                    }

                    if (bindState.IsActive && bindState.MainDevice.InputBind == InputBinding.AwacsOverlayToggle)
                    {
                        EventBus.Instance.PublishOnUIThreadAsync(new ToogleAwacsRadioOverlayMessage());
                        break;
                    }

                    if ((int)bindState.MainDevice.InputBind >= (int)InputBinding.Up100 &&
                        (int)bindState.MainDevice.InputBind <= (int)InputBinding.AwacsOverlayToggle)
                    {
                        if (bindState.MainDevice.InputBind == _lastActiveBinding && !bindState.IsActive)
                            //Assign to a totally different binding to mark as unassign
                            _lastActiveBinding = InputBinding.ModifierIntercom;
                        //key repeat
                        if (bindState.IsActive && bindState.MainDevice.InputBind != _lastActiveBinding)
                        {
                            _lastActiveBinding = bindState.MainDevice.InputBind;

                            var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

                            if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent())
                                switch (bindState.MainDevice.InputBind)
                                {
                                    case InputBinding.Up100:
                                        RadioHelper.UpdateRadioFrequency(100, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Up10:
                                        RadioHelper.UpdateRadioFrequency(10, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Up1:
                                        RadioHelper.UpdateRadioFrequency(1, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Up01:
                                        RadioHelper.UpdateRadioFrequency(0.1, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Up001:
                                        RadioHelper.UpdateRadioFrequency(0.01, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Up0001:
                                        RadioHelper.UpdateRadioFrequency(0.001, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Down100:
                                        RadioHelper.UpdateRadioFrequency(-100, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Down10:
                                        RadioHelper.UpdateRadioFrequency(-10, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Down1:
                                        RadioHelper.UpdateRadioFrequency(-1, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Down01:
                                        RadioHelper.UpdateRadioFrequency(-0.1, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Down001:
                                        RadioHelper.UpdateRadioFrequency(-0.01, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.Down0001:
                                        RadioHelper.UpdateRadioFrequency(-0.001, dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.ToggleGuard:
                                        RadioHelper.ToggleGuard(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.ToggleEncryption:
                                        RadioHelper.ToggleEncryption(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.NextRadio:
                                        RadioHelper.SelectNextRadio();
                                        break;
                                    case InputBinding.PreviousRadio:
                                        RadioHelper.SelectPreviousRadio();
                                        break;
                                    case InputBinding.EncryptionKeyIncrease:
                                        RadioHelper.IncreaseEncryptionKey(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.EncryptionKeyDecrease:
                                        RadioHelper.DecreaseEncryptionKey(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.RadioChannelUp:
                                        RadioHelper.RadioChannelUp(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.RadioChannelDown:
                                        RadioHelper.RadioChannelDown(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.TransponderIDENT:
                                        TransponderHelper.ToggleIdent();
                                        break;
                                    case InputBinding.RadioVolumeUp:
                                        RadioHelper.RadioVolumeUp(dcsPlayerRadioInfo.selected);
                                        break;
                                    case InputBinding.RadioVolumeDown:
                                        RadioHelper.RadioVolumeDown(dcsPlayerRadioInfo.selected);
                                        break;
                                }


                            break;
                        }
                    }
                }

                Thread.Sleep(40);
            }
        });
        pttInputThread.Start();
    }


    public void StopListening()
    {
        _detectPtt = false;
    }

    private bool GetButtonState(InputDevice inputDeviceBinding)
    {
        // Check for virtual key
        if (inputDeviceBinding.Button >= VirtualKeyOffset && inputDeviceBinding.Button <= VirtualKeyOffset + MaxVirtualKey)
        {
            int vKey = inputDeviceBinding.Button - VirtualKeyOffset;
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        foreach (var kpDevice in _inputDevices)
        {
            var device = kpDevice.Value;
            if (device == null ||
                device.IsDisposed ||
                !device.Information.InstanceGuid.Equals(inputDeviceBinding.InstanceGuid))
                continue;

            try
            {
                if (device is XInputController)
                {
                    var state = (device as XInputController).GetCurrentState();
                    return state.HasFlag((GamepadButtonFlags)inputDeviceBinding.Button);
                }

                if (device is Joystick)
                {
                    //device.Poll();
                    var state = (device as Joystick).GetCurrentState();

                    if (inputDeviceBinding.Button >= 128) //its a POV!
                    {
                        var pov = state.PointOfViewControllers;
                        //-128 to get POV index
                        return pov[inputDeviceBinding.Button - 128] == inputDeviceBinding.ButtonValue;
                    }

                    return state.Buttons[inputDeviceBinding.Button];
                }

                if (device is Keyboard)
                {
                    var keyboard = device as Keyboard;
                    // keyboard.Poll();
                    var state = keyboard.GetCurrentState();
                    return
                        state.IsPressed(state.AllKeys[inputDeviceBinding.Button]);
                }

                if (device is Mouse)
                {
                    // device.Poll();
                    var state = (device as Mouse).GetCurrentState();

                    //just incase mouse changes number of buttons, like logitech can?
                    if (inputDeviceBinding.Button < state.Buttons.Length)
                        return state.Buttons[inputDeviceBinding.Button];
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

        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"An error occurred while querying your {deviceName} input device.\nThis could for example be caused by unplugging " +
                $"your joystick or disabling it in the Windows settings.\n\nAll controls bound to this input device will not work anymore until your press 'Rescan Controller Input' in the SRS controls section or restart SRS",
                "Input device error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }

    public List<InputBindState> GenerateBindStateList()
    {
        var bindStates = new List<InputBindState>();
        var currentInputProfile = _globalSettings.ProfileSettingsStore.GetCurrentInputProfile();

        //REMEMBER TO UPDATE THIS WHEN NEW BINDINGS ARE ADDED
        //MIN + MAX bind numbers
        for (var i = (int)InputBinding.Intercom; i <= (int)InputBinding.AwacsOverlayToggle; i++)
        {
            if (!currentInputProfile.ContainsKey((InputBinding)i)) continue;

            var input = currentInputProfile[(InputBinding)i];
            //construct InputBindState

            var bindState = new InputBindState
            {
                IsActive = false,
                MainDevice = input,
                MainDeviceState = false,
                ModifierDevice = null,
                ModifierState = false
            };

            if (currentInputProfile.ContainsKey((InputBinding)i + 100))
                bindState.ModifierDevice = currentInputProfile[(InputBinding)i + 100];

            bindStates.Add(bindState);
        }

        return bindStates;
    }

    #region Singleton Definition

    private static volatile InputDeviceManager _instance;
    private static readonly object _lock = new();

    public static InputDeviceManager Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new InputDeviceManager();
                }

            return _instance;
        }
    }

    #endregion
}