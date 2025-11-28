using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;

public class DCSRadio
{
    public enum EncryptionMode
    {
        NO_ENCRYPTION = 0,
        ENCRYPTION_JUST_OVERLAY = 1,
        ENCRYPTION_FULL = 2,
        ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE = 3

        // 0  is no controls
        // 1 is FC3 Gui Toggle + Gui Enc key setting
        // 2 is InCockpit toggle + Incockpit Enc setting
        // 3 is Incockpit toggle + Gui Enc Key setting
    }

    public enum FreqMode
    {
        COCKPIT = 0,
        OVERLAY = 1
    }

    public enum RetransmitMode
    {
        COCKPIT = 0,
        OVERLAY = 1,
        DISABLED = 2
    }

    public enum VolumeMode
    {
        COCKPIT = 0,
        OVERLAY = 1
    }

    [JsonNetworkIgnoreSerialization] public int channel = -1;

    public bool enc; // encryption enabled
    public byte encKey;


    [JsonNetworkIgnoreSerialization] public EncryptionMode encMode = EncryptionMode.NO_ENCRYPTION;

    [JsonNetworkIgnoreSerialization] [JsonDCSIgnoreSerialization]
    public bool expansion;

    public double freq = 1;

    [JsonDCSIgnoreSerialization] [JsonNetworkIgnoreSerialization]
    public double freqMax = 1;

    [JsonDCSIgnoreSerialization] [JsonNetworkIgnoreSerialization]
    public double freqMin = 1;

    [JsonNetworkIgnoreSerialization] [JsonDCSIgnoreSerialization]
    public FreqMode freqMode = FreqMode.COCKPIT;

    [JsonNetworkIgnoreSerialization] [JsonDCSIgnoreSerialization]
    public FreqMode guardFreqMode = FreqMode.COCKPIT;

    public Modulation modulation = Modulation.DISABLED;

    [JsonNetworkIgnoreSerialization] public string name = "";

    // Radio model (arc210, link16, r812, etc).
    [JsonNetworkIgnoreSerialization] public string model = "";

    //should the radio restransmit?
    public bool retransmit;

    [JsonNetworkIgnoreSerialization]
    [JsonDCSIgnoreSerialization]
    //override for intercom - to allow tuning to another units intercom for instructor mode
    public uint IntercomUnitId { get; set; } = 0;

    [JsonNetworkIgnoreSerialization] [JsonDCSIgnoreSerialization]
    public RetransmitMode rtMode = RetransmitMode.DISABLED;

    [JsonNetworkIgnoreSerialization] public bool rxOnly;

    public double secFreq = 1;

    [JsonNetworkIgnoreSerialization] public bool simul;

    [JsonNetworkIgnoreSerialization] [JsonDCSIgnoreSerialization]
    public VolumeMode volMode = VolumeMode.COCKPIT;

    [JsonNetworkIgnoreSerialization] public float volume = 1.0f;

    /**
     * Used to determine if we should send an update to the server or not
     * We only need to do that if something that would stop us Receiving happens which
     * is frequencies and modulation
     */
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var compare = (DCSRadio)obj;

        if (!name.Equals(compare.name)) return false;
        if (!model.Equals(compare.model)) return false;
        if (!DCSPlayerRadioInfo.FreqCloseEnough(freq, compare.freq)) return false;
        if (modulation != compare.modulation) return false;
        if (enc != compare.enc) return false;
        if (encKey != compare.encKey) return false;
        if (retransmit != compare.retransmit) return false;
        if (!DCSPlayerRadioInfo.FreqCloseEnough(secFreq, compare.secFreq)) return false;
        if (IntercomUnitId != compare.IntercomUnitId) return false;
        //if (volume != compare.volume)
        //{
        //    return false;
        //}
        //if (freqMin != compare.freqMin)
        //{
        //    return false;
        //}
        //if (freqMax != compare.freqMax)
        //{
        //    return false;
        //}


        return true;
    }

    public override int GetHashCode() => HashCode.Combine(name, freq, modulation, enc, encKey, retransmit, secFreq, IntercomUnitId);

    public DCSRadio DeepClone()
    {
        //probably can use memberswise clone
        return new DCSRadio
        {
            channel = channel,
            enc = enc,
            encKey = encKey,
            encMode = encMode,
            expansion = expansion,
            freq = freq,
            freqMax = freqMax,
            freqMin = freqMin,
            freqMode = freqMode,
            guardFreqMode = guardFreqMode,
            modulation = modulation,
            secFreq = secFreq,
            name = name,
            model = model,
            simul = simul,
            volMode = volMode,
            volume = volume,
            retransmit = retransmit,
            rtMode = rtMode,
            rxOnly = rxOnly,
            IntercomUnitId = IntercomUnitId
        };
    }
}