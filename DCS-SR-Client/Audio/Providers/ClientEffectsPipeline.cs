using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using MathNet.Filtering;
using NAudio.Dsp;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers
{
    public class ClientEffectsPipeline
    {
        private readonly Random _random = new Random();

        private OnlineFilter[] _filters;

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;

        private static readonly double HQ_RESET_CHANCE = 0.8;

        private int hqTonePosition = 0;
        private int natoPosition = 0;
        private int fmNoisePosition = 0;
        private int vhfNoisePosition = 0;
        private int uhfNoisePosition = 0;
        private int hfNoisePosition = 0;

        private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;

        private bool natoToneEnabled;
        private bool hqToneEnabled;
        private bool radioEffectsEnabled;
        private bool clippingEnabled;
        private float hqToneVolume;
        private float natoToneVolume;

        private float fmVol;
        private float hfVol;
        private float uhfVol;
        private float vhfVol;

        private long lastRefresh = 0; //last refresh of settings

        private readonly Settings.ProfileSettingsStore profileSettings;

        private bool radioEffects;
        private bool radioBackgroundNoiseEffect;

        private CachedAudioEffect amCollisionEffect;
        private int amEffectPosition = 0;
        private float amCollisionVol = 1.0f;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;
        
        public ClientEffectsPipeline()
        {
            profileSettings = Settings.GlobalSettingsStore.Instance.ProfileSettingsStore;
            serverSettings =  SyncedServerSettings.Instance;

            _filters = new OnlineFilter[2];
            _filters[0] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.OUTPUT_SAMPLE_RATE, 560, 3900);
            _filters[1] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.OUTPUT_SAMPLE_RATE, 100, 4500);

            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.OUTPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.OUTPUT_SAMPLE_RATE, 4130, 2.0f);
            RefreshSettings();

            amCollisionEffect = effectProvider.AMCollision;
           
        }

        private void RefreshSettings()
        {
            //only get settings every 3 seconds - and cache them - issues with performance
            long now = DateTime.Now.Ticks;

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

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect) ;

                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
            }
        }

        public float[] ProcessClientTransmissions(float[] tempBuffer, List<DeJitteredTransmission> transmissions, out int clientTransmissionLength)
        {
            RefreshSettings();
            DeJitteredTransmission lastTransmission = transmissions[0];

            clientTransmissionLength = 0;
            foreach (var transmission in transmissions)
            {
                for (int i = 0; i < transmission.PCMAudioLength; i++)
                {
                    tempBuffer[i] += transmission.PCMMonoAudio[i];
                }

                clientTransmissionLength = Math.Max(clientTransmissionLength, transmission.PCMAudioLength);
            }

            bool process = true;

            // take info account server setting AND volume of this radio AND if its AM or FM
            // FOR HAVEQUICK - only if its MORE THAN TWO
            if (lastTransmission.ReceivedRadio != 0
                && !lastTransmission.NoAudioEffects
                && (lastTransmission.Modulation == RadioInformation.Modulation.AM
                    || lastTransmission.Modulation == RadioInformation.Modulation.FM
                    || lastTransmission.Modulation == RadioInformation.Modulation.SINCGARS
                    || lastTransmission.Modulation == RadioInformation.Modulation.HAVEQUICK)
                && irlRadioRXInterference)
            {
                if (transmissions.Count > 1)
                {
                    //All AM is wrecked if more than one transmission
                    //For HQ - only if more than TWO transmissions and its totally fucked
                    if (lastTransmission.Modulation == RadioInformation.Modulation.HAVEQUICK && transmissions.Count > 2 && amCollisionEffect.Loaded)
                    {
                        //replace the buffer with our own
                        int outIndex = 0;
                        while (outIndex < clientTransmissionLength)
                        {
                            var amByte = this.amCollisionEffect.AudioEffectFloat[amEffectPosition++];

                            tempBuffer[outIndex++] = (amByte *amCollisionVol) * lastTransmission.Volume;

                            if (amEffectPosition == amCollisionEffect.AudioEffectFloat.Length)
                            {
                                amEffectPosition = 0;
                            }
                        }

                        process = false;
                    }
                    else if (lastTransmission.Modulation == RadioInformation.Modulation.AM && amCollisionEffect.Loaded)
                    {
                        //AM https://www.youtube.com/watch?v=yHRDjhkrDbo
                        //Heterodyne tone AND audio from multiple transmitters in a horrible mess
                        //TODO improve this

              
                        //process here first
                        tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);
                        process = false;

                        //apply heterodyne tone to the mixdown
                        //replace the buffer with our own
                        int outIndex = 0;
                        while (outIndex < clientTransmissionLength)
                        {
                            var amByte = this.amCollisionEffect.AudioEffectFloat[amEffectPosition++];

                            tempBuffer[outIndex++] += ((amByte * amCollisionVol) * lastTransmission.Volume);

                            if (amEffectPosition == amCollisionEffect.AudioEffectFloat.Length)
                            {
                                amEffectPosition = 0;
                            }
                        }
                    }
                    else if (lastTransmission.Modulation == RadioInformation.Modulation.FM || lastTransmission.Modulation == RadioInformation.Modulation.SINCGARS)
                    {
                        //FM picketing / picket fencing - pick one transmission at random
                        //TODO improve this to pick the stronger frequency?

                        int index = _random.Next(transmissions.Count);
                        var transmission = transmissions[index];

                        for (int i = 0; i < transmission.PCMAudioLength; i++)
                        {
                            tempBuffer[i] = transmission.PCMMonoAudio[i];
                        }

                        clientTransmissionLength = transmission.PCMMonoAudio.Length;
                    }
                }
            }

            //only process if AM effect doesnt apply
            if (process)
                tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);


            return tempBuffer;
        }

        public float[] ProcessClientAudioSamples(float[] buffer, int count, int offset, DeJitteredTransmission transmission)
        {
            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == RadioInformation.Modulation.MIDS
                    || transmission.Modulation == RadioInformation.Modulation.SATCOM
                    || transmission.Modulation == RadioInformation.Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        AddRadioEffectIntercom(buffer, count, offset, transmission.Modulation);
                    }
                }
                else
                {
                    AddRadioEffect(buffer, count, offset, transmission.Modulation, transmission.Frequency);
                }
            }

            //final adjust
            AdjustVolume(buffer, count, offset, transmission.Volume);

            return buffer;
        }

        private void AdjustVolume(float[] buffer, int count, int offset, float volume)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                buffer[outputIndex] *= volume;

                outputIndex++;
            }
        }

        private void AddRadioEffectIntercom(float[] buffer, int count, int offset,RadioInformation.Modulation modulation)
        {
            int outputIndex = offset;
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


        private void AddRadioEffect(float[] buffer, int count, int offset, RadioInformation.Modulation modulation, double freq)
        {
            int outputIndex = offset;
             
            while (outputIndex < offset + count)
            {
                var audio = (double) buffer[outputIndex];

                if (radioEffectsEnabled)
                {
                    if (clippingEnabled)
                    {
                        if (audio > RadioFilter.CLIPPING_MAX)
                        {
                            audio = RadioFilter.CLIPPING_MAX;
                        }
                        else if (audio < RadioFilter.CLIPPING_MIN)
                        {
                            audio = RadioFilter.CLIPPING_MIN;
                        }
                    }

                    //high and low pass filter
                    for (int j = 0; j < _filters.Length; j++)
                    {
                        var filter = _filters[j];
                        audio = filter.ProcessSample(audio);
                        if (double.IsNaN(audio))
                        {
                            audio = (double)buffer[outputIndex];
                        }

                        audio *= RadioFilter.BOOST;
                    }
                }

                if ((modulation == RadioInformation.Modulation.FM || modulation == RadioInformation.Modulation.SINCGARS)
                    && effectProvider.NATOTone.Loaded
                    && natoToneEnabled)
                {
                    var natoTone = effectProvider.NATOTone.AudioEffectFloat;
                    audio += ((natoTone[natoPosition]) * natoToneVolume);
                    natoPosition++;

                    if (natoPosition == natoTone.Length)
                    {
                        natoPosition = 0;
                    }
                }

                if (modulation == RadioInformation.Modulation.HAVEQUICK
                     && effectProvider.HAVEQUICKTone.Loaded
                     && hqToneEnabled)
                {
                    var hqTone = effectProvider.HAVEQUICKTone.AudioEffectFloat;

                    audio += ((hqTone[hqTonePosition]) * hqToneVolume);
                    hqTonePosition++;

                    if (hqTonePosition == hqTone.Length)
                    {
                        var reset = _random.NextDouble();

                        if (reset > HQ_RESET_CHANCE)
                        {
                            hqTonePosition = 0;
                        }
                        else
                        {
                            //one back to try again
                            hqTonePosition += -1;
                        }
                    }
                }

                audio = AddRadioBackgroundNoiseEffect(audio, modulation,freq);

                // clip
                if (audio > 1.0f)
                    audio = 1.0f;
                if (audio < -1.0f)
                    audio = -1.0f;

                buffer[outputIndex] = (float) audio;

                outputIndex++;
            }
        }

        private double AddRadioBackgroundNoiseEffect(double audio, RadioInformation.Modulation modulation, double freq)
        {
            if (radioBackgroundNoiseEffect)
            {
                if (modulation == RadioInformation.Modulation.HAVEQUICK || modulation == RadioInformation.Modulation.AM)
                {
                    //mix in based on frequency
                    if (freq >= 200d * 1000000)
                    {
                        if (effectProvider.UHFNoise.Loaded)
                        {
                            var noise = effectProvider.UHFNoise.AudioEffectFloat;
                            //UHF Band?
                            audio += ((noise[uhfNoisePosition]) * uhfVol);
                            uhfNoisePosition++;

                            if (uhfNoisePosition == noise.Length)
                            {
                                uhfNoisePosition = 0;
                            }
                        }
                    }
                    else if (freq > 80d * 1000000)
                    {
                        if (effectProvider.VHFNoise.Loaded)
                        {
                            //VHF Band? - Very rough
                            var noise = effectProvider.VHFNoise.AudioEffectFloat;
                            audio += ((noise[vhfNoisePosition]) * vhfVol);
                            vhfNoisePosition++;

                            if (vhfNoisePosition == noise.Length)
                            {
                                vhfNoisePosition = 0;
                            }
                        }
                    }
                    else
                    {
                        if (effectProvider.HFNoise.Loaded)
                        {
                            //HF!
                            var noise = effectProvider.HFNoise.AudioEffectFloat;
                            audio += ((noise[hfNoisePosition]) * hfVol);
                            hfNoisePosition++;

                            if (hfNoisePosition == noise.Length)
                            {
                                hfNoisePosition = 0;
                            }
                        }
                    }
                }
                else if (modulation == RadioInformation.Modulation.FM || modulation == RadioInformation.Modulation.SINCGARS)
                {
                    if (effectProvider.FMNoise.Loaded)
                    {

                        //FM picks up most of the 20-60 ish range + has a different effect
                        //HF!
                        var noise = effectProvider.FMNoise.AudioEffectFloat;
                        //UHF Band?
                        audio += ((noise[fmNoisePosition]) * fmVol);
                        fmNoisePosition++;

                        if (fmNoisePosition == noise.Length)
                        {
                            fmNoisePosition = 0;
                        }
                    }
                }
            }

            return audio;
        }
    }
}
