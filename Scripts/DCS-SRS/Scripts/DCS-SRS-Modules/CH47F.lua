local _ch47 = {}
_ch47.radio1 = { enc = 0 }
_ch47.radio2 = { guard = 0, enc = false }
_ch47.radio3 = { guard = 0, enc = false }

function exportRadioCH47F(_data, SR)
    -- RESET
    if SR.lastKnownUnitId ~= _data.unitId then
        _ch47.radio1 = { enc = 0 }
        _ch47.radio2 = { guard = 0, enc = 0 }
        _ch47.radio3 = { guard = 0, enc = 0 }
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation

    _data.radios[2].name = "AN/ARC-201 FM1" -- ARC 201
    _data.radios[2].freq = SR.getRadioFrequency(51)
    _data.radios[2].modulation = SR.getRadioModulation(51)

    _data.radios[2].encKey = 1
    _data.radios[2].encMode = 3 -- Cockpit Toggle + Gui Enc key setting

    _data.radios[3].name = "AN/ARC-164 UHF" -- ARC_164
    _data.radios[3].freq = SR.getRadioFrequency(49)
    _data.radios[3].modulation = SR.getRadioModulation(49)

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- Cockpit Toggle + Gui Enc key setting

    _data.radios[4].name = "AN/ARC-186 VHF" -- ARC_186
    _data.radios[4].freq = SR.getRadioFrequency(50)
    _data.radios[4].modulation = SR.getRadioModulation(50)

    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 3 -- Cockpit Toggle + Gui Enc key setting

    -- Handle GUARD freq selection for the VHF Backup head.
    local arc186FrequencySelectionDial = SR.getSelectorPosition(1221, 0.1)
    if arc186FrequencySelectionDial == 0 then
        _data.radios[4].freq = 40.5e6
        _data.radios[4].modulation = 1
    elseif arc186FrequencySelectionDial == 1 then
        _data.radios[4].freq = 121.5e6
        _data.radios[4].modulation = 0
    end

    _data.radios[5].name = "AN/ARC-220 HF" -- ARC_220
    _data.radios[5].freq = SR.getRadioFrequency(52)
    _data.radios[5].modulation = SR.getRadioModulation(52)

    _data.radios[5].encMode = 0

    -- TODO (still in overlay)
    _data.radios[6].name = "AN/ARC-201 FM2"
    -- _data.radios[6].freq = SR.getRadioFrequency(32)
    _data.radios[6].freq = 32000000
    _data.radios[6].modulation = 1

    _data.radios[6].freqMin = 20.0 * 1000000
    _data.radios[6].freqMax = 60.0 * 1000000
    _data.radios[6].freqMode = 1

    _data.radios[6].encKey = 1
    _data.radios[6].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    local _seat = SR.lastKnownSeat

    local _pilotCopilotRadios = function(_offset, _pttArg)
        _data.radios[1].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false) -- 23
        _data.radios[2].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 1) -- +1
        _data.radios[3].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 2, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 3) -- +3
        _data.radios[4].volume = SR.getRadioVolume(0, 1219, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 4, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 5)
        _data.radios[5].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 6, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 7)
        _data.radios[6].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 8, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 9)

        local _selector = SR.getSelectorPosition(_offset + 22, 0.05)

        if _selector <= 6 then
            _data.selected = _selector
        elseif _offset ~= 657 and _selector == 9 then -- BU
            -- Look up the BKUP RAD SEL switch to know which radio we have.
            local bkupRadSel = SR.getButtonPosition(1466)
            if bkupRadSel < 0.5 then
                -- Switch facing down: Pilot gets V3, Copilot U2.
                _data.selected = _offset == 591 and 3 or 2
            else
                -- Other way around.
                _data.selected = _offset == 591 and 2 or 3
            end
        else -- 8 = RMT, TODO
            _data.selected = -1
        end

        if _pttArg > 0 then
            local _ptt = SR.getButtonPosition(_pttArg)

            if _ptt >= 0.1 then
                if _ptt == 0.5 then
                    -- intercom
                    _data.selected = 0
                end

                _data.ptt = true
            end
        end
    end

    if _seat == 0 then -- 591
        local _offset = 591

        _pilotCopilotRadios(591, 1271)

        _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
        _data.control = 1 -- Full Radio

    elseif _seat == 1 then --624
        _pilotCopilotRadios(624, 1283)

        _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
        _data.control = 1 -- Full Radio

    elseif _seat == 2 then --657
        _pilotCopilotRadios(657, -1)

        _data.capabilities =
            { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
    else
        _data.radios[1].volume = 1.0
        _data.radios[2].volume = 1.0
        _data.radios[3].volume = 1.0
        _data.radios[4].volume = 1.0
        _data.radios[5].volume = 1.0
        _data.radios[6].volume = 1.0

        _data.radios[1].volMode = 1
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1
        _data.radios[5].volMode = 1
        _data.radios[6].volMode = 1

        _data.capabilities =
            { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    end

    -- EMER Guard switch.
    -- If enabled, forces F1, U2, and V3 to GUARDs.
    local manNormGuard = SR.getSelectorPosition(583, 0.1)
    if manNormGuard > 1 then
        _data.radios[2].freq = 40.5e6 -- F1
        _data.radios[3].freq = 243e6 -- U2
        _data.radios[4].freq = 121.5e6 -- V3
    end

    -- EMER IFF.
    -- When enabled, toggles all transponders ON.
    -- Since we currently can't change M1 and M2 codes in cockpit,
    -- Set 3A to 7700, and enable S.
    --[[ FIXME: Having issues handing over the controls back to the overlay.
    local holdOffEmer = SR.getSelectorPosition(585, 0.1)
    if holdOffEmer > 1 then
        _data.iff = {
        status = 1,
        mode3 = 7700,
        mode4 = true,
        control = 0
        }
    else
        -- Release control back to overlay.
        _data.iff.control = 1
    end
    ]]

    -- engine on
    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "ch47" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ch47" }
    end

    local _ufc = nil
    if _seat == 0 then
        -- RIGHT SEAT (pilot)
        _ufc = SR.getListIndicatorValue(1)
    elseif _seat == 1 then
        -- LEFT SEAT (Copilot)
        _ufc = SR.getListIndicatorValue(0)
    end

    if _ufc ~= nil then
        if _ufc["pg_title_F1_FM_FH_COMM"] then
            --   "pg_title_F1_FM_FH_COMM": "F1 CONTROL",
            -- IF CIPHER
            --   "F1_FM_FH_COMSEC_MODE_CIPHER": "CIPHER",

            if _ufc["F1_FM_FH_COMSEC_MODE_CIPHER"] then
                _ch47.radio1.enc = true
            else
                _ch47.radio1.enc = false
            end
        elseif _ufc["pg_title_U2_COMM"] then
            --   "pg_title_U2_COMM": "U2 CONTROL",
            --     "U2_VHF_AM_MODE_TR_plus_G": "TR+G",
            --   "U2_VHF_AM_COMSEC_MODE_CIPHER": "CIPHER",

            if _ufc["U2_VHF_AM_COMSEC_MODE_CIPHER"] then
                _ch47.radio2.enc = true
            else
                _ch47.radio2.enc = false
            end

            if _ufc["U2_VHF_AM_MODE_TR_plus_G"] then
                _ch47.radio2.guard = 243.0 * 1000000
            else
                _ch47.radio2.guard = 0
            end
        elseif _ufc["pg_title_V3_COMM"] then
            --   "pg_title_V3_COMM": "V3 CONTROL",
            --   "V3_VHF_AM_FM_COMSEC_MODE_CIPHER": "CIPHER",
            --   "V3_VHF_AM_FM_MODE_TR_plus_G": "TR+G",

            if _ufc["V3_VHF_AM_FM_COMSEC_MODE_CIPHER"] then
                _ch47.radio3.enc = true
            else
                _ch47.radio3.enc = false
            end

            if _ufc["V3_VHF_AM_FM_MODE_TR_plus_G"] then
                _ch47.radio3.guard = 121.5 * 1000000
            else
                _ch47.radio3.guard = 0
            end
        elseif _ufc["pg_title_COMM"] then
            --   "F1_COMSEC_MODE_CIPHER": "C",
            --   "U2_COMSEC_MODE_CIPHER": "C",
            --   "V3_COMSEC_MODE_CIPHER": "C",

            if _ufc["F1_COMSEC_MODE_CIPHER"] then
                _ch47.radio1.enc = true
            else
                _ch47.radio1.enc = false
            end

            if _ufc["U2_COMSEC_MODE_CIPHER"] then
                _ch47.radio2.enc = true
            else
                _ch47.radio2.enc = false
            end

            if _ufc["V3_COMSEC_MODE_CIPHER"] then
                _ch47.radio3.enc = true
            else
                _ch47.radio3.enc = false
            end
        end
    end

    _data.radios[2].enc = _ch47.radio1.enc
    _data.radios[3].enc = _ch47.radio2.enc
    _data.radios[3].secFreq = _ch47.radio2.guard
    _data.radios[4].enc = _ch47.radio3.enc
    _data.radios[4].secFreq = _ch47.radio3.guard

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["CH-47Fbl1"] = exportRadioCH47F
    end,
}
return result
