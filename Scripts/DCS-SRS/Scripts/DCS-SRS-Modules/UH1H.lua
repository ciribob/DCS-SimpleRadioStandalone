function exportRadioUH1H(_data, SR)

    local intercomOn =  SR.getButtonPosition(27)
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume =  SR.getRadioVolume(0, 29, { 0.3, 1.0 }, true)

    if intercomOn > 0.5 then
        --- control hot mic instead of turning it on and off
        _data.intercomHotMic = true
    end

    local fmOn =  SR.getButtonPosition(23)
    _data.radios[2].name = "AN/ARC-131"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].modulation = 1
    _data.radios[2].volume = SR.getRadioVolume(0, 37, { 0.3, 1.0 }, true)

    if fmOn < 0.5 then
        _data.radios[2].freq = 1
    end

    local uhfOn =  SR.getButtonPosition(24)
    _data.radios[3].name = "AN/ARC-51BX - UHF"
    _data.radios[3].freq = SR.getRadioFrequency(22)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 21, { 0.0, 1.0 }, true)

    -- get channel selector
    local _selector = SR.getSelectorPosition(15, 0.1)

    if _selector < 1 then
        _data.radios[3].channel = SR.getSelectorPosition(16, 0.05) + 1 --add 1 as channel 0 is channel 1
    end

    if uhfOn < 0.5 then
        _data.radios[3].freq = 1
        _data.radios[3].channel = -1
    end

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(17, 0.1)
    if uhfModeKnob == 2 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    local vhfOn =  SR.getButtonPosition(25)
    _data.radios[4].name = "AN/ARC-134"
    _data.radios[4].freq = SR.getRadioFrequency(20)
    _data.radios[4].modulation = 0
    _data.radios[4].volume = SR.getRadioVolume(0, 9, { 0.0, 0.60 }, false)

    if vhfOn < 0.5 then
        _data.radios[4].freq = 1
    end

    --_device:get_argument_value(_arg)

    -- TODO check it works
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    if _seat == 0 then

         local _panel = GetDevice(0)

        local switch = _panel:get_argument_value(30)

        if SR.nearlyEqual(switch, 0.1, 0.03) then
            _data.selected = 0
        elseif SR.nearlyEqual(switch, 0.2, 0.03) then
            _data.selected = 1
        elseif SR.nearlyEqual(switch, 0.3, 0.03) then
            _data.selected = 2
        elseif SR.nearlyEqual(switch, 0.4, 0.03) then
            _data.selected = 3
        else
            _data.selected = -1
        end

        local _pilotPTT = SR.getButtonPosition(194)
        if _pilotPTT >= 0.1 then

            if _pilotPTT == 0.5 then
                -- intercom
                _data.selected = 0
            end

            _data.ptt = true
        end

        _data.control = 1; -- Full Radio


        _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "Hot mic on INT switch" }
    else
        _data.control = 0; -- no copilot or gunner radio controls - allow them to switch
        
        _data.radios[1].volMode = 1 
        _data.radios[2].volMode = 1 
        _data.radios[3].volMode = 1 
        _data.radios[4].volMode = 1

        _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = true, desc = "Hot mic on INT switch" }
    end


    -- HANDLE TRANSPONDER
    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}


    local iffPower =  SR.getSelectorPosition(59,0.1)

    local iffIdent =  SR.getButtonPosition(66) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- MODE set to MIC
        if iffIdent == -1 then

            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT due to MIC switch
            end
        end

    end

    local mode1On =  SR.getButtonPosition(61)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(68,0.33), 0.1)*10+SR.round(SR.getSelectorPosition(69,0.11), 0.1)


    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(63)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(70,0.11), 0.1) * 1000 + SR.round(SR.getSelectorPosition(71,0.11), 0.1) * 100 + SR.round(SR.getSelectorPosition(72,0.11), 0.1)* 10 + SR.round(SR.getSelectorPosition(73,0.11), 0.1)

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(67)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    local _doorLeft = SR.getButtonPosition(420)

    -- engine on
    if SR.getAmbientVolumeEngine()  > 10 then
        if _doorLeft >= 0 and _doorLeft < 0.5 then
            -- engine on and door closed
            _data.ambient = {vol = 0.2,  abType = 'uh1' }
        else
            -- engine on and door open
            _data.ambient = {vol = 0.35, abType = 'uh1' }
        end
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'uh1' }
    end


    -- SR.log("ambient STATUS"..SR.JSON:encode(_data.ambient).."\n\n")

    return _data

end

local result = {
   register = function(SR)
  SR.exporters["UH-1H"] = exportRadioUH1H
  end
}
return result
