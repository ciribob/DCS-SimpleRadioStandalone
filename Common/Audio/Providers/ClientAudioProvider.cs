﻿using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class ClientAudioProvider : AudioProvider
{
    private readonly Random _random = new();
    public float[] PcmAudioFloat { get; set; } = new float[Constants.OUTPUT_SAMPLE_RATE * 120 / 1000]; // max Opus frame size.

    //progress per radio
    private readonly Dictionary<string, int>[] ambientEffectProgress;

    private readonly CachedAudioEffectProvider audioEffectProvider = CachedAudioEffectProvider.Instance;

    private readonly ProfileSettingsStore settingsStore = GlobalSettingsStore.Instance.ProfileSettingsStore;
    private bool ambientCockpitEffectEnabled = true;

    private float ambientCockpitEffectVolume = 1.0f;
    private bool ambientCockpitIntercomEffectEnabled = true;

    private double lastLoaded;

    //   private readonly WaveFileWriter waveWriter;
    public ClientAudioProvider(bool passThrough = false) : base(passThrough)
    {
        var radios = Constants.MAX_RADIOS;
        if (!passThrough)
        {
            JitterBufferProviderInterface =
                new JitterBufferProviderInterface[radios];

            for (var i = 0; i < radios; i++)
                JitterBufferProviderInterface[i] =
                    new JitterBufferProviderInterface(new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));
        }
        //    waveWriter = new NAudio.Wave.WaveFileWriter($@"C:\\temp\\output{RandomFloat()}.wav", new WaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));


        ambientEffectProgress = new Dictionary<string, int>[radios];

        for (var i = 0; i < radios; i++) ambientEffectProgress[i] = new Dictionary<string, int>();
    }

    public JitterBufferProviderInterface[] JitterBufferProviderInterface { get; }

    public override JitterBufferAudio AddClientAudioSamples(ClientAudio audio)
    {
        ReLoadSettings();
        //sort out volume
        //            var timer = new Stopwatch();
        //            timer.Start();

        var newTransmission = LikelyNewTransmission();

        // Target buffer contains at least one frame.
        var decodedLength = _decoder.DecodeFloat(audio.EncodedAudio, PcmAudioFloat, newTransmission);

        if (decodedLength <= 0)
        {
            Logger.Info("Failed to decode audio from Packet for client");
            return null;
        }

        //convert the byte buffer to a wave buffer
        //   var waveBuffer = new WaveBuffer(tmp);

        // waveWriter.WriteSamples(tmp,0,tmp.Length);
        var
            decrytable =
                audio.Decryptable /* || (audio.Encryption == 0) <--- this test has already been performed by all callers and would require another call to check for STRICT_AUDIO_ENCRYPTION */;

        var pcmAudio = PcmAudioFloat.AsSpan(0, decodedLength);
        if (decrytable)
        {
            //adjust for LOS + Distance + Volume
            AdjustVolumeForLoss(audio, pcmAudio);

            //Add cockpit effect - but not for Intercom unless you specifically opt in
            if ((ambientCockpitEffectEnabled && audio.Modulation != (short)Modulation.INTERCOM)
                || (ambientCockpitEffectEnabled && audio.Modulation == (short)Modulation.INTERCOM &&
                    ambientCockpitIntercomEffectEnabled))
                AddCockpitAmbientAudio(audio, pcmAudio);
        }
        else
        {
            AddEncryptionFailureEffect(audio, pcmAudio);
        }

        LastUpdate = DateTime.Now.Ticks;

        //return and skip jitter buffer if its passthrough as its local mic
        if (passThrough)
            //return MONO PCM 16 as bytes
            // NOT MONO PCM 32
            return new JitterBufferAudio
            {
                Audio = pcmAudio.ToArray(),
                PacketNumber = audio.PacketNumber,
                Decryptable = decrytable,
                Modulation = (Modulation)audio.Modulation,
                ReceivedRadio = audio.ReceivedRadio,
                Volume = audio.Volume,
                IsSecondary = audio.IsSecondary,
                Frequency = audio.Frequency,
                NoAudioEffects = audio.NoAudioEffects,
                Guid = audio.ClientGuid,
                OriginalClientGuid = audio.OriginalClientGuid,
                Encryption = audio.Encryption
            };

        JitterBufferProviderInterface[audio.ReceivedRadio].AddSamples(new JitterBufferAudio
        {
            Audio = pcmAudio.ToArray(),
            PacketNumber = audio.PacketNumber,
            Decryptable = decrytable,
            Modulation = (Modulation)audio.Modulation,
            ReceivedRadio = audio.ReceivedRadio,
            Volume = audio.Volume,
            IsSecondary = audio.IsSecondary,
            Frequency = audio.Frequency,
            NoAudioEffects = audio.NoAudioEffects,
            Guid = audio.ClientGuid,
            OriginalClientGuid = audio.OriginalClientGuid,
            Encryption = audio.Encryption
        });


        return null;
    }

    //high throughput - cache these settings for 3 seconds
    private void ReLoadSettings()
    {
        var now = DateTime.Now.Ticks;
        if (now - lastLoaded > 30000000)
        {
            lastLoaded = now;
            ambientCockpitEffectEnabled =
                settingsStore.GetClientSettingBool(ProfileSettingsKeys.AmbientCockpitNoiseEffect);
            ambientCockpitEffectVolume =
                settingsStore.GetClientSettingFloat(ProfileSettingsKeys.AmbientCockpitNoiseEffectVolume);
            ambientCockpitIntercomEffectEnabled =
                settingsStore.GetClientSettingBool(ProfileSettingsKeys.AmbientCockpitIntercomNoiseEffect);
        }
    }

    private void AddCockpitAmbientAudio(ClientAudio clientAudio, Span<float> pcmAudio)
    {
        //           clientAudio.Ambient.abType = "uh1";
        //           clientAudio.Ambient.vol = 0.35f;

        var abType = clientAudio.Ambient?.abType;

        if (string.IsNullOrEmpty(abType)) return;

        var effect = audioEffectProvider.GetAmbientEffect(clientAudio.Ambient.abType);

        var vol = clientAudio.Ambient.vol;

        if (clientAudio.Modulation == (short)Modulation.MIDS)
            //for MIDS - half volume again - just for ambient vol
            vol = vol / 0.50f;

        var ambientEffectProg = ambientEffectProgress[clientAudio.ReceivedRadio];

        if (effect.Loaded)
        {
            var effectLength = effect.AudioEffectFloat.Length;

            if (!ambientEffectProg.TryGetValue(clientAudio.Ambient.abType, out var progress))
            {
                progress = 0;
                ambientEffectProg[clientAudio.Ambient.abType] = 0;
            }

            for (var i = 0; i < pcmAudio.Length; i++)
            {
                pcmAudio[i] += effect.AudioEffectFloat[progress] * (vol * ambientCockpitEffectVolume);

                progress++;

                if (progress >= effectLength) progress = 0;
            }

            ambientEffectProg[clientAudio.Ambient.abType] = progress;
        }
    }

    private void AdjustVolumeForLoss(ClientAudio clientAudio, Span<float> pcmAudio)
    {
        if (clientAudio.Modulation == (short)Modulation.MIDS || clientAudio.Modulation == (short)Modulation.SATCOM
                                                             || clientAudio.Modulation == (short)Modulation.INTERCOM)
            return;

        for (var i = 0; i < pcmAudio.Length; i++)
        {
            var audioFloat = pcmAudio[i];

            //add in radio loss
            //if less than loss reduce volume
            if (clientAudio.RecevingPower > 0.85) // less than 20% or lower left
                //gives linear signal loss from 15% down to 0%
                audioFloat = (float)(audioFloat * (1.0f - clientAudio.RecevingPower));

            //0 is no loss so if more than 0 reduce volume
            if (clientAudio.LineOfSightLoss > 0) audioFloat = audioFloat * (1.0f - clientAudio.LineOfSightLoss);

            pcmAudio[i] = audioFloat;
        }
    }

    private void AddEncryptionFailureEffect(ClientAudio clientAudio, Span<float> pcmAudio)
    {
        for (var i = 0; i < pcmAudio.Length; i++) pcmAudio[i] = RandomFloat();
    }


    private float RandomFloat()
    {
        //random float at max volume at eights
        var f = _random.Next(-32768 / 8, 32768 / 8) / (float)32768;
        if (f > 1) f = 1;
        if (f < -1) f = -1;

        return f;
    }
}