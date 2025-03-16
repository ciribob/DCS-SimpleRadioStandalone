using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Recording;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using CommunityToolkit.Mvvm.DependencyInjection;
using NAudio.Utils;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers
{
    public class RadioMixingProvider : ISampleProvider
    {
        private readonly int radioId;
        private readonly List<ClientAudioProvider> sources;


        private ClientEffectsPipeline pipeline = new ClientEffectsPipeline();

        private ProfileSettingsModel ProfileSettings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>().CurrentProfile;

        private float[] mixBuffer;
        private float[] secondaryMixBuffer;
        private List<DeJitteredTransmission> _mainAudio = new List<DeJitteredTransmission>();
        private List<DeJitteredTransmission> _secondaryAudio = new List<DeJitteredTransmission>();

        private AudioRecordingManager _audioRecordingManager = AudioRecordingManager.Instance;

        private CircularFloatBuffer effectsBuffer;

        private readonly CachedAudioEffectProvider _cachedAudioEffectsProvider;

        RadioInformation.Modulation lastModulation = RadioInformation.Modulation.DISABLED;

        // put these in a struct? 
        private bool hasPlayedTransmissionEnd = true;
        private long lastReceivedAt = 0;
        private bool hasPlayedTransmissionStart = false;
        private float lastVolume = 1;

        //  private readonly WaveFileWriter waveWriter;
        public RadioMixingProvider(WaveFormat waveFormat, int radioId)
        {
            if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Mixer wave format must be IEEE float");
            }

            this.radioId = radioId;
            sources = new List<ClientAudioProvider>();
            WaveFormat = waveFormat;

            //5 seconds worth of buffer
            effectsBuffer = new CircularFloatBuffer(WaveFormat.SampleRate * 5);
            _cachedAudioEffectsProvider = CachedAudioEffectProvider.Instance;
            ;

            //   waveWriter = new NAudio.Wave.WaveFileWriter($@"C:\\temp\\output{Guid.NewGuid()}.wav", new WaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 2));
        }

        /// <summary>
        /// Returns the mixer inputs (read-only - use AddMixerInput to add an input
        /// </summary>
        public IEnumerable<ClientAudioProvider> MixerInputs => sources;

        /// <summary>
        /// Adds a new mixer input
        /// </summary>
        /// <param name="mixerInput">Mixer input</param>
        public void AddMixerInput(ClientAudioProvider mixerInput)
        {
            // we'll just call the lock around add since we are protecting against an AddMixerInput at
            // the same time as a Read, rather than two AddMixerInput calls at the same time
            lock (sources)
            {
                sources.Add(mixerInput);
            }
        }

        public void RemoveMixerInput(ClientAudioProvider mixerInput)
        {
            lock (sources)
            {
                sources.Remove(mixerInput);
            }
        }

        /// <summary>
        /// Removes all mixer inputs
        /// </summary>
        public void RemoveAllMixerInputs()
        {
            lock (sources)
            {
                sources.Clear();
            }
        }

        /// <summary>
        /// The output WaveFormat of this sample provider
        /// </summary>
        public WaveFormat WaveFormat { get; private set; }

        public float[] ClearArray(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 0;
            }

            return buffer;
        }

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="count">Number of samples required</param>
        /// <returns>Number of samples read</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            _mainAudio.Clear();
            _secondaryAudio.Clear();
            int primarySamples = 0;
            int secondarySamples = 0;

            mixBuffer = BufferHelpers.Ensure(mixBuffer, count);
            secondaryMixBuffer = BufferHelpers.Ensure(secondaryMixBuffer, count);

            ClearArray(mixBuffer);
            ClearArray(secondaryMixBuffer);

            bool ky58Tone = false;

            lock (sources)
            {
                int index = sources.Count - 1;
                while (index >= 0)
                {
                    var source = sources[index];

                    //ask for count/2 as the source is MONO but the request for this is STEREO
                    var transmission = source.JitterBufferProviderInterface[radioId].Read(count / 2);

                    if (transmission.PCMAudioLength > 0)
                    {
                        if (transmission.IsSecondary)
                        {
                            _secondaryAudio.Add(transmission);
                        }
                        else
                        {
                            _mainAudio.Add(transmission);
                        }

                        if (transmission.Decryptable && transmission.Encryption > 0)
                        {
                            ky58Tone = true;
                        }

                        lastModulation = transmission.Modulation;
                        lastVolume = transmission.Volume;
                    }

                    index--;
                }
            }

            //copy to the recording service - as we have everything we need to know about the audio
            //at this point
            if (_mainAudio.Count > 0 || _secondaryAudio.Count > 0)
            {
                lastReceivedAt = DateTime.Now.Ticks;
                hasPlayedTransmissionEnd = false;
                _audioRecordingManager.AppendClientAudio(_mainAudio, _secondaryAudio, radioId);
            }

            if (_mainAudio.Count > 0)
            {
                mixBuffer = pipeline.ProcessClientTransmissions(mixBuffer, _mainAudio, out primarySamples);
            }

            //handle guard
            if (_secondaryAudio.Count > 0)
            {
                secondaryMixBuffer =
                    pipeline.ProcessClientTransmissions(secondaryMixBuffer, _secondaryAudio, out secondarySamples);
            }

            //reuse mix buffer
            mixBuffer = AudioManipulationHelper.MixArraysNoClipping(mixBuffer, primarySamples, secondaryMixBuffer,
                secondarySamples, out int outputSamples);

            //Now mix in start and end tones, Beeps etc
            mixBuffer = HandleStartEndTones(mixBuffer, count / 2, _mainAudio.Count > 0 || _secondaryAudio.Count > 0,
                lastModulation, ky58Tone, out int effectOutputSamples); //divide by 2 as we're not yet in stereo

            //now clip post all mixing
            mixBuffer = AudioManipulationHelper.ClipArray(mixBuffer, count/2);

            //figure out number of samples to return
            outputSamples = Math.Max(outputSamples, effectOutputSamples);

            buffer = SeparateAudio(mixBuffer, outputSamples, 0, buffer, offset, radioId);

            //we're now stereo - double the samples
            outputSamples = outputSamples * 2;

            return EnsureFullBuffer(buffer, outputSamples, offset, count);
        }

        private float[] HandleStartEndTones(float[] mixBuffer, int count, bool transmisson,
          RadioInformation.Modulation modulation, bool encryption, out int outputSamples)
        {
            //enqueue
            if (transmisson && !hasPlayedTransmissionStart)
            {
                hasPlayedTransmissionStart = true;
                hasPlayedTransmissionEnd = false;

                PlaySoundEffectStartReceive(encryption, modulation);
            }
            else if (!transmisson && !hasPlayedTransmissionEnd && IsEndOfTransmission)
            {
                hasPlayedTransmissionStart = false;
                hasPlayedTransmissionEnd = true;

                //TODO not sure about simultaneous
                //We used to have this logic https://github.com/ciribob/DCS-SimpleRadioStandalone/blob/cd8fcbf7e2b2fafcf30875fc958276e3083e0ebb/DCS-SR-Client/Network/UDPVoiceHandler.cs#L135
                //if (!radioReceivingState.IsSimultaneous)
                PlaySoundEffectEndReceive(modulation);
            }

            //read
            if (effectsBuffer.Count > 0)
            {
                float[] tempBuffer = new float[count];

                effectsBuffer.Read(tempBuffer, 0, count);

                for (int i = 0; i < count; i++)
                {
                    mixBuffer[i] += (tempBuffer[i] * lastVolume);
                    /// should we clip here?
                }

                outputSamples = count;
            }
            else
            {
                outputSamples = 0;
            }

            return mixBuffer;
        }

        private void PlaySoundEffectEndReceive(RadioInformation.Modulation modulation)
        {
            if (!ProfileSettings.RadioRxEffects_End)
            {
                return;
            }

            bool midsTone = ProfileSettings.MidsRadioEffect;

            if (radioId == 0)
            {
                var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionEndEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else if (modulation == RadioInformation.Modulation.MIDS && midsTone)
            {
                //end receive tone for MIDS
                var effect = _cachedAudioEffectsProvider.MIDSEndTone;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else
            {
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionEndEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
        }

        public void PlaySoundEffectStartReceive(bool encrypted, RadioInformation.Modulation modulation)
        {
            if (!ProfileSettings.RadioRxEffects_Start)
            {
                return;
            }

            bool midsTone = ProfileSettings.MidsRadioEffect;

            if (modulation == RadioInformation.Modulation.MIDS && midsTone)
            {
                //no tone for MIDS
                return;
            }


            if (radioId == 0)
            {
                var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionStartEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else if (encrypted && ProfileSettings.RadioEncryptionEffects)
            {
                var effect = _cachedAudioEffectsProvider.KY58EncryptionEndTone;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else
            {
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionStartEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
        }

        public void PlaySoundEffectStartTransmit(bool encrypted, float volume, RadioInformation.Modulation modulation)
        {
            lastModulation = modulation;
            lastVolume = volume;

            if (!ProfileSettings.RadioTxEffectsStart)
            {
                return;
            }

            bool midsTone = ProfileSettings.MidsRadioEffect;

            if (radioId == 0)
            {
                var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionStartEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else if (encrypted && ProfileSettings.RadioEncryptionEffects)
            {
                var effect = _cachedAudioEffectsProvider.KY58EncryptionTransmitTone;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else if (modulation == RadioInformation.Modulation.MIDS && midsTone)
            {
                var effect = _cachedAudioEffectsProvider.MIDSTransmitTone;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else
            {
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionStartEffect;

                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
        }

        public void PlaySoundEffectEndTransmit(float volume, RadioInformation.Modulation modulation)
        {
            lastModulation = modulation;
            lastVolume = volume;

            if (!ProfileSettings.RadioTxEffectsEnd)
            {
                return;
            }

            bool midsTone = ProfileSettings.MidsRadioEffect;

            if (radioId == 0)
            {
                var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionEndEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else if (modulation == RadioInformation.Modulation.MIDS && midsTone)
            {
                var effect = _cachedAudioEffectsProvider.MIDSEndTone;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
            else
            {
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionEndEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
                }
            }
        }



        public float[] SeparateAudio(float[] srcFloat, int srcCount, int srcOffset, float[] dstFloat, int dstOffset,
            int radioId)
        {
            var settingType = ProfileSettingsKeys.Radio1Channel;

            if (radioId == 0)
            {
                settingType = ProfileSettingsKeys.IntercomChannel;
            }
            else if (radioId == 1)
            {
                settingType = ProfileSettingsKeys.Radio1Channel;
            }
            else if (radioId == 2)
            {
                settingType = ProfileSettingsKeys.Radio2Channel;
            }
            else if (radioId == 3)
            {
                settingType = ProfileSettingsKeys.Radio3Channel;
            }
            else if (radioId == 4)
            {
                settingType = ProfileSettingsKeys.Radio4Channel;
            }
            else if (radioId == 5)
            {
                settingType = ProfileSettingsKeys.Radio5Channel;
            }
            else if (radioId == 6)
            {
                settingType = ProfileSettingsKeys.Radio6Channel;
            }
            else if (radioId == 7)
            {
                settingType = ProfileSettingsKeys.Radio7Channel;
            }
            else if (radioId == 8)
            {
                settingType = ProfileSettingsKeys.Radio8Channel;
            }
            else if (radioId == 9)
            {
                settingType = ProfileSettingsKeys.Radio9Channel;
            }
            else if (radioId == 10)
            {
                settingType = ProfileSettingsKeys.Radio10Channel;
            }
            else
            {
                return CreateBalancedMix(srcFloat, srcCount, srcOffset, dstFloat, dstOffset, 0);
            }

            float balance = 0;
            try
            {
                balance = profileSettings.GetClientSettingFloat(settingType);
            }
            catch (Exception)
            {
                //ignore
            }

            return CreateBalancedMix(srcFloat, srcCount, srcOffset, dstFloat, dstOffset, balance);
        }

        public static float[] CreateBalancedMix(float[] srcFloat, int srcCount, int srcOffset, float[] dstFloat,
            int dstOffset, float balance)
        {
            float left = (1.0f - balance) / 2.0f;
            float right = 1.0f - left;

            //temp set of mono floats
            int monoBufferPosition = 0;
            for (int i = 0; i < srcCount * 2; i += 2)
            {
                dstFloat[i + dstOffset] = srcFloat[monoBufferPosition + srcOffset] * left;
                dstFloat[i + dstOffset + 1] = srcFloat[monoBufferPosition + srcOffset] * right;
                monoBufferPosition++;
            }

            return dstFloat;
        }

        private int EnsureFullBuffer(float[] buffer, int samplesCount, int offset, int count)
        {
            // ensure we return a full buffer of STEREO
            if (samplesCount < count)
            {
                int outputIndex = offset + samplesCount;
                while (outputIndex < offset + count)
                {
                    buffer[outputIndex++] = 0;
                }

                samplesCount = count;
            }

            //Should be impossible - ensures audio doesnt crash if its not
            if (samplesCount > count)
            {
                samplesCount = count;
            }

            return samplesCount;
        }

        public bool IsEndOfTransmission
        {
            get { return (DateTime.Now.Ticks - lastReceivedAt) < 3500000; }
        }
    }
}