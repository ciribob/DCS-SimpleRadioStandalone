function exportRadioLa7(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "RSIU-4M"
    _data.radios[2].freq = SR.getRadioFrequency(7)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 225.000e6
    _data.radios[2].freqMax = 399.975e6
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    _data.selected = 1

    if SR.getButtonPosition(26) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = true
    _data.radios[3].model = SR.RadioModels.AN_ARC186

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0

    if SR.getAmbientVolumeEngine() > 10 then
        local _door = SR.getButtonPosition(77)
        if _door > 0.1 then
            _data.ambient = {vol = 0.35, abType = 'la7'}
        else
            _data.ambient = {vol = 0.15, abType = 'la7'}
        end
    else
        _data.ambient = {vol = 0, abType = 'la7'}
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["La-7"] = exportRadioLa7
    end,
}
return result
