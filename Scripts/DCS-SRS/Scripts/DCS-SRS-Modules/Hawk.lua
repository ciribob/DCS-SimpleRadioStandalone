function exportRadioHawk(_data, SR)

    local MHZ = 1000000

    _data.radios[2].name = "AN/ARC-164 UHF"

    local _selector = SR.getSelectorPosition(221, 0.25)

    if _selector == 1 or _selector == 2 then

        local _hundreds = SR.getSelectorPosition(226, 0.25) * 100 * MHZ
        local _tens = SR.round(SR.getKnobPosition(0, 227, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 10 * MHZ
        local _ones = SR.round(SR.getKnobPosition(0, 228, { 0.0, 0.9 }, { 0, 9 }), 0.1) * MHZ
        local _tenth = SR.round(SR.getKnobPosition(0, 229, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 100000
        local _hundreth = SR.round(SR.getKnobPosition(0, 230, { 0.0, 0.3 }, { 0, 3 }), 0.1) * 10000

        _data.radios[2].freq = _hundreds + _tens + _ones + _tenth + _hundreth
    else
        _data.radios[2].freq = 1
    end
    _data.radios[2].modulation = 0
    _data.radios[2].volume = 1
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    _data.radios[3].name = "ARI 23259/1"
    _data.radios[3].freq = SR.getRadioFrequency(7)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = 1

    --guard mode for UHF Radio
    local _uhfKnob = SR.getSelectorPosition(221, 0.25)
    if _uhfKnob == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    --- is VHF ON?
    if SR.getSelectorPosition(391, 0.2) == 0 then
        _data.radios[3].freq = 1
    end
    --guard mode for VHF Radio
    local _vhfKnob = SR.getSelectorPosition(391, 0.2)
    if _vhfKnob == 2 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 121.5 * 1000000
    end

    -- Radio Select Switch
    if (SR.getButtonPosition(265)) > 0.5 then
        _data.selected = 2
    else
        _data.selected = 1
    end

    _data.control = 1; -- full radio

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
        _data.ambient = {vol = 0.15,  abType = 'jet' }
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'jet' }
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["Hawk"] = exportRadioHawk
    end,
}
return result
