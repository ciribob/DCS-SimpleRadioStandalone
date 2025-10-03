local _ah64Mode1Persist = -1 -- Need this persistence for only MODE1 because it's pulled from the XPNDR page; default it to off
function exportRadioAH64D(_data, SR)
    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "Recommended: Always Allow SRS Hotkeys - OFF. Bind Intercom Select & PTT, Radio PTT and DCS RTS up down" }
    _data.control = 1

    local _iffSettings = {
        status = 0,
        mode1 = _ah64Mode1Persist,
        mode2 = -1,
        mode3 = -1,
        mode4 = false,
        control = 0,
        expansion = false
    }
    
    -- Check if player is in a new aircraft
    if SR.LastKnownUnitId ~= _data.unitId then
        -- New aircraft; SENS volume is at 0
            pcall(function()
                 -- source https://github.com/DCSFlightpanels/dcs-bios/blob/master/Scripts/DCS-BIOS/lib/AH-64D.lua
                GetDevice(63):performClickableAction(3011, 1) -- Pilot Master
                GetDevice(63):performClickableAction(3012, 1) -- Pilot SENS

                GetDevice(62):performClickableAction(3011, 1) -- CoPilot Master
                GetDevice(62):performClickableAction(3012, 1) -- CoPilot SENS
            end)
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-186 VHF"
    _data.radios[2].freq = SR.getRadioFrequency(58)
    _data.radios[2].modulation = SR.getRadioModulation(58)
    _data.radios[2].volMode = 0
    _data.radios[2].model = SR.RadioModels.AN_ARC186

    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(57)
    _data.radios[3].modulation = SR.getRadioModulation(57)
    _data.radios[3].volMode = 0
    _data.radios[3].encMode = 2
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    _data.radios[4].name = "AN/ARC201D FM1"
    _data.radios[4].freq = SR.getRadioFrequency(59)
    _data.radios[4].modulation = SR.getRadioModulation(59)
    _data.radios[4].volMode = 0
    _data.radios[4].encMode = 2
    _data.radios[4].model = SR.RadioModels.AN_ARC201D

    _data.radios[5].name = "AN/ARC-201D FM2"
    _data.radios[5].freq = SR.getRadioFrequency(60)
    _data.radios[5].modulation = SR.getRadioModulation(60)
    _data.radios[5].volMode = 0
    _data.radios[5].encMode = 2
    _data.radios[5].model = SR.RadioModels.AN_ARC201D

    _data.radios[6].name = "AN/ARC-220 HF"
    _data.radios[6].freq = SR.getRadioFrequency(61)
    _data.radios[6].modulation = 0
    _data.radios[6].volMode = 0
    _data.radios[6].encMode = 2 -- As of DCS ver 2.9.4.53627 the HF preset functionality is bugged, but I'll leave this here in hopes ED fixes the bug
    _data.radios[6].model = SR.RadioModels.AN_ARC220

    local _seat = get_param_handle("SEAT"):get() -- PLT/CPG ?
    local _eufdDevice = nil
    local _mpdLeft = nil
    local _mpdRight = nil
    local _iffIdentBtn = nil
    local _iffEmergency = nil

    if _seat == 0 then
        _eufdDevice = SR.getListIndicatorValue(18)
        _mpdLeft = SR.getListIndicatorValue(7)
        _mpdRight = SR.getListIndicatorValue(9)
        _iffIdentBtn = SR.getButtonPosition(347) -- PLT comm panel ident button
        _iffEmergency = GetDevice(0):get_argument_value(404) -- PLT Emergency Panel XPNDR Indicator

        local _masterVolume = SR.getRadioVolume(0, 344, { 0.0, 1.0 }, false) 
        
        --intercom 
        _data.radios[1].volume = SR.getRadioVolume(0, 345, { 0.0, 1.0 }, false) * _masterVolume

        -- VHF
        if SR.getButtonPosition(449) == 0 then
            _data.radios[2].volume = SR.getRadioVolume(0, 334, { 0.0, 1.0 }, false) * _masterVolume 
        else
            _data.radios[2].volume = 0
        end

        -- UHF
        if SR.getButtonPosition(450) == 0 then
            _data.radios[3].volume = SR.getRadioVolume(0, 335, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[3].volume = 0
        end

        -- FM1
        if SR.getButtonPosition(451) == 0 then
            _data.radios[4].volume = SR.getRadioVolume(0, 336, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[4].volume = 0
        end

         -- FM2
        if SR.getButtonPosition(452) == 0 then
            _data.radios[5].volume = SR.getRadioVolume(0, 337, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[5].volume = 0
        end

         -- HF
        if SR.getButtonPosition(453) == 0 then
            _data.radios[6].volume = SR.getRadioVolume(0, 338, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[6].volume = 0
        end

         if SR.getButtonPosition(346) ~= 1 then
            _data.intercomHotMic = true
        end

    else
        _eufdDevice = SR.getListIndicatorValue(19)
        _mpdLeft = SR.getListIndicatorValue(11)
        _mpdRight = SR.getListIndicatorValue(13)
        _iffIdentBtn = SR.getButtonPosition(388) -- CPG comm panel ident button
        _iffEmergency = GetDevice(0):get_argument_value(428) -- CPG Emergency Panel XPNDR Indicator

        local _masterVolume = SR.getRadioVolume(0, 385, { 0.0, 1.0 }, false) 

        --intercom 
        _data.radios[1].volume = SR.getRadioVolume(0, 386, { 0.0, 1.0 }, false) * _masterVolume

        -- VHF
        if SR.getButtonPosition(459) == 0 then
            _data.radios[2].volume = SR.getRadioVolume(0, 375, { 0.0, 1.0 }, false) * _masterVolume 
        else
            _data.radios[2].volume = 0
        end

        -- UHF
        if SR.getButtonPosition(460) == 0 then
            _data.radios[3].volume = SR.getRadioVolume(0, 376, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[3].volume = 0
        end

        -- FM1
        if SR.getButtonPosition(461) == 0 then
            _data.radios[4].volume = SR.getRadioVolume(0, 377, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[4].volume = 0
        end

         -- FM2
        if SR.getButtonPosition(462) == 0 then
            _data.radios[5].volume = SR.getRadioVolume(0, 378, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[5].volume = 0
        end

         -- HF
        if SR.getButtonPosition(463) == 0 then
            _data.radios[6].volume = SR.getRadioVolume(0, 379, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[6].volume = 0
        end

        if SR.getButtonPosition(387) ~= 1 then
            _data.intercomHotMic = true
        end

    end

    if _eufdDevice then
        -- figure out selected
        if _eufdDevice['Rts_VHF_'] == '<' then
            _data.selected = 1
        elseif _eufdDevice['Rts_UHF_'] == '<' then
            _data.selected = 2
        elseif _eufdDevice['Rts_FM1_'] == '<' then
            _data.selected = 3
        elseif _eufdDevice['Rts_FM2_'] == '<' then
            _data.selected = 4
        elseif _eufdDevice['Rts_HF_'] == '<' then
            _data.selected = 5
        end

        if _eufdDevice['Guard'] == 'G' then
            _data.radios[3].secFreq = 243e6
        end

        -- TODO??: Regarding IFF, I had not checked prior to @falcoger pointing it out... the Apache XPNDR
        --      page only runs a simple logic check (#digits) to accept input for modes. I considered
        --      scripting the logic on SRS' end to perform the check for validity, but without sufficient
        --      means to provide user feedback regarding the validity, I opted to just let the user input
        --      invalid codes since it seems (?) LotAtc doesn't perform a proper logic check either, just a
        --      #digit check. Hopefully ED will someday emplace the logic check within the Apache.

        if _eufdDevice["Transponder_MC"] == "NORM" then -- IFF NORM
            _iffSettings.status = (_iffIdentBtn > 0) and 2 or 1 -- IDENT and Power

            _iffSettings.mode3 = tonumber(_eufdDevice["Transponder_MODE_3A"]) or -1

            _iffSettings.mode4 = _eufdDevice["XPNDR_MODE_4"] ~= nil
        else -- Transponder_MC == "STBY" or there's no power
            _iffSettings.status = 0
        end

        if _iffEmergency == 1 then
            _iffSettings.mode3 = 7700

            -- XPNDR btn would actually turn on the XPNDR if it were in STBY (in real life)
            -- Allow IDENT to still apply.
            _iffSettings.status = (_iffIdentBtn > 0) and 2 or 1 
        end

        _data.radios[3].enc = _eufdDevice["Cipher_UHF"] ~= nil
        _data.radios[3].encKey = tonumber(string.match(_eufdDevice["Cipher_UHF"] or "C1", "^C(%d+)"))

        _data.radios[4].enc = _eufdDevice["Cipher_FM1"] ~= nil
        _data.radios[4].encKey = tonumber(string.match(_eufdDevice["Cipher_FM1"] or "C1", "^C(%d+)"))

        _data.radios[5].enc = _eufdDevice["Cipher_FM2"] ~= nil
        _data.radios[5].encKey = tonumber(string.match(_eufdDevice["Cipher_FM2"] or "C1", "^C(%d+)"))

        _data.radios[6].enc = _eufdDevice["Cipher_HF"] ~= nil
        _data.radios[6].encKey =  tonumber(string.match(_eufdDevice["Cipher_HF"] or "C1", "^C(%d+)"))
    end

    if (_mpdLeft or _mpdRight) then
        if _mpdLeft["Mode_S_Codes_Window_text_1"] then -- We're on the XPNDR page on the LEFT MPD
            _ah64Mode1Persist = _mpdLeft["PB24_9"] == "}1" and -1 or tonumber(string.format("%02d", _mpdLeft["PB7_23"]))
        end

        if _mpdRight["Mode_S_Codes_Window_text_1"] then -- We're on the XPNDR page on the RIGHT MPD
            _ah64Mode1Persist = _mpdRight["PB24_9"] == "}1" and -1 or tonumber(string.format("%02d", _mpdRight["PB7_23"]))
        end
    end

      --CYCLIC_RTS_SW_LEFT 573 CPG 531 PLT
    local _pttButtonId = 573
    if _seat == 0 then
        _pttButtonId = 531
    end

    local _pilotPTT = SR.getButtonPosition(_pttButtonId)
    if _pilotPTT >= 0.5 then

        _data.intercomHotMic = false
        -- intercom
        _data.selected = 0
        _data.ptt = true

    elseif _pilotPTT <= -0.5 then
        _data.ptt = true
    end

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(795)
        local _doorRight = SR.getButtonPosition(798)

        if _doorLeft > 0.3 or _doorRight > 0.3 then 
            _data.ambient = {vol = 0.35,  abType = 'ah64' }
        else
            _data.ambient = {vol = 0.2,  abType = 'ah64' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'ah64' }
    end

    for k,v in pairs(_iffSettings) do _data.iff[k] = v end -- IFF table overwrite

    return _data

end

local result = {
    register = function(SR)
        SR.exporters["AH-64D_BLK_II"] = exportRadioAH64D
    end,
}
return result
