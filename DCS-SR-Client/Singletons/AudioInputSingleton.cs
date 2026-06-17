using System;
using System.Collections.Generic;
using System.Linq;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Properties;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.CoreAudioApi;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;

public class AudioInputSingleton
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    #region Singleton Definition

    private static volatile AudioInputSingleton _instance;
    private static readonly object _lock = new();

    public static AudioInputSingleton Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new AudioInputSingleton();
                }

            return _instance;
        }
    }

    #endregion

    #region Instance Definition

    private GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    public List<AudioDeviceListItem> InputAudioDevices { get; }

    public AudioDeviceListItem SelectedAudioInput
    {
        get;
        set
        {
            if (!MicrophoneAvailable)
            {
                field = value;
                return;
            }

            if (value == null || value.Value == null)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, "default");
            }
            else
            {
                var input = ((MMDevice)value.Value).ID;
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, input);
            }

            field = value;
        }
    }

    // Indicates whether a valid microphone is available - deactivating audio input controls and transmissions otherwise
    public bool MicrophoneAvailable { get; private set; }

    private AudioInputSingleton()
    {
        InputAudioDevices = BuildAudioInputs();
    }

    private List<AudioDeviceListItem> BuildAudioInputs()
    {
        Logger.Info("Audio Input - Saved ID " +
                    GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue);

        var inputs = new List<AudioDeviceListItem>();

        var deviceEnum = new MMDeviceEnumerator();
        var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

        if (devices.Count == 0)
        {
            MicrophoneAvailable = false;
            Logger.Info("Audio Input - No audio input devices available, disabling mic preview");
            return inputs;
        }

        Logger.Info("Audio Input - " + devices.Count + " audio input devices available, configuring as usual");

        inputs.Add(new AudioDeviceListItem
        {
            Text = Resources.DefaultMicrophone,
            Value = null
        });

        foreach (var item in devices)
            try
            {
                var input = new AudioDeviceListItem
                {
                    Text = item.FriendlyName,
                    Value = item
                };

                Logger.Info("Audio Input - " + item.DeviceFriendlyName + " " + item.ID + " CHN:" +
                            item.AudioClient.MixFormat.Channels + " Rate:" +
                            item.AudioClient.MixFormat.SampleRate);

                inputs.Add(input);

                if (item.ID.Trim().Equals(GlobalSettingsStore.Instance
                        .GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue.Trim()))
                {
                    SelectedAudioInput = input;
                    Logger.Info("Audio Input - Found Saved ");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Audio Input - " + item.DeviceFriendlyName);
            }

        MicrophoneAvailable = true;

        if (SelectedAudioInput == null)
        {
            SelectedAudioInput = inputs[0];
        }

        return inputs;
    }

    #endregion
}