using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;

public class DCSPlayerRadioInfo
{
    //HOTAS or IN COCKPIT controls
    public enum RadioSwitchControls
    {
        HOTAS = 0,
        IN_COCKPIT = 1
    }

    public enum SimultaneousTransmissionControl
    {
        ENABLED_INTERNAL_SRS_CONTROLS = 1,
        EXTERNAL_DCS_CONTROL = 0
    }

    [JsonIgnore]
    public static readonly uint
        UnitIdOffset = 100000000; // this is where non aircraft "Unit" Ids start from for satcom intercom

    public Ambient ambient = new()
    {
        vol = 0.0f,
        abType = ""
    };

    public DCSAircraftCapabilities capabilities = new();

    [JsonDCSIgnoreSerialization] public RadioSwitchControls control = RadioSwitchControls.HOTAS;

    public Transponder iff = new();

    [JsonDCSIgnoreSerialization] public bool inAircraft = false;

    [JsonDCSIgnoreSerialization] public bool intercomHotMic = false; //if true switch to intercom and transmit

    [JsonDCSIgnoreSerialization] public LatLngPosition latLng = new();

    [JsonDCSIgnoreSerialization] public string name = "";

    [JsonDCSIgnoreSerialization] public volatile bool ptt;

    public DCSRadio[] radios = new DCSRadio[Constants.MAX_RADIOS]; //10 + intercom

    [JsonDCSIgnoreSerialization] public int seat;

    public short selected;

    [JsonDCSIgnoreSerialization]
    public bool
        simultaneousTransmission; // Global toggle enabling simultaneous transmission on multiple radios, activated via the AWACS panel

    public SimultaneousTransmissionControl simultaneousTransmissionControl =
        SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL;

    public string unit = "";

    public uint unitId;

    public DCSPlayerRadioInfo()
    {
        for (var i = 0; i < Constants.MAX_RADIOS; i++) radios[i] = new DCSRadio();
    }

    [JsonIgnore] public long LastUpdate { get; set; }

    public void Reset()
    {
        name = "";
        latLng = new LatLngPosition();
        ambient = new Ambient
        {
            vol = 1.0f,
            //  pitch = 1.0f,
            abType = ""
        };
        ptt = false;
        selected = 0;
        unit = "";
        simultaneousTransmission = false;
        simultaneousTransmissionControl = SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL;
        LastUpdate = 0;
        seat = 0;
        for (var i = 0; i < Constants.MAX_RADIOS; i++) radios[i] = new DCSRadio();
    }

    // override object.Equals
    public override bool Equals(object compare)
    {
        try
        {
            if (compare == null || GetType() != compare.GetType()) return false;

            var compareRadio = compare as DCSPlayerRadioInfo;

            if (control != compareRadio.control) return false;
            //if (side != compareRadio.side)
            //{
            //    return false;
            //}
            if (!name.Equals(compareRadio.name)) return false;
            if (!unit.Equals(compareRadio.unit)) return false;

            if (unitId != compareRadio.unitId) return false;

            if (inAircraft != compareRadio.inAircraft) return false;

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

    public override int GetHashCode() => HashCode.Combine(control, name, unit, unitId, inAircraft, iff, ambient, radios);


    /*
     * Was Radio updated in the last 10 Seconds
     */

    public bool IsCurrent()
    {
        return LastUpdate > DateTime.Now.Ticks - 100000000;
    }

    //comparing doubles is risky - check that we're close enough to hear (within 100hz)
    public static bool FreqCloseEnough(double freq1, double freq2)
    {
        var diff = Math.Abs(freq1 - freq2);

        return diff < 500;
    }

    public DCSPlayerRadioInfo DeepClone()
    {
        var clone = (DCSPlayerRadioInfo)MemberwiseClone();

        clone.iff = iff.Copy();
        clone.ambient = ambient.Copy();
        //ignore position
        clone.radios = new DCSRadio[Constants.MAX_RADIOS];

        for (var i = 0; i < radios.Length; i++) clone.radios[i] = radios[i].DeepClone();

        return clone;
    }

    public PlayerRadioInfoBase ConvertToRadioBase()
    {
        var radiosBase = new RadioBase[Constants.MAX_RADIOS];
        for (var i = 0; i < radiosBase.Length; i++)
        {
            var radio = radios[i];
            radiosBase[i] = new RadioBase
            {
                enc = radio.enc,
                encKey = radio.encKey,
                freq = radio.freq,
                modulation = radio.modulation,
                retransmit = radio.retransmit,
                secFreq = radio.secFreq,
                Model = radio.model,
                IntercomUnitId = radio.IntercomUnitId
            };
        }

        return new PlayerRadioInfoBase
        {
            ambient = ambient?.Copy(),
            iff = iff?.Copy(),
            unit = unit,
            unitId = unitId,
            radios = radiosBase
        };
    }


    //TODO merge this so we dont have two versions of CanHearTransmission!
    public DCSRadio CanHearTransmission(double frequency,
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

        DCSRadio bestMatchingRadio = null;
        RadioReceivingState bestMatchingRadioState = null;
        var bestMatchingDecryptable = false;

        for (var i = 0; i < radios.Length; i++)
        {
            var receivingRadio = radios[i];

            if (receivingRadio != null)
            {
                
                if (modulation == Modulation.DISABLED
                    || receivingRadio.modulation == Modulation.DISABLED)
                    continue;
                
                //handle INTERCOM Modulation is 2
                if (receivingRadio.modulation == Modulation.INTERCOM &&
                    modulation == Modulation.INTERCOM)
                {
                    if ((unitId > 0 && sendingUnitId > 0
                                   && unitId == sendingUnitId)
                            || (sendingUnitId > 0 && receivingRadio.IntercomUnitId == sendingUnitId))
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

                    // decryptable = false;
                    // receivingState = null;
                    // return null;
                    continue;
                }


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