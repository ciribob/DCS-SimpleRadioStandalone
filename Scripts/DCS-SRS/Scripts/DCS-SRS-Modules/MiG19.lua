function exportRadioMIG19(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "RSIU-4V"
    _data.radios[2].freq = SR.getRadioFrequency(17)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 327, { 0.0, 1.0 }, false)

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

    _data.control = 0; -- Hotas Controls radio

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(433)

        if _door > 0.1 then 
            _data.ambient = {vol = 0.3,  abType = 'mig19' }
        else
            _data.ambient = {vol = 0.2,  abType = 'mig19' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'mig19' }
    end

    return _data;
end

local result = {
   register = function(SR)
  SR.exporters["MiG-19P"] = exportRadioMIG19
  end
}
return result