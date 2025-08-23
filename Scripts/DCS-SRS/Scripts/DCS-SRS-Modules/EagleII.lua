function exportRadioEagleII(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1 --SR.getRadioVolume(0, 288,{0.0,0.8},false)

    _data.radios[2].name = "KY-197A"
    _data.radios[2].freq = SR.getRadioFrequency(5)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 364, { 0.1, 1.0 }, false)

    if _data.radios[2].volume < 0 then
        _data.radios[2].volume = 0
    end

    -- Intercom button depressed
    -- if(SR.getButtonPosition(133) > 0.5 or SR.getButtonPosition(546) > 0.5) then
    --     _data.selected = 0
    --     _data.ptt = true
    -- elseif (SR.getButtonPosition(134) > 0.5 or SR.getButtonPosition(547) > 0.5) then
    --     _data.selected= 1
    --     _data.ptt = true
    -- else
    --     _data.selected= 1
    --      _data.ptt = false
    -- end

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

    _data.control = 0 -- HOTAS

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "ChristenEagle" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ChristenEagle" }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["Christen Eagle II"] = exportRadioEagleII
    end,
}
return result
