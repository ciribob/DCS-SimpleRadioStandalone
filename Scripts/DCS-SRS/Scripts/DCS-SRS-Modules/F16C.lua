local _f16 = {}
_f16.radio1 = {}
_f16.radio1.guard = 0

function exportRadioF16C(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    -- UHF
    _data.radios[2].name = "AN/ARC-164"
    _data.radios[2].freq = SR.getRadioFrequency(36)
    _data.radios[2].modulation = SR.getRadioModulation(36)
    _data.radios[2].volume = SR.getRadioVolume(0, 430, { 0.0, 1.0 }, false)
    _data.radios[2].encMode = 2
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    -- C&I Backup/UFC by Raffson, aka Stoner
    local _cni = SR.getButtonPosition(542)
    if _cni == 0 then
        local _buhf_func = SR.getSelectorPosition(417, 0.1)
        if _buhf_func == 2 then
            -- Function set to BOTH --> also listen to guard
            _data.radios[2].secFreq = 243.0 * 1000000
        else
            _data.radios[2].secFreq = 0
        end

        -- Check UHF frequency mode (0 = MNL, 1 = PRESET, 2 = GRD)
        local _selector = SR.getSelectorPosition(416, 0.1)
        if _selector == 1 then
            -- Using UHF preset channels
            local _channel = SR.getSelectorPosition(410, 0.05) + 1 --add 1 as channel 0 is channel 1
            _data.radios[2].channel = _channel
        end
    else
        -- Parse the UFC - LOOK FOR BOTH (OR MAIN)
        local ded = SR.getListIndicatorValue(6)
        --PANEL 6{"Active Frequency or Channel":"305.00","Asterisks on Scratchpad_lhs":"*","Asterisks on Scratchpad_rhs":"*","Bandwidth":"NB","Bandwidth_placeholder":"","COM 1 Mode":"UHF","Preset Frequency":"305.00","Preset Frequency_placeholder":"","Preset Label":"PRE     a","Preset Number":" 1","Preset Number_placeholder":"","Receiver Mode":"BOTH","Scratchpad":"305.00","Scratchpad_placeholder":"","TOD Label":"TOD"}
        
        if ded and ded["Receiver Mode"] ~= nil and  ded["COM 1 Mode"] == "UHF" then
            if ded["Receiver Mode"] == "BOTH" then
                _f16.radio1.guard= 243.0 * 1000000
            else
                _f16.radio1.guard= 0
            end
        else
            if _data.radios[2].freq < 1000 then
                _f16.radio1.guard= 0
            end
        end

        _data.radios[2].secFreq = _f16.radio1.guard
            
     end

    -- VHF
    _data.radios[3].name = "AN/ARC-222"
    _data.radios[3].freq = SR.getRadioFrequency(38)
    _data.radios[3].modulation = SR.getRadioModulation(38)
    _data.radios[3].volume = SR.getRadioVolume(0, 431, { 0.0, 1.0 }, false)
    _data.radios[3].encMode = 2
    _data.radios[3].guardFreqMode = 1
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].model = SR.RadioModels.AN_ARC222

    -- KY-58 Radio Encryption
    local _ky58Power = SR.round(SR.getButtonPosition(707), 0.1)

    if _ky58Power == 0.5 and SR.round(SR.getButtonPosition(705), 0.1) == 0.1 then
        -- mode switch set to C and powered on
        -- Power on and correct mode selected
        -- Get encryption key
        local _channel = SR.getSelectorPosition(706, 0.1)

        local _cipherSwitch = SR.round(SR.getButtonPosition(701), 1)
        local _radio = nil
        if _cipherSwitch > 0.5 then
            -- CRAD1 (UHF)
            _radio = _data.radios[2]
        elseif _cipherSwitch < -0.5 then
            -- CRAD2 (VHF)
            _radio = _data.radios[3]
        end
        if _radio ~= nil and _channel > 0 and _channel < 7 then
            _radio.encKey = _channel
            _radio.enc = true
            _radio.volume = SR.getRadioVolume(0, 708, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 432, { 0.0, 1.0 }, false)--User KY-58 volume if chiper is used
        end
    end

    local _cipherOnly =  SR.round(SR.getButtonPosition(443),1) < -0.5 --If HOT MIC CIPHER Switch, HOT MIC / OFF / CIPHER set to CIPHER, allow only cipher
    if _cipherOnly and _data.radios[3].enc ~=true then
        _data.radios[3].freq = 0
    end
    if _cipherOnly and _data.radios[2].enc ~=true then
        _data.radios[2].freq = 0
    end

    _data.control = 0; -- SRS Hotas Controls

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(539,0.1)

    local iffIdent =  SR.getButtonPosition(125) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

    end

    local modeSelector =  SR.getButtonPosition(553)

    if modeSelector == -1 then

        --shares a dial with the mode 3, limit number to max 3
        local _secondDigit = SR.round(SR.getButtonPosition(548), 0.1)*10

        if _secondDigit > 3 then
            _secondDigit = 3
        end

        _data.iff.mode1 = SR.round(SR.getButtonPosition(546), 0.1)*100 + _secondDigit
    else
        _data.iff.mode1 = -1
    end

    if modeSelector ~= 0 then
        _data.iff.mode3 = SR.round(SR.getButtonPosition(546), 0.1) * 10000 + SR.round(SR.getButtonPosition(548), 0.1) * 1000 + SR.round(SR.getButtonPosition(550), 0.1)* 100 + SR.round(SR.getButtonPosition(552), 0.1) * 10
    else
        _data.iff.mode3 = -1
    end

    if iffPower == 4 and modeSelector ~= 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(541)

    local mode4Code = SR.getButtonPosition(543)

    if mode4On == 0 and mode4Code ~= -1 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(7)

        if _door > 0.1 then 
            _data.ambient = {vol = 0.3,  abType = 'f16' }
        else
            _data.ambient = {vol = 0.15,  abType = 'f16' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f16' }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["F-16C_50"] = exportRadioF16C
        SR.exporters["F-16D_50_NS"] = exportRadioF16C
        SR.exporters["F-16D_52_NS"] = exportRadioF16C
        SR.exporters["F-16D_50"] = exportRadioF16C
        SR.exporters["F-16D_52"] = exportRadioF16C
        SR.exporters["F-16D_Barak_40"] = exportRadioF16C
        SR.exporters["F-16D_Barak_30"] = exportRadioF16C
        SR.exporters["F-16I"] = exportRadioF16C
    end,
}
return result
