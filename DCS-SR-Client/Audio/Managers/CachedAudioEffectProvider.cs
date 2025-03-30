using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers
{
    class CachedAudioEffectProvider
    {
        public List<CachedAudioEffect> RadioTransmissionStart { get; }
        public List<CachedAudioEffect> RadioTransmissionEnd { get; }

        public List<CachedAudioEffect> IntercomTransmissionStart { get; }
        public List<CachedAudioEffect> IntercomTransmissionEnd { get; }

        private Dictionary<string, CachedAudioEffect> _ambientAudioEffects { get; } =
            new Dictionary<string, CachedAudioEffect>();



        private static CachedAudioEffectProvider _instance;

        public static CachedAudioEffectProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CachedAudioEffectProvider();

                    //stops cyclic init

                }
                return _instance;
            }
        }

        public CachedAudioEffect SelectedRadioTransmissionStartEffect
        {
            get
            {
                var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore.RadioTransmissionStartSelection.ToLowerInvariant();

                foreach (var startEffect in RadioTransmissionStart)
                {
                    if (startEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    {
                        return startEffect;
                    }
                }

                return RadioTransmissionStart[0];
            }
        }

        public CachedAudioEffect SelectedRadioTransmissionEndEffect
        {
            get
            {
                var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore.RadioTransmissionEndSelection.ToLowerInvariant();

                foreach (var endEffect in RadioTransmissionEnd)
                {
                    if (endEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    {
                        return endEffect;
                    }
                }

                return RadioTransmissionEnd[0];
            }
        }

        public CachedAudioEffect SelectedIntercomTransmissionStartEffect
        {
            get
            {
                var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore.IntercomTransmissionStartSelection.ToLowerInvariant();

                foreach (var startEffect in IntercomTransmissionStart)
                {
                    if (startEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    {
                        return startEffect;
                    }
                }

                return IntercomTransmissionStart[0];
            }
        }

        public CachedAudioEffect SelectedIntercomTransmissionEndEffect
        {
            get
            {
                var selectedTone = GlobalSettingsStore.Instance.ProfileSettingsStore.IntercomTransmissionEndSelection.ToLowerInvariant();

                foreach (var endEffect in IntercomTransmissionEnd)
                {
                    if (endEffect.FileName.ToLowerInvariant().Equals(selectedTone))
                    {
                        return endEffect;
                    }
                }

                return IntercomTransmissionEnd[0];
            }
        }

        public CachedAudioEffect KY58EncryptionTransmitTone { get; }
        public CachedAudioEffect KY58EncryptionEndTone { get; }
        public CachedAudioEffect NATOTone { get; }
        public CachedAudioEffect MIDSTransmitTone { get; }
        public CachedAudioEffect MIDSEndTone { get; }

        public CachedAudioEffect HAVEQUICKTone { get; }

        public CachedAudioEffect FMNoise { get; }
        public CachedAudioEffect UHFNoise { get; }
        public CachedAudioEffect VHFNoise { get; }
        public CachedAudioEffect HFNoise { get; }

        public CachedAudioEffect AMCollision { get; set; }

        private readonly string sourceFolder;

        private CachedAudioEffectProvider()
        {
            sourceFolder = AppDomain.CurrentDomain.BaseDirectory + "\\AudioEffects\\";
            
            //init lists
            RadioTransmissionStart = new List<CachedAudioEffect>();
            RadioTransmissionEnd = new List<CachedAudioEffect>();

            IntercomTransmissionStart = new List<CachedAudioEffect>();
            IntercomTransmissionEnd = new List<CachedAudioEffect>();

            LoadRadioStartAndEndEffects();
            LoadIntercomStartAndEndEffects();

            KY58EncryptionTransmitTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.KY_58_TX);
            KY58EncryptionEndTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.KY_58_RX);
            
            NATOTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.NATO_TONE);
            
            MIDSTransmitTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.MIDS_TX);
            MIDSEndTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.MIDS_TX_END);

            HAVEQUICKTone = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE);

            FMNoise = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.FM_NOISE);
            VHFNoise = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.VHF_NOISE);
            UHFNoise = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.UHF_NOISE);
            HFNoise = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.HF_NOISE);

            //sort out volume (if needed)
            // CreateAudioEffectDouble(HAVEQUICKTone);
            // CreateAudioEffectDouble(NATOTone);
            // CreateAudioEffectDouble(FMNoise);
            // CreateAudioEffectDouble(UHFNoise);
            // CreateAudioEffectDouble(VHFNoise);
            // CreateAudioEffectDouble(HFNoise);

            AMCollision = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.AM_COLLISION);
            // CreateAudioEffectDouble(AMCollision);
        }

        private void LoadRadioStartAndEndEffects()
        {
            var audioEffectsList = Directory.EnumerateFiles(sourceFolder);

            //might need to split the path - we'll see
            foreach (var effectPath in audioEffectsList)
            {
                var effect =  effectPath.Split(Path.DirectorySeparatorChar).Last();

                if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START
                    .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START, effect, effectPath);

                    if (audioEffect.AudioEffectFloat != null)
                    {
                        RadioTransmissionStart.Add(audioEffect);
                    }

                }
                else if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END
                    .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END, effect,effectPath);

                    if (audioEffect.AudioEffectFloat != null)
                    {
                        RadioTransmissionEnd.Add(audioEffect);
                    }
                }
            }

            //IF the audio folder is missing - to avoid a crash, init with a blank one
            if (RadioTransmissionStart.Count == 0)
            {
                RadioTransmissionStart.Add(new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START));
            }
            if (RadioTransmissionEnd.Count == 0)
            {
                RadioTransmissionEnd.Add(new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END));
            }

        }

        private void LoadIntercomStartAndEndEffects()
        {
            var audioEffectsList = Directory.EnumerateFiles(sourceFolder);

            //might need to split the path - we'll see
            foreach (var effectPath in audioEffectsList)
            {
                var effect = effectPath.Split(Path.DirectorySeparatorChar).Last();

                if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START
                    .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START, effect, effectPath);

                    if (audioEffect.AudioEffectFloat != null)
                    {
                        IntercomTransmissionStart.Add(audioEffect);
                    }

                }
                else if (effect.ToLowerInvariant().StartsWith(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END
                    .ToString().ToLowerInvariant()))
                {
                    var audioEffect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END, effect, effectPath);

                    if (audioEffect.AudioEffectFloat != null)
                    {
                        IntercomTransmissionEnd.Add(audioEffect);
                    }
                }
            }

            //IF the audio folder is missing - to avoid a crash, init with a blank one
            if (IntercomTransmissionStart.Count == 0)
            {
                IntercomTransmissionStart.Add(new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_START));
            }
            if (IntercomTransmissionEnd.Count == 0)
            {
                IntercomTransmissionEnd.Add(new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.INTERCOM_TRANS_END));
            }

        }

        //TODO unload ever?
        public CachedAudioEffect GetAmbientEffect(string name)
        {
            name = name.ToLowerInvariant();
            if (_ambientAudioEffects.TryGetValue(name, out CachedAudioEffect effect))
            {
                return effect;
            }

            effect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.AMBIENT_COCKPIT,
                name);

            _ambientAudioEffects[name] = effect;

            return effect;
        }
    }
}
