function exportRadioFW190(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FuG 16ZY"
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = 0

    local _volRaw = GetDevice(0):get_argument_value(83)
    if _volRaw >= 0 and _volRaw <= 0.25 then
        _data.radios[2].volume = (1.0 - SR.getRadioVolume(0, 83,{0,0.5},true)) + 0.5 -- Volume knob is not behaving..
    else
        _data.radios[2].volume = ((1.0 - SR.getRadioVolume(0, 83,{0,0.5},true)) - 0.5) * -1.0 -- ABS
    end

    _data.selected = 1


    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = true

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0; -- hotas radio

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(194)

        if _door > 0.1 then 
            _data.ambient = {vol = 0.35,  abType = 'fw190' }
        else
            _data.ambient = {vol = 0.2,  abType = 'fw190' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'fw190' }
    end

    return _data;
end

local result = {
   register = function(SR)
  SR.exporters["FW-190D9"] = exportRadioFW190
  SR.exporters["FW-190A8"] = exportRadioFW190
  end
}
return result