namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;


using System;

//Pretty much identical to DCSRadio.cs in the client - but i dont want to have to copy all the other DCS specific dependancies to common
//any changes to DCSRadio.cs must also be here
public class DCSRadioCustom
{
    public int channel = -1;

    public bool enc; // encryption enabled
    public byte encKey;

    public int encMode = 0;
    public bool expansion;

    public double freq = 1;

    public double freqMax = 1;

    public double freqMin = 1;

    public int freqMode = 0;
    
    public int guardFreqMode = 0;

    public Modulation modulation = Modulation.DISABLED;

    public string name = "";

    // Radio model (arc210, link16, r812, etc).
    public string model = "";
    
    public int rtMode = 2;

    public bool rxOnly;

    public double secFreq = 1;
    
    public int volMode = 0;

    public bool simul;
}