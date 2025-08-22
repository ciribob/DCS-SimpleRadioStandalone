function exportRadioVSNF4(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
   
    _data.radios[2].name = "AN/ARC-164 UHF"
    _data.radios[2].freq = SR.getRadioFrequency(2)
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 0
    _data.radios[2].volume = 1.0
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 0


    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].expansion = true
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    _data.radios[2].encKey = 1
    _data.radios[2].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
        _data.ambient = {vol = 0.2,  abType = 'jet' }
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'jet' }
    end

    return _data
end

local result = {
   register = function(SR)
    SR.exporters["VSN_F4C"] = exportRadioVSNF4
    SR.exporters["VSN_F4B"] = exportRadioVSNF4
  end
}
return result