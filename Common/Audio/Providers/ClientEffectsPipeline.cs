using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using MathNet.Filtering;
using NAudio.Dsp;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class ClientEffectsPipeline
{
    private static readonly double HQ_RESET_CHANCE = 0.8;

    private readonly OnlineFilter[] _filters;

    private readonly BiQuadFilter _highPassFilter;
    private readonly BiQuadFilter _lowPassFilter;
    private readonly Random _random = new();

    private readonly CachedAudioEffect amCollisionEffect;

    private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;

    private readonly ProfileSettingsStore profileSettings;

    private readonly SyncedServerSettings serverSettings;
    private float amCollisionVol = 1.0f;
    private int amEffectPosition;
    private bool clippingEnabled;
    private int fmNoisePosition;

    private float fmVol;
    private int hfNoisePosition;
    private float hfVol;
    private bool hqToneEnabled;

    private int hqTonePosition;
    private float hqToneVolume;

    private bool irlRadioRXInterference;

    private long lastRefresh; //last refresh of settings
    private int natoPosition;

    private bool natoToneEnabled;
    private float natoToneVolume;
    private bool radioBackgroundNoiseEffect;

    private bool radioEffects;
    private bool radioEffectsEnabled;
    private int uhfNoisePosition;
    private float uhfVol;
    private int vhfNoisePosition;
    private float vhfVol;

    public ClientEffectsPipeline()
    {
        profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
        serverSettings = SyncedServerSettings.Instance;

        _filters = new OnlineFilter[2];
        _filters[0] =
            OnlineFilter.CreateBandpass(ImpulseResponse.Finite, Constants.OUTPUT_SAMPLE_RATE, 560, 3900);
        _filters[1] =
            OnlineFilter.CreateBandpass(ImpulseResponse.Finite, Constants.OUTPUT_SAMPLE_RATE, 100, 4500);

        _highPassFilter = BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, 520, 0.97f);
        _lowPassFilter = BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, 4130, 2.0f);
        RefreshSettings();

        amCollisionEffect = effectProvider.AMCollision;
    }

    private void RefreshSettings()
    {
        //only get settings every 3 seconds - and cache them - issues with performance
        var now = DateTime.Now.Ticks;

        if (TimeSpan.FromTicks(now - lastRefresh).TotalSeconds > 3) //3 seconds since last refresh
        {
            lastRefresh = now;

            natoToneEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
            hqToneEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.HAVEQUICKTone);
            radioEffectsEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
            clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);

            hqToneVolume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HQToneVolume);
            natoToneVolume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume);

            amCollisionVol = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.AMCollisionVolume);

            fmVol = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume);
            hfVol = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume);
            uhfVol = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume);
            vhfVol = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume);

            radioEffects = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);

            radioBackgroundNoiseEffect =
                profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect);

            irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
        }
    }

    public void ProcessClientTransmissions(float[] tempBuffer, int offset, List<DeJitteredTransmission> transmissions,
        out int clientTransmissionLength)
    {
        RefreshSettings();
        var lastTransmission = transmissions[0];

        clientTransmissionLength = 0;
        foreach (var transmission in transmissions)
        {
            for (var i = 0; i < transmission.PCMAudioLength; i++) tempBuffer[offset + i] += transmission.PCMMonoAudio[i];

            clientTransmissionLength = Math.Max(clientTransmissionLength, transmission.PCMAudioLength);
        }

        var process = true;

        // take info account server setting AND volume of this radio AND if its AM or FM
        // FOR HAVEQUICK - only if its MORE THAN TWO
        if (lastTransmission.ReceivedRadio != 0
            && !lastTransmission.NoAudioEffects
            && (lastTransmission.Modulation == Modulation.AM
                || lastTransmission.Modulation == Modulation.FM
                || lastTransmission.Modulation == Modulation.SINCGARS
                || lastTransmission.Modulation == Modulation.HAVEQUICK)
            && irlRadioRXInterference)
            if (transmissions.Count > 1)
            {
                //All AM is wrecked if more than one transmission
                //For HQ - only if more than TWO transmissions and its totally fucked
                if (lastTransmission.Modulation == Modulation.HAVEQUICK && transmissions.Count > 2 &&
                    amCollisionEffect.Loaded)
                {
                    //replace the buffer with our own
                    var outIndex = 0;
                    while (outIndex < clientTransmissionLength)
                    {
                        var amByte = amCollisionEffect.AudioEffectFloat[amEffectPosition++];

                        tempBuffer[offset + outIndex++] = amByte * amCollisionVol * lastTransmission.Volume;

                        if (amEffectPosition == amCollisionEffect.AudioEffectFloat.Length) amEffectPosition = 0;
                    }

                    process = false;
                }
                else if (lastTransmission.Modulation == Modulation.AM && amCollisionEffect.Loaded)
                {
                    //AM https://www.youtube.com/watch?v=yHRDjhkrDbo
                    //Heterodyne tone AND audio from multiple transmitters in a horrible mess
                    //TODO improve this
                    //process here first
                    ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, offset, lastTransmission);
                    process = false;

                    //apply heterodyne tone to the mixdown
                    //replace the buffer with our own
                    var outIndex = 0;
                    while (outIndex < clientTransmissionLength)
                    {
                        var amByte = amCollisionEffect.AudioEffectFloat[amEffectPosition++];

                        tempBuffer[offset + outIndex++] += amByte * amCollisionVol * lastTransmission.Volume;

                        if (amEffectPosition == amCollisionEffect.AudioEffectFloat.Length) amEffectPosition = 0;
                    }
                }
                else if (lastTransmission.Modulation == Modulation.FM ||
                         lastTransmission.Modulation == Modulation.SINCGARS)
                {
                    //FM picketing / picket fencing - pick one transmission at random
                    //TODO improve this to pick the stronger frequency?
                    var index = _random.Next(transmissions.Count);
                    var transmission = transmissions[index];

                    for (var i = 0; i < transmission.PCMAudioLength; i++) tempBuffer[offset + i] = transmission.PCMMonoAudio[i];

                    clientTransmissionLength = transmission.PCMMonoAudio.Length;
                }
            }

        //only process if AM effect doesnt apply
        if (process)
            ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, offset, lastTransmission);
    }

    public void ProcessClientAudioSamples(float[] buffer, int count, int offset, DeJitteredTransmission transmission)
    {
        if (!transmission.NoAudioEffects)
        {
            if (transmission.Modulation == Modulation.MIDS
                || transmission.Modulation == Modulation.SATCOM
                || transmission.Modulation == Modulation.INTERCOM)
            {
                if (radioEffects) AddRadioEffectIntercom(buffer, count, offset, transmission.Modulation);
            }
            else
            {
                AddRadioEffect(buffer, count, offset, transmission.Modulation, transmission.Frequency);
            }
        }

        //final adjust
        AdjustVolume(buffer, count, offset, transmission.Volume);
    }

    private void AdjustVolume(float[] buffer, int count, int offset, float volume)
    {
        var outputIndex = offset;
        while (outputIndex < offset + count)
        {
            buffer[outputIndex] *= volume;

            outputIndex++;
        }
    }

    private void AddRadioEffectIntercom(float[] buffer, int count, int offset, Modulation modulation)
    {
        var outputIndex = offset;
        while (outputIndex < offset + count)
        {
            var audio = _highPassFilter.Transform(buffer[outputIndex]);

            audio = _highPassFilter.Transform(audio);

            if (float.IsNaN(audio))
                audio = _lowPassFilter.Transform(buffer[outputIndex]);
            else
                audio = _lowPassFilter.Transform(audio);

            if (!float.IsNaN(audio))
            {
                // clip
                if (audio > 1.0f)
                    audio = 1.0f;
                if (audio < -1.0f)
                    audio = -1.0f;

                buffer[outputIndex] = audio;
            }

            outputIndex++;
        }
    }


    private void AddRadioEffect(float[] buffer, int count, int offset, Modulation modulation, double freq)
    {
        var outputIndex = offset;

        while (outputIndex < offset + count)
        {
            var audio = (double)buffer[outputIndex];

            if (radioEffectsEnabled)
            {
                if (clippingEnabled)
                {
                    if (audio > RadioFilter.CLIPPING_MAX)
                        audio = RadioFilter.CLIPPING_MAX;
                    else if (audio < RadioFilter.CLIPPING_MIN) audio = RadioFilter.CLIPPING_MIN;
                }

                //high and low pass filter
                for (var j = 0; j < _filters.Length; j++)
                {
                    var filter = _filters[j];
                    audio = filter.ProcessSample(audio);
                    if (double.IsNaN(audio)) audio = buffer[outputIndex];

                    audio *= RadioFilter.BOOST;
                }
            }

            if ((modulation == Modulation.FM || modulation == Modulation.SINCGARS)
                && effectProvider.NATOTone.Loaded
                && natoToneEnabled)
            {
                var natoTone = effectProvider.NATOTone.AudioEffectFloat;
                audio += natoTone[natoPosition] * natoToneVolume;
                natoPosition++;

                if (natoPosition == natoTone.Length) natoPosition = 0;
            }

            if (modulation == Modulation.HAVEQUICK
                && effectProvider.HAVEQUICKTone.Loaded
                && hqToneEnabled)
            {
                var hqTone = effectProvider.HAVEQUICKTone.AudioEffectFloat;

                audio += hqTone[hqTonePosition] * hqToneVolume;
                hqTonePosition++;

                if (hqTonePosition == hqTone.Length)
                {
                    var reset = _random.NextDouble();

                    if (reset > HQ_RESET_CHANCE)
                        hqTonePosition = 0;
                    else
                        //one back to try again
                        hqTonePosition += -1;
                }
            }

            audio = AddRadioBackgroundNoiseEffect(audio, modulation, freq);

            // clip
            if (audio > 1.0f)
                audio = 1.0f;
            if (audio < -1.0f)
                audio = -1.0f;

            buffer[outputIndex] = (float)audio;

            outputIndex++;
        }
    }

    private double AddRadioBackgroundNoiseEffect(double audio, Modulation modulation, double freq)
    {
        if (radioBackgroundNoiseEffect)
        {
            if (modulation == Modulation.HAVEQUICK || modulation == Modulation.AM)
            {
                //mix in based on frequency
                if (freq >= 200d * 1000000)
                {
                    if (effectProvider.UHFNoise.Loaded)
                    {
                        var noise = effectProvider.UHFNoise.AudioEffectFloat;
                        //UHF Band?
                        audio += noise[uhfNoisePosition] * uhfVol;
                        uhfNoisePosition++;

                        if (uhfNoisePosition == noise.Length) uhfNoisePosition = 0;
                    }
                }
                else if (freq > 80d * 1000000)
                {
                    if (effectProvider.VHFNoise.Loaded)
                    {
                        //VHF Band? - Very rough
                        var noise = effectProvider.VHFNoise.AudioEffectFloat;
                        audio += noise[vhfNoisePosition] * vhfVol;
                        vhfNoisePosition++;

                        if (vhfNoisePosition == noise.Length) vhfNoisePosition = 0;
                    }
                }
                else
                {
                    if (effectProvider.HFNoise.Loaded)
                    {
                        //HF!
                        var noise = effectProvider.HFNoise.AudioEffectFloat;
                        audio += noise[hfNoisePosition] * hfVol;
                        hfNoisePosition++;

                        if (hfNoisePosition == noise.Length) hfNoisePosition = 0;
                    }
                }
            }
            else if (modulation == Modulation.FM || modulation == Modulation.SINCGARS)
            {
                if (effectProvider.FMNoise.Loaded)
                {
                    //FM picks up most of the 20-60 ish range + has a different effect
                    //HF!
                    var noise = effectProvider.FMNoise.AudioEffectFloat;
                    //UHF Band?
                    audio += noise[fmNoisePosition] * fmVol;
                    fmNoisePosition++;

                    if (fmNoisePosition == noise.Length) fmNoisePosition = 0;
                }
            }
        }

        return audio;
    }
}