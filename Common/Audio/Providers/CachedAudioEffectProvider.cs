using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

public class CachedAudioEffectProvider
{
    public delegate Stream CachedEffectsLoaderDelegate(string fileName);

    private static CachedAudioEffectProvider _instance;

    private readonly string sourceFolder;

    private CachedAudioEffectProvider()
    {
        sourceFolder = AppDomain.CurrentDomain.BaseDirectory + "\\AudioEffects\\";
        LoadEffects();
    }

    //Used on Android or other platforms to change the loader system
    public static CachedEffectsLoaderDelegate CachedEffectsLoader { get; set; }

    private Dictionary<string, CachedAudioEffect> _ambientAudioEffects { get; } = new();

    public List<CachedAudioEffect> RadioTransmissionStart { get; set; }
    public List<CachedAudioEffect> RadioTransmissionEnd { get; set; }

    public List<CachedAudioEffect> IntercomTransmissionStart { get; set; }
    public List<CachedAudioEffect> IntercomTransmissionEnd { get; set; }

    public static CachedAudioEffectProvider Instance
    {
        get
        {
            if (_instance == null) _instance = new CachedAudioEffectProvider();
            //stops cyclic init
            return _instance;
        }
    }

    public CachedAudioEffect SelectedRadioTransmissionStartEffect
    {
        get
        {
            var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore
                .GetClientSettingString(ProfileSettingsKeys.RadioTransmissionStartSelection).ToLowerInvariant();

            foreach (var startEffect in RadioTransmissionStart)
                if (startEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    return startEffect;

            return RadioTransmissionStart[0];
        }
    }

    public CachedAudioEffect SelectedRadioTransmissionEndEffect
    {
        get
        {
            var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore
                .GetClientSettingString(ProfileSettingsKeys.RadioTransmissionEndSelection).ToLowerInvariant();

            foreach (var endEffect in RadioTransmissionEnd)
                if (endEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    return endEffect;

            return RadioTransmissionEnd[0];
        }
    }

    public CachedAudioEffect SelectedIntercomTransmissionStartEffect
    {
        get
        {
            var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore
                .GetClientSettingString(ProfileSettingsKeys.IntercomTransmissionStartSelection).ToLowerInvariant();

            foreach (var startEffect in IntercomTransmissionStart)
                if (startEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    return startEffect;

            return IntercomTransmissionStart[0];
        }
    }

    public CachedAudioEffect SelectedIntercomTransmissionEndEffect
    {
        get
        {
            var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore
                .GetClientSettingString(ProfileSettingsKeys.IntercomTransmissionEndSelection).ToLowerInvariant();

            foreach (var endEffect in IntercomTransmissionEnd)
                if (endEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    return endEffect;

            return IntercomTransmissionEnd[0];
        }
    }

    public CachedAudioEffect KY58EncryptionTransmitTone { get; set; }
    public CachedAudioEffect KY58EncryptionEndTone { get; set; }
    public CachedAudioEffect NATOTone { get; set; }
    public CachedAudioEffect MIDSTransmitTone { get; set; }
    public CachedAudioEffect MIDSEndTone { get; set; }

    public CachedAudioEffect HAVEQUICKTone { get; set; }

    public void LoadEffects()
    {
        //init lists
        RadioTransmissionStart = new List<CachedAudioEffect>();
        RadioTransmissionEnd = new List<CachedAudioEffect>();

        IntercomTransmissionStart = new List<CachedAudioEffect>();
        IntercomTransmissionEnd = new List<CachedAudioEffect>();

        LoadRadioStartAndEndEffects();
        LoadIntercomStartAndEndEffects();

        KY58EncryptionTransmitTone =
            new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.KY_58_TX, CachedEffectsLoader);
        KY58EncryptionEndTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.KY_58_RX, CachedEffectsLoader);

        NATOTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.NATO_TONE, CachedEffectsLoader);

        MIDSTransmitTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.MIDS_TX, CachedEffectsLoader);
        MIDSEndTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.MIDS_TX_END, CachedEffectsLoader);

        HAVEQUICKTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE, CachedEffectsLoader);
    }

    private void LoadRadioStartAndEndEffects()
    {
        if (Directory.Exists(sourceFolder))
        {
            var audioEffectsList = Directory.EnumerateFiles(sourceFolder);

            //might need to split the path - we'll see
            foreach (var effectPath in audioEffectsList)
            {
                var effect = effectPath.Split(Path.DirectorySeparatorChar).Last();

                if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START
                        .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START,
                        effect, effectPath, CachedEffectsLoader);

                    if (audioEffect.AudioEffectFloat != null) RadioTransmissionStart.Add(audioEffect);
                }
                else if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END
                             .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END,
                        effect, effectPath, CachedEffectsLoader);

                    if (audioEffect.AudioEffectFloat != null) RadioTransmissionEnd.Add(audioEffect);
                }
            }

            //IF the audio folder is missing - to avoid a crash, init with a blank one
            if (RadioTransmissionStart.Count == 0)
                RadioTransmissionStart.Add(
                    new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START, CachedEffectsLoader));

            if (RadioTransmissionEnd.Count == 0)
                RadioTransmissionEnd.Add(new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END,
                    CachedEffectsLoader));
        }
        else
        {
            //IF the audio folder is missing - to avoid a crash, init with a blank one
            if (RadioTransmissionStart.Count == 0)
                RadioTransmissionStart.Add(
                    new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START, CachedEffectsLoader));

            if (RadioTransmissionEnd.Count == 0)
                RadioTransmissionEnd.Add(new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END,
                    CachedEffectsLoader));
        }
    }

    private void LoadIntercomStartAndEndEffects()
    {
        if (Directory.Exists(sourceFolder))
        {
            var audioEffectsList = Directory.EnumerateFiles(sourceFolder);

            //might need to split the path - we'll see
            foreach (var effectPath in audioEffectsList)
            {
                var effect = effectPath.Split(Path.DirectorySeparatorChar).Last();

                if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START
                        .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START,
                        effect, effectPath, CachedEffectsLoader);

                    if (audioEffect.AudioEffectFloat != null) IntercomTransmissionStart.Add(audioEffect);
                }
                else if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END
                             .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END,
                        effect, effectPath, CachedEffectsLoader);

                    if (audioEffect.AudioEffectFloat != null) IntercomTransmissionEnd.Add(audioEffect);
                }
            }

            //IF the audio folder is missing - to avoid a crash, init with a blank one
            if (IntercomTransmissionStart.Count == 0)
                IntercomTransmissionStart.Add(
                    new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START,
                        CachedEffectsLoader));

            if (IntercomTransmissionEnd.Count == 0)
                IntercomTransmissionEnd.Add(
                    new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END, CachedEffectsLoader));
        }
        else
        {
            //IF the audio folder is missing - to avoid a crash, init with a blank one
            if (IntercomTransmissionStart.Count == 0)
                IntercomTransmissionStart.Add(
                    new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START,
                        CachedEffectsLoader));

            if (IntercomTransmissionEnd.Count == 0)
                IntercomTransmissionEnd.Add(
                    new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END, CachedEffectsLoader));
        }
    }

    //TODO unload ever?
    public CachedAudioEffect GetAmbientEffect(string name)
    {
        name = name.ToLowerInvariant();
        if (_ambientAudioEffects.TryGetValue(name, out var effect)) return effect;

        effect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.AMBIENT_COCKPIT,
            name + ".wav",
            AppDomain.CurrentDomain.BaseDirectory +
            $"{Path.DirectorySeparatorChar}AudioEffects{Path.DirectorySeparatorChar}Ambient{Path.DirectorySeparatorChar}" +
            name + ".wav", CachedEffectsLoader);

        _ambientAudioEffects[name] = effect;

        return effect;
    }
}