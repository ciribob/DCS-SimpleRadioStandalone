function exportRadioOH6A(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }


    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0

    _data.radios[2].name = "AN/ARC-54 VHF FM"
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = SR.getRadioModulation(15)
    _data.radios[2].volMode = 0

    _data.radios[3].name = "AN/ARC-51 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(14)
    _data.radios[3].modulation = SR.getRadioModulation(14)
    _data.radios[3].volMode = 0


    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()
    local _hotMic = 0
    local _selector = 0
    
    if _seat == 0 then

        if SR.getButtonPosition(344) > 0.5 then
            _data.radios[1].volume = SR.getRadioVolume(0, 346, { -1.0, 1.0 }, false)
        else
            _data.radios[1].volume = 0
        end
      

        if SR.getButtonPosition(340) > 0.5 then
            _data.radios[2].volume = SR.getRadioVolume(0, 346, { -1.0, 1.0 }, false) * SR.getRadioVolume(0, 51, { 0.0, 1.0 }, false)
        else
            _data.radios[2].volume = 0
        end

        if SR.getButtonPosition(341) > 0.5 then
            _data.radios[3].volume = SR.getRadioVolume(0, 346, { -1.0, 1.0 }, false) *SR.getRadioVolume(0, 57, { 0.0, 1.0 }, false)
        else
            _data.radios[3].volume = 0
        end

        _selector = SR.getSelectorPosition(347, 0.165)

    else

        if SR.getButtonPosition(352) > 0.5 then
            _data.radios[1].volume = SR.getRadioVolume(0, 354, { -1.0, 1.0 }, false)
        else
            _data.radios[1].volume = 0
        end

        if SR.getButtonPosition(348) > 0.5 then
            _data.radios[2].volume = SR.getRadioVolume(0, 354, { -1.0, 1.0 }, false) * SR.getRadioVolume(0, 51, { 0.0, 1.0 }, false)
        else
            _data.radios[2].volume = 0
        end

       if SR.getButtonPosition(349) > 0.5 then
            _data.radios[3].volume = SR.getRadioVolume(0, 354, { -1.0, 1.0 }, false) *SR.getRadioVolume(0, 57, { 0.0, 1.0 }, false)
        else
            _data.radios[3].volume = 0
        end
      
        -- _hotMic = SR.getSelectorPosition(186, 0.1)
         _selector = SR.getSelectorPosition(355, 0.165)

    end

    if _selector == 2 then
        _data.selected = 0
    elseif _selector == 3 then
        _data.selected = 1
    elseif _selector == 4 then
        _data.selected = 2
    else
        _data.selected = -1
    end

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(56, 0.33)
    if uhfModeKnob == 2 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    --guard mode for UHF Radio
    local retran = SR.getSelectorPosition(52, 0.33)

    if retran == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].rtMode = 0
        _data.radios[2].retransmit = true

        _data.radios[3].rtMode = 0
        _data.radios[3].retransmit = true
    end


    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.35,  abType = 'oh6a' }
        else
            _data.ambient = {vol = 0.2,  abType = 'oh6a' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'oh6a' }
    end

    _data.control = 1

    return _data
end


local result = { }

function result.register(SR)
  SR.exporters["OH-6A"] = exportRadioOH6A
end

return result
