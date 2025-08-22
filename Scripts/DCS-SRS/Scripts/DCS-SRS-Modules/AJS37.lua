function exportRadioAJS37(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FR 22"
    _data.radios[2].freq = SR.getRadioFrequency(30)
    _data.radios[2].modulation = SR.getRadioModulation(30)
    _data.radios[2].volume =  SR.getRadioVolume(0, 385,{0.0, 1.0},false)
    _data.radios[2].volMode = 0

    _data.radios[3].name = "FR 24"
    _data.radios[3].freq = SR.getRadioFrequency(31)
    _data.radios[3].modulation = SR.getRadioModulation(31)
    _data.radios[3].volume = 1.0
    _data.radios[3].volMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(10)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'ajs37' }
        else
            _data.ambient = {vol = 0.2,  abType = 'ajs37' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'ajs37' }
    end

    return _data
end

local result = { }

function result.register(SR)
  SR.exporters["AJS37"] = exportRadioAJS37
end

return result
