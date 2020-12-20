using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;
using NAudio.CoreAudioApi;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public class AudioInputSingleton : AudioDevicesBase
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

        private AudioDeviceListItem _selectedAudioInput;
        private List<AudioDeviceListItem> _inputAudioDevices;

        
        public List<AudioDeviceListItem> InputAudioDevices
        {
            get => _inputAudioDevices; private set
            {
                _inputAudioDevices = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MicrophoneAvailable));
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

        private AudioInputSingleton() : base(Logger)
        {
            InputAudioDevices = BuildAudioInputs();
        }

        protected override void OnDeviceEnumChanged(string deviceId)
        {
            //react naiively to any event to start, we can work on logic to reduce unnecessary churn later
            DisposeListMembers(InputAudioDevices);
            InputAudioDevices = BuildAudioInputs();
        }

        private List<AudioDeviceListItem> BuildAudioInputs()
        {
            Logger.Info("Audio Input - Saved ID " +
                        GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue);

            var inputs = new List<AudioDeviceListItem>();
            var devices = DeviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

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

        #endregion

       
    }
}
