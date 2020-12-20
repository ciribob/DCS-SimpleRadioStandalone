using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public class AudioInputSingleton : IDisposable, IMMNotificationClient, INotifyPropertyChanged
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Singleton Definition
        private static volatile AudioInputSingleton _instance;
        private static object _lock = new Object();

        public static AudioInputSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new AudioInputSingleton();
                    }
                }

                return _instance;
            }
        }
        #endregion

        #region Instance Definition
        private MMDeviceEnumerator _deviceEnum;
        private AudioDeviceListItem _selectedAudioInput;
        private List<AudioDeviceListItem> _inputAudioDevices;

        public event PropertyChangedEventHandler PropertyChanged;

        public List<AudioDeviceListItem> InputAudioDevices
        {
            get => _inputAudioDevices; private set
            {
                _inputAudioDevices = value;
                OnPropertyChanged();
                OnPropertyChanged("MicrophoneAvailable");
            }
        }

        public AudioDeviceListItem SelectedAudioInput
        {
            get => _selectedAudioInput;
            set
            {
                _selectedAudioInput = value;
                OnPropertyChanged();
            }
        }

        // Indicates whether a valid microphone is available - deactivating audio input controls and transmissions otherwise
        public bool MicrophoneAvailable => InputAudioDevices.Any();

        private AudioInputSingleton()
        {
            _deviceEnum = new MMDeviceEnumerator();
            InputAudioDevices = BuildAudioInputs();

            _deviceEnum.RegisterEndpointNotificationCallback(this);
        }

        private List<AudioDeviceListItem> BuildAudioInputs()
        {
            Logger.Info("Audio Input - Saved ID " +
                        GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue);

            var inputs = new List<AudioDeviceListItem>();
            var devices = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            if (devices.Count == 0)
            {
                Logger.Info("Audio Input - No audio input devices available, disabling mic preview");
                return inputs;
            }

            Logger.Info("Audio Input - " + devices.Count + " audio input devices available, configuring as usual");

            inputs.Add(new AudioDeviceListItem()
            {
                Text = "Default Microphone",
                Value = null
            });
            SelectedAudioInput = inputs[0];

            foreach (var item in devices)
            {
                try
                {
                    var input = new AudioDeviceListItem()
                    {
                        Text = item.FriendlyName,
                        Value = item
                    };

                    Logger.Info("Audio Input - " + item.DeviceFriendlyName + " " + item.ID + " CHN:" +
                                item.AudioClient.MixFormat.Channels + " Rate:" +
                                item.AudioClient.MixFormat.SampleRate.ToString());

                    inputs.Add(input);

                    if (item.ID.Trim().Equals(GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue.Trim()))
                    {
                        SelectedAudioInput = input;
                        Logger.Info("Audio Input - Found Saved ");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Audio Input - " + item.DeviceFriendlyName);
                }
            }


            return inputs;
        }


        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (_deviceEnum != null)
            {
                _deviceEnum.UnregisterEndpointNotificationCallback(this);
                _deviceEnum.Dispose();
                _deviceEnum = null;
            }
        }

        #endregion

        #region IMMNotificationClient

        public void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.I4)] DeviceState newState)
        {
            Logger.Info("Device {deviceId} State Changed to {newState}, rebuilding device list", deviceId, newState);
            InputAudioDevices = BuildAudioInputs();
        }

        // The added and removed handlers don't seem to fire for me, but as the logic is the same I'm going to leave these handlers here.
        public void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId)
        {
            Logger.Info("Device {deviceId} Added, rebuilding device list", deviceId);
            InputAudioDevices = BuildAudioInputs();
        }

        public void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId)
        {
            Logger.Info("Device {deviceId} Removed, rebuilding device list.", deviceId);
            InputAudioDevices = BuildAudioInputs();
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId)
        {
        }

        public void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PropertyKey key)
        {
        }

        #endregion
    }
}
