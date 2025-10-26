function exportRadioMI24P(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "Use Radio/ICS Switch to control Intercom Hot Mic" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "R-863"
    _data.radios[2].freq = SR.getRadioFrequency(49)
    _data.radios[2].modulation = SR.getRadioModulation(49)
    _data.radios[2].volume = SR.getRadioVolume(0, 511, { 0.0, 1.0 }, false)
    _data.radios[2].volMode = 0
    _data.radios[2].model = SR.RadioModels.R_863

    local guard = SR.getSelectorPosition(507, 1)
    if guard == 1 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 121.5 * 1000000
    end


    _data.radios[3].name = "R-828"
    _data.radios[3].freq = SR.getRadioFrequency(51)
    _data.radios[3].modulation = 1 --SR.getRadioModulation(50)
    _data.radios[3].volume = SR.getRadioVolume(0, 339, { 0.0, 1.0 }, false)
    _data.radios[3].volMode = 0
    _data.radios[3].model = SR.RadioModels.R_828

    _data.radios[4].name = "JADRO-1I"
    _data.radios[4].freq = SR.getRadioFrequency(50, 500)
    _data.radios[4].modulation = SR.getRadioModulation(50)
    _data.radios[4].volume = SR.getRadioVolume(0, 426, { 0.0, 1.0 }, false)
    _data.radios[4].volMode = 0
    _data.radios[4].model = SR.RadioModels.JADRO_1A

    -- listen only radio - moved to expansion
    _data.radios[5].name = "R-852"
    _data.radios[5].freq = SR.getRadioFrequency(52)
    _data.radios[5].modulation = SR.getRadioModulation(52)
    _data.radios[5].volume = SR.getRadioVolume(0, 517, { 0.0, 1.0 }, false)
    _data.radios[5].volMode = 0
    _data.radios[5].expansion = true
    _data.radios[5].model = SR.RadioModels.R_852

    -- TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    if _seat == 0 then

         _data.radios[1].volume = SR.getRadioVolume(0, 457, { 0.0, 1.0 }, false)

        --Pilot SPU-8 selection
        local _switch = SR.getSelectorPosition(455, 0.2)
        if _switch == 0 then
            _data.selected = 1            -- R-863
        elseif _switch == 1 then 
            _data.selected = -1          -- No Function
        elseif _switch == 2 then
            _data.selected = 2            -- R-828
        elseif _switch == 3 then
            _data.selected = 3            -- JADRO
        elseif _switch == 4 then
            _data.selected = 4
        else
            _data.selected = -1
        end

        local _pilotPTT = SR.getButtonPosition(738) 
        if _pilotPTT >= 0.1 then

            if _pilotPTT == 0.5 then
                -- intercom
              _data.selected = 0
            end

            _data.ptt = true
        end

        --hot mic 
        if SR.getButtonPosition(456) >= 1.0 then
            _data.intercomHotMic = true
        end

    else

        --- copilot
        _data.radios[1].volume = SR.getRadioVolume(0, 661, { 0.0, 1.0 }, false)
        -- For the co-pilot allow volume control
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1
        _data.radios[5].volMode = 1
        
        local _switch = SR.getSelectorPosition(659, 0.2)
        if _switch == 0 then
            _data.selected = 1            -- R-863
        elseif _switch == 1 then 
            _data.selected = -1          -- No Function
        elseif _switch == 2 then
            _data.selected = 2            -- R-828
        elseif _switch == 3 then
            _data.selected = 3            -- JADRO
        elseif _switch == 4 then
            _data.selected = 4
        else
            _data.selected = -1
        end

        local _copilotPTT = SR.getButtonPosition(856) 
        if _copilotPTT >= 0.1 then

            if _copilotPTT == 0.5 then
                -- intercom
              _data.selected = 0
            end

            _data.ptt = true
        end

        --hot mic 
        if SR.getButtonPosition(660) >= 1.0 then
            _data.intercomHotMic = true
        end
    end
    
    _data.control = 1;

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(9)
        local _doorRight = SR.getButtonPosition(849)

        if _doorLeft > 0.2 or _doorRight > 0.2 then 
            _data.ambient = {vol = 0.35,  abType = 'mi24' }
        else
            _data.ambient = {vol = 0.15,  abType = 'mi24' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'mi24' }
    end

    return _data

end

local result = {
    register = function(SR)
        SR.exporters["Mi-24P"] = exportRadioMI24P
    end,
}
return result
