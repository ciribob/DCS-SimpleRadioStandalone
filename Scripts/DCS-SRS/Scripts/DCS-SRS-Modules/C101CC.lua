function exportRadioC101CC(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "The hot mic talk button (labeled TALK in cockpit) must be pulled out" }

    -- TODO - figure out channels.... it saves state??
    -- figure out volume
    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = SR.getRadioVolume(0, 403, { 0.0, 1.0 }, false)
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "V/TVU-740"
    _data.radios[2].freq = SR.getRadioFrequency(11)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = 1.0--SR.getRadioVolume(0, 234,{0.0,1.0},false)
    _data.radios[2].volMode = 1

    local _channel = SR.getButtonPosition(231)

    -- SR.log("Channel SELECTOR: ".. SR.getButtonPosition(231).."\n")


    local uhfModeKnob = SR.getSelectorPosition(232, 0.1)
    if uhfModeKnob == 2 and _data.radios[2].freq > 1000 then
        -- Function dial set to BOTH
        -- Listen to Guard as well as designated frequency
        _data.radios[2].secFreq = 243.0 * 1000000
    else
        -- Function dial set to OFF, MAIN, or ADF
        -- Not listening to Guard secondarily
        _data.radios[2].secFreq = 0
    end

    _data.radios[3].name = "20B VHF"
    _data.radios[3].modulation = 0
    _data.radios[3].volume = 1.0 --SR.getRadioVolume(0, 412,{0.0,1.0},false)
    _data.radios[3].volMode = 1

    --local _vhfPower = SR.getSelectorPosition(413,1.0)
    --
    --if _vhfPower == 1 then
    _data.radios[3].freq = SR.getRadioFrequency(10)
    --else
    --    _data.radios[3].freq = 1
    --end
    --
    local _seat = GetDevice(0):get_current_seat()
    local _selector

    if _seat == 0 then
        _selector = SR.getSelectorPosition(404, 0.05)
    else
        _selector = SR.getSelectorPosition(947, 0.05)
    end

    if _selector == 0 then
        _data.selected = 0
    elseif _selector == 2 then
        _data.selected = 2
    elseif _selector == 12 then
        _data.selected = 1
    else
        _data.selected = -1
    end

    --TODO figure our which cockpit you're in? So we can have controls working in the rear?

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(347,0.25)

    --SR.log("IFF iffPower"..iffPower.."\n\n")

    local iffIdent =  SR.getButtonPosition(361) -- -1 is off 0 or more is on

    if iffPower <= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 1

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(349)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(355), 0.1)*100+SR.round(SR.getButtonPosition(356), 0.1)*10

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(351)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(357), 0.1) * 10000 + SR.round(SR.getButtonPosition(358), 0.1) * 1000 + SR.round(SR.getButtonPosition(359), 0.1)* 100 + SR.round(SR.getButtonPosition(360), 0.1) * 10

    if mode3On == 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(354)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    _data.control = 1;


    local frontHotMic =  SR.getButtonPosition(287)
    local rearHotMic =   SR.getButtonPosition(891)
    -- only if The hot mic talk button (labeled TALK in cockpit) is up
    if frontHotMic == 1 or rearHotMic == 1 then
       _data.intercomHotMic = true
    end

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(1)
        local _doorRight = SR.getButtonPosition(301)

        if _doorLeft > 0.7 or _doorRight > 0.7 then 
            _data.ambient = {vol = 0.3,  abType = 'c101' }
        else
            _data.ambient = {vol = 0.15,  abType = 'c101' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'c101' }
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["C-101CC"] = exportRadioC101CC
    end,
}
return result
