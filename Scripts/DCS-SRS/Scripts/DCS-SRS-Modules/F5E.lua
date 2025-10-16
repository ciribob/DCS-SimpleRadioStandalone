function exportRadioF5E(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "AN/ARC-164"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].volume = SR.getRadioVolume(0, 309, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    local modulation = SR.getSelectorPosition(327, 0.1)

    --is HQ selected (A on the Radio)
    if modulation == 0 then
        _data.radios[2].modulation = 4
    else
        _data.radios[2].modulation = 0
    end

    -- get channel selector
    local _selector = SR.getSelectorPosition(307, 0.1)

    if _selector == 1 then
        _data.radios[2].channel = SR.getSelectorPosition(300, 0.05) + 1 --add 1 as channel 0 is channel 1
    end

    _data.selected = 1

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(311, 0.1)

    if uhfModeKnob == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    -- Check PTT - By Tarres!
    --NWS works as PTT when wheels up
    if (SR.getButtonPosition(135) > 0.5 or (SR.getButtonPosition(131) > 0.5 and SR.getButtonPosition(83) > 0.5)) then
        _data.ptt = true
    else
        _data.ptt = false
    end

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0; -- hotas radio

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(200,0.1)

    local iffIdent =  SR.getButtonPosition(207) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1)*100+SR.round(SR.getButtonPosition(210), 0.1)*10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000 + SR.round(SR.getButtonPosition(212), 0.1) * 1000 + SR.round(SR.getButtonPosition(213), 0.1)* 100 + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.1 then 
            _data.ambient = {vol = 0.3,  abType = 'f5' }
        else
            _data.ambient = {vol = 0.2,  abType = 'f5' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f5' }
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["F-5E-3"] = exportRadioF5E
    end,
}
return result
