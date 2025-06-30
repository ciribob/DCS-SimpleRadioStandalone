using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.SoundFont;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class RadioMixingProvider : ISampleProvider
{
    private readonly AudioRecordingManager _audioRecordingManager = AudioRecordingManager.Instance;

    private readonly CachedAudioEffectProvider _cachedAudioEffectsProvider;
    private readonly CircularFloatBuffer effectsBuffer;


    private readonly ClientEffectsPipeline pipeline = new();

    private readonly ProfileSettingsStore profileSettings =
        GlobalSettingsStore.Instance.ProfileSettingsStore;

    private readonly int radioId;
    private readonly List<ClientAudioProvider> sources;

    private Modulation lastModulation = Modulation.DISABLED;
    private long lastReceivedAt;
    private bool IsReceiving { get; set; } = false;
    private float lastVolume = 1;

    private float[] mixBuffer;
    private int availableInBuffer = 0;

    //  private readonly WaveFileWriter waveWriter;
    public RadioMixingProvider(WaveFormat waveFormat, int radioId)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");

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
    ///     Returns the mixer inputs (read-only - use AddMixerInput to add an input
    /// </summary>
    public IEnumerable<ClientAudioProvider> MixerInputs => sources;

    public bool IsEndOfTransmission => TimeSpan.FromTicks(DateTime.Now.Ticks - lastReceivedAt).TotalMilliseconds > 350;

    /// <summary>
    ///     The output WaveFormat of this sample provider
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    ///     Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="count">Number of samples required</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        // Accumulate in local mono buffer before switching to stereo.
        var monoBuffer = new float[count / 2];
        
        // Read effects.
        var monoOffset = ReadEffects(monoBuffer, 0, monoBuffer.Length);

        // Read any available audio that we have queued up.
        monoOffset += ReadMixBuffer(monoBuffer, monoOffset, monoBuffer.Length - monoOffset);

        // Are we starved? Rehydrate.
        if (monoOffset < monoBuffer.Length)
        {
            List<DeJitteredTransmission> mainAudio = new();
            List<DeJitteredTransmission> secondaryAudio = new();

            // Update sources by queueing incoming audio.
            var ky58Tone = false;
            var longestMainLength = 0;
            var longestSecondaryLength = 0;
            lock (sources)
            {
                var index = sources.Count - 1;
                while (index >= 0)
                {
                    var source = sources[index];

                    //ask for count/2 as the source is MONO but the request for this is STEREO
                    var transmission = source.JitterBufferProviderInterface[radioId].Read(monoBuffer.Length - monoOffset);

                    if (transmission.PCMAudioLength > 0)
                    {
                        if (!transmission.IsSecondary)
                        {
                            mainAudio.Add(transmission);
                            longestMainLength = Math.Max(longestMainLength, transmission.PCMAudioLength);

                        }
                        else
                        {
                            secondaryAudio.Add(transmission);
                            longestSecondaryLength = Math.Max(longestSecondaryLength, transmission.PCMAudioLength);
                        }


                        if (transmission.Decryptable && transmission.Encryption > 0) ky58Tone = true;

                        lastModulation = transmission.Modulation;
                        lastVolume = transmission.Volume;


                    }

                    index--;
                }
            }

            var hasIncomingAudio = mainAudio.Count > 0 || secondaryAudio.Count > 0;
            //copy to the recording service - as we have everything we need to know about the audio
            //at this point
            if (hasIncomingAudio)
            {
                lastReceivedAt = DateTime.Now.Ticks;
                _audioRecordingManager.AppendClientAudio(mainAudio, secondaryAudio, radioId);
            }
            else if (!IsEndOfTransmission)
            {
                // #TODO: Generate a dejiterred transmission silence, to go through the radio pipeline?
            }

            // #FIXME: Should copy into mixBuffer, and use that throughout as our primary mixdown here.
            monoOffset += HandleStartEndTones(hasIncomingAudio || availableInBuffer > 0, ky58Tone, monoBuffer, monoOffset, monoBuffer.Length - monoOffset);

            // Queue new audio (if any).
            if (hasIncomingAudio)
            {
                // Need to be able to hold whatever is left over + incoming audio.
                var longestTransmissionLength = Math.Max(longestMainLength, longestSecondaryLength);
                var totalSize = availableInBuffer + longestTransmissionLength;
                if (mixBuffer == null || mixBuffer.Length < totalSize)
                {
                    Array.Resize(ref mixBuffer, totalSize);
                }

                var targetSpan = mixBuffer.AsSpan(availableInBuffer, longestTransmissionLength);
                targetSpan.Clear();
                var primarySamples = 0;
                if (mainAudio.Count > 0)
                {
                    pipeline.ProcessClientTransmissions(mixBuffer, availableInBuffer, mainAudio, out primarySamples);
                }

                //handle guard
                if (secondaryAudio.Count > 0)
                {
                    var secondarySamples = 0;
                    var secondaryMixBuffer = new float[longestSecondaryLength];
                    pipeline.ProcessClientTransmissions(secondaryMixBuffer, 0, secondaryAudio, out secondarySamples);

                    // Mix with primary.
                    AudioManipulationHelper.MixArraysNoClipping(secondaryMixBuffer.AsSpan(0, secondarySamples), targetSpan);
                }

                //now clip all mixing
                AudioManipulationHelper.ClipArray(targetSpan);
                availableInBuffer += targetSpan.Length;

                // Drain newly queued audio.
                monoOffset += ReadMixBuffer(monoBuffer, monoOffset, monoBuffer.Length - monoOffset);
            }
        }

        // Did we consume everything? Make sure we don't keep a buffer too big.
        if (mixBuffer != null && availableInBuffer == 0)
        {
            if (mixBuffer.Length > Constants.OUTPUT_SEGMENT_FRAMES)
            {
                Array.Resize(ref mixBuffer, Constants.OUTPUT_SEGMENT_FRAMES);
            }
        }


        if (monoOffset > 0)
        {
             // We have available data, make it stereo and copy to target.
            SeparateAudio(monoBuffer, 0, monoOffset, buffer, offset, radioId);
        }

        return monoOffset * 2; // double because of mono -> stereo.
    }

    /// <summary>
    ///     Adds a new mixer input
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
    ///     Removes all mixer inputs
    /// </summary>
    public void RemoveAllMixerInputs()
    {
        lock (sources)
        {
            sources.Clear();
        }
    }

    private int HandleStartEndTones(bool isActive, bool encryption, float[] buffer, int offset, int count)
    {
        if (isActive ^ IsReceiving)
        {
            // State change.
            if (isActive)
            {
                // Start
                effectsBuffer.Reset(); // In case we were playing the end tone, cut it short.
                PlaySoundEffectStartReceive(encryption, lastModulation);
                IsReceiving = true;
            }
            else if (IsEndOfTransmission)
            {
                IsReceiving = false;
                // end.
                //TODO not sure about simultaneous
                //We used to have this logic https://github.com/ciribob/DCS-SimpleRadioStandalone/blob/cd8fcbf7e2b2fafcf30875fc958276e3083e0ebb/DCS-SR-Client/Network/UDPVoiceHandler.cs#L135
                //if (!radioReceivingState.IsSimultaneous)
                PlaySoundEffectEndReceive(lastModulation);
            }
        }

        return ReadEffects(buffer, offset, count);
    }

    private int ReadEffects(float[] buffer, int offset, int count)
    {
        //read
        var outputSamples = Math.Min(effectsBuffer.Count, count);
        if (outputSamples > 0)
        {
            effectsBuffer.Read(buffer, offset, outputSamples);
        }

        return outputSamples;
    }
    private int ReadMixBuffer(float[] buffer, int offset, int count)
    {
        // Drain current.
        var samplesRead = Math.Min(count, availableInBuffer);
        if (samplesRead > 0)
        {
            Array.Copy(mixBuffer, 0, buffer, offset, samplesRead);
            availableInBuffer -= samplesRead;
            // Shift elements so that the data available is always at the beginning.
            Array.Copy(mixBuffer, samplesRead, mixBuffer, 0, availableInBuffer);
        }

        return samplesRead;
    }

    private void PlaySoundEffectEndReceive(Modulation modulation)
    {
        if (!profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End)) return;

        var midsTone = profileSettings.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

        if (radioId == 0)
        {
            var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionEndEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else if (modulation == Modulation.MIDS && midsTone)
        {
            //end receive tone for MIDS
            var effect = _cachedAudioEffectsProvider.MIDSEndTone;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else
        {
            var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionEndEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
    }

    public void PlaySoundEffectStartReceive(bool encrypted, Modulation modulation)
    {
        if (!profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start)) return;

        var midsTone = profileSettings.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

        if (modulation == Modulation.MIDS && midsTone)
            //no tone for MIDS
            return;


        if (radioId == 0)
        {
            var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionStartEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else if (encrypted &&
                 profileSettings.GetClientSettingBool(ProfileSettingsKeys
                     .RadioEncryptionEffects))
        {
            var effect = _cachedAudioEffectsProvider.KY58EncryptionEndTone;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else
        {
            var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionStartEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
    }

    public void PlaySoundEffectStartTransmit(bool encrypted, float volume, Modulation modulation)
    {
        lastModulation = modulation;
        lastVolume = volume;

        if (!profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start)) return;

        var midsTone = profileSettings.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

        if (radioId == 0)
        {
            var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionStartEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else if (encrypted && profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects))
        {
            var effect = _cachedAudioEffectsProvider.KY58EncryptionTransmitTone;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else if (modulation == Modulation.MIDS && midsTone)
        {
            var effect = _cachedAudioEffectsProvider.MIDSTransmitTone;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else
        {
            var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionStartEffect;

            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
    }

    public void PlaySoundEffectEndTransmit(float volume, Modulation modulation)
    {
        lastModulation = modulation;
        lastVolume = volume;

        if (!profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_End)) return;

        var midsTone = profileSettings.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

        if (radioId == 0)
        {
            var effect = _cachedAudioEffectsProvider.SelectedIntercomTransmissionEndEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else if (modulation == Modulation.MIDS && midsTone)
        {
            var effect = _cachedAudioEffectsProvider.MIDSEndTone;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
        else
        {
            var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionEndEffect;
            if (effect.Loaded) effectsBuffer.Write(effect.AudioEffectFloat, 0, effect.AudioEffectFloat.Length);
        }
    }


    public void SeparateAudio(float[] srcFloat, int srcOffset, int srcCount, float[] dstFloat, int dstOffset,
        int radioId)
    {
        ProfileSettingsKeys? settingType = null;
        switch (radioId)
        {
            case 0:
                settingType = ProfileSettingsKeys.IntercomChannel;
                break;
            case 1:
                settingType = ProfileSettingsKeys.Radio1Channel;
                break;
            case 2:
                settingType = ProfileSettingsKeys.Radio2Channel;
                break;
            case 3:
                settingType = ProfileSettingsKeys.Radio3Channel;
                break;
            case 4:
                settingType = ProfileSettingsKeys.Radio4Channel;
                break;
            case 5:
                settingType = ProfileSettingsKeys.Radio5Channel;
                break;
            case 6:
                settingType = ProfileSettingsKeys.Radio6Channel;
                break;
            case 7:
                settingType = ProfileSettingsKeys.Radio7Channel;
                break;
            case 8:
                settingType = ProfileSettingsKeys.Radio8Channel;
                break;
            case 9:
                settingType = ProfileSettingsKeys.Radio9Channel;
                break;
            case 10:
                settingType = ProfileSettingsKeys.Radio10Channel;
                break;
        };

        float balance = 0f;
        if (settingType.HasValue)
        {
            try
            {
                balance = profileSettings.GetClientSettingFloat(settingType.Value);
            }
            catch (Exception)
            {
                //ignore
            }
        }

        CreateBalancedMix(srcFloat, srcOffset, srcCount, dstFloat, dstOffset, balance);
    }

    public static void CreateBalancedMix(float[] srcFloat, int srcOffset, int srcCount, float[] dstFloat,
        int dstOffset, float balance)
    {
        var left = (1.0f - balance) / 2.0f;
        var right = 1.0f - left;

        //temp set of mono floats
        for (var i = 0; i < srcCount; ++i)
        {
            dstFloat[dstOffset + 2 * i] = srcFloat[srcOffset + i] * left;
            dstFloat[dstOffset + 2 * i + 1] = srcFloat[srcOffset + i] * right;
        }
    }

    private int EnsureFullBuffer(float[] buffer, int samplesCount, int offset, int count)
    {
        // ensure we return a full buffer of STEREO
        if (samplesCount < count)
        {
            var outputIndex = offset + samplesCount;
            while (outputIndex < offset + count) buffer[outputIndex++] = 0;

            samplesCount = count;
        }

        //Should be impossible - ensures audio doesnt crash if its not
        if (samplesCount > count) samplesCount = count;

        return samplesCount;
    }
}