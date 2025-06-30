using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

public class PlayerRadioInfoBase
{
    public Ambient ambient = new()
    {
        vol = 0.0f,
        abType = ""
    };

    public Transponder iff = new();

    public RadioBase[] radios = new RadioBase[Constants.MAX_RADIOS]; //10 + intercom

    public string unit = "";

    public uint unitId;


    public PlayerRadioInfoBase()
    {
        for (var i = 0; i < radios.Length; i++) radios[i] = new RadioBase();
    }

    [JsonIgnore] public long LastUpdate { get; set; }


    public void Reset()
    {
        ambient = new Ambient
        {
            vol = 1.0f,
            abType = ""
        };
        unit = "";
        for (var i = 0; i < radios.Length; i++) radios[i] = new RadioBase();
    }

    // override object.Equals
    public override bool Equals(object compare)
    {
        try
        {
            if (compare == null || GetType() != compare.GetType()) return false;

            var compareRadio = compare as PlayerRadioInfoBase;

            if (!unit.Equals(compareRadio.unit)) return false;

            if (unitId != compareRadio.unitId) return false;

            if (iff == null || compareRadio.iff == null) return false;

            //check iff
            if (!iff.Equals(compareRadio.iff)) return false;

            if (ambient == null || compareRadio.ambient == null) return false;

            //check ambient
            if (!ambient.Equals(compareRadio.ambient)) return false;

            for (var i = 0; i < radios.Length; i++)
            {
                var radio1 = radios[i];
                var radio2 = compareRadio.radios[i];

                if (radio1 != null && radio2 != null)
                    if (!radio1.Equals(radio2))
                        return false;
            }
        }
        catch
        {
            return false;
        }


        return true;
    }


    public PlayerRadioInfoBase DeepClone()
    {
        var clone = (PlayerRadioInfoBase)MemberwiseClone();

        clone.iff = iff?.Copy();
        clone.ambient = ambient?.Copy();
        //ignore position

        clone.radios = new RadioBase[Constants.MAX_RADIOS];

        for (var i = 0; i < clone.radios.Length; i++) clone.radios[i] = radios[i].DeepClone();

        return clone;
    }

    //TODO merge this so we dont have two versions of can hear transmission
    //should be in a helper class and static
    public RadioBase CanHearTransmission(double frequency,
        Modulation modulation,
        byte encryptionKey,
        bool strictEncryption,
        uint sendingUnitId,
        List<int> blockedRadios,
        out RadioReceivingState receivingState,
        out bool decryptable)
    {
        //    if (!IsCurrent())
        //     {
        //         receivingState = null;
        //        decryptable = false;
        //       return null;
        //   }

        RadioBase bestMatchingRadio = null;
        RadioReceivingState bestMatchingRadioState = null;
        var bestMatchingDecryptable = false;

        for (var i = 0; i < radios.Length; i++)
        {
            var receivingRadio = radios[i];

            if (receivingRadio != null)
            {
                //handle INTERCOM Modulation is 2
                if (receivingRadio.modulation == Modulation.INTERCOM &&
                    modulation == Modulation.INTERCOM)
                {
                    if (unitId > 0 && sendingUnitId > 0
                                   && unitId == sendingUnitId)
                    {
                        receivingState = new RadioReceivingState
                        {
                            IsSecondary = false,
                            LastReceivedAt = DateTime.Now.Ticks,
                            ReceivedOn = i
                        };
                        decryptable = true;
                        return receivingRadio;
                    }

                    decryptable = false;
                    receivingState = null;
                    return null;
                }

                if (modulation == Modulation.DISABLED
                    || receivingRadio.modulation == Modulation.DISABLED)
                    continue;

                //within 1khz
                if (RadioBase.FreqCloseEnough(receivingRadio.freq, frequency)
                    && receivingRadio.modulation == modulation
                    && receivingRadio.freq > 10000)
                {
                    var isDecryptable = (receivingRadio.enc ? receivingRadio.encKey : 0) == encryptionKey ||
                                        (!strictEncryption && encryptionKey == 0);

                    if (isDecryptable && !blockedRadios.Contains(i))
                    {
                        receivingState = new RadioReceivingState
                        {
                            IsSecondary = false,
                            LastReceivedAt = DateTime.Now.Ticks,
                            ReceivedOn = i
                        };
                        decryptable = true;
                        return receivingRadio;
                    }

                    bestMatchingRadio = receivingRadio;
                    bestMatchingRadioState = new RadioReceivingState
                    {
                        IsSecondary = false,
                        LastReceivedAt = DateTime.Now.Ticks,
                        ReceivedOn = i
                    };
                    bestMatchingDecryptable = isDecryptable;
                }

                //within 1khz
                if (RadioBase.FreqCloseEnough(receivingRadio.secFreq, frequency)
                    && receivingRadio.secFreq > 10000)
                {
                    if ((receivingRadio.enc ? receivingRadio.encKey : 0) == encryptionKey ||
                        (!strictEncryption && encryptionKey == 0))
                    {
                        receivingState = new RadioReceivingState
                        {
                            IsSecondary = true,
                            LastReceivedAt = DateTime.Now.Ticks,
                            ReceivedOn = i
                        };
                        decryptable = true;
                        return receivingRadio;
                    }

                    bestMatchingRadio = receivingRadio;
                    bestMatchingRadioState = new RadioReceivingState
                    {
                        IsSecondary = true,
                        LastReceivedAt = DateTime.Now.Ticks,
                        ReceivedOn = i
                    };
                }
            }
        }

        decryptable = bestMatchingDecryptable;
        receivingState = bestMatchingRadioState;
        return bestMatchingRadio;
    }
}