using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow;
using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public class AudioOutputSingleton
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Singleton Definition
        private static volatile AudioOutputSingleton _instance;
        private static object _lock = new Object();

        public static AudioOutputSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new AudioOutputSingleton();
                    }
                }

                return _instance;
            }
        }
        #endregion

        #region Instance Definition

        public List<AudioDeviceListItem> OutputAudioDevices { get; }
        public AudioDeviceListItem SelectedAudioOutput { get; set; }

        public List<AudioDeviceListItem> MicOutputAudioDevices { get; }
        public AudioDeviceListItem SelectedMicAudioOutput { get; set; }


        // Version of Windows without bundled multimedia stuff as part of European anti-trust settlement
        // https://support.microsoft.com/en-us/help/11529/what-is-a-windows-7-n-edition
        public bool WindowsN { get; set; }

        private AudioOutputSingleton()
        {
            WindowsN = DetectWindowsN();
            OutputAudioDevices = BuildNormalAudioOutputs();
            MicOutputAudioDevices = BuildMicAudioOutputs();
        }

        private List<AudioDeviceListItem> BuildNormalAudioOutputs()
        {
            Logger.Info("Building Normal Audio Outputs");
            Logger.Info("Audio Output - Saved ID " +
                         GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId).RawValue);

            return BuildAudioOutputs(Properties.Resources.DefaultSpeakers, false);
        }

        private List<AudioDeviceListItem> BuildMicAudioOutputs()
        {
            Logger.Info("Building Microphone Audio Outputs");
            Logger.Info("Mic Audio Output - Saved ID " +
                                   GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId).RawValue);

            return BuildAudioOutputs(Properties.Resources.DefaultNoPassthru, true);
        }

        private List<AudioDeviceListItem> BuildAudioOutputs(string defaultItemText, bool micOutput)
        {
            var outputs = new List<AudioDeviceListItem>
            {
                new AudioDeviceListItem()
                {
                    Text = defaultItemText,
                    Value = null
                }
            };

            string savedDeviceId;
            if (micOutput)
            {
                savedDeviceId = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId).RawValue;
                SelectedMicAudioOutput = outputs[0];
            } else
            {
                savedDeviceId = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId).RawValue;
                SelectedAudioOutput = outputs[0];
            }

            var enumerator = new MMDeviceEnumerator();
            var outputDeviceList = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var i = 1;
            foreach (var device in outputDeviceList)
            {
                try
                {
                    Logger.Info("Audio Output - " + device.DeviceFriendlyName + " " + device.ID + " CHN:" +
                            device.AudioClient.MixFormat.Channels + " Rate:" +
                            device.AudioClient.MixFormat.SampleRate.ToString());

                    outputs.Add(new AudioDeviceListItem()
                    {
                        Text = device.FriendlyName,
                        Value = device
                    });

                    if (device.ID == savedDeviceId)
                    {
                        if (micOutput)
                        {
                            SelectedMicAudioOutput = outputs[i];
                        }
                        else
                        {
                            SelectedAudioOutput = outputs[i];
                        }
                    }

                    i++;

                }
                catch (Exception e)
                {
                    Logger.Error(e, "Audio Output - Error processing device - device skipped");
                }
            }

            return outputs;
        }

        private bool DetectWindowsN()
        {
            try
            {
                var dmoResampler = new DmoResampler();
                dmoResampler.Dispose();
                return false;
            }
            catch (Exception)
            {
                Logger.Warn("Windows N Detected - using inbuilt resampler");
                return true;
            }
        }
        #endregion
    }
}
