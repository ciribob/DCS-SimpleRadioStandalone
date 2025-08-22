local _fa18 = {}
_fa18.radio1 = {}
_fa18.radio2 = {}
_fa18.radio3 = {}
_fa18.radio4 = {}
_fa18.radio1.guard = 0
_fa18.radio2.guard = 0
_fa18.radio3.channel = 127 --127 is disabled for MIDS
_fa18.radio4.channel = 127
-- initial IFF status set to -1 to indicate its not initialized, status then set depending on cold/hot start
_fa18.iff = {
    status = -1,
    mode1 = -1,
    mode2 = -1,
    mode3 = -1,
    mode4 = true,
    control = 0,
    expansion = false,
}
_fa18.enttries = 0
_fa18.mode3opt = "" -- to distinguish between 3 and 3/C while ED doesn't fix the different codes for those
_fa18.identEnd = 0 -- time to end IFF ident -(18 seconds)

--[[
From NATOPS - https://info.publicintelligence.net/F18-ABCD-000.pdf (VII-23-2)

ARC-210(RT-1556 and DCS)

Frequency Band(MHz) Modulation  Guard Channel (MHz)
    30 to 87.995        FM
    *108 to 135.995     AM          121.5
    136 to 155.995      AM/FM
    156 to 173.995      FM
    225 to 399.975      AM/FM       243.0 (AM)

*Cannot transmit on 108 thru 117.995 MHz
]]
--

function exportRadioFA18C(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    local _ufc = SR.getListIndicatorValue(6)

    --{
    --   "UFC_Comm1Display": " 1",
    --   "UFC_Comm2Display": " 8",
    --   "UFC_MainDummy": "",
    --   "UFC_OptionCueing1": ":",
    --   "UFC_OptionCueing2": ":",
    --   "UFC_OptionCueing3": "",
    --   "UFC_OptionCueing4": ":",
    --   "UFC_OptionCueing5": "",
    --   "UFC_OptionDisplay1": "GRCV",
    --   "UFC_OptionDisplay2": "SQCH",
    --   "UFC_OptionDisplay3": "CPHR",
    --   "UFC_OptionDisplay4": "AM  ",
    --   "UFC_OptionDisplay5": "MENU",
    --   "UFC_ScratchPadNumberDisplay": "257.000",
    --   "UFC_ScratchPadString1Display": " 8",
    --   "UFC_ScratchPadString2Display": "_",
    --   "UFC_mask": ""
    -- }
    --_data.radios[3].secFreq = 243.0 * 1000000
    -- reset state on aircraft switch
    if _lastUnitId ~= _data.unitId then
        _fa18.radio1.guard = 0
        _fa18.radio1.channel = nil
        _fa18.radio2.guard = 0
        _fa18.radio2.channel = nil
        _fa18.radio3.channel = 127 --127 is disabled for MIDS
        _fa18.radio4.channel = 127
        _fa18.iff = { status = -1, mode1 = -1, mode2 = -1, mode3 = -1, mode4 = true, control = 0, expansion = false }
        _fa18.mode3opt = ""
        _fa18.identEnd = 0
        _fa18.link16 = false
        _fa18.scratchpad = {}
    end

    local getGuardFreq = function(freq, currentGuard, modulation)
        if freq > 1000000 then
            -- check if UFC is currently displaying the GRCV for this radio
            --and change state if so

            if _ufc and _ufc.UFC_OptionDisplay1 == "GRCV" then
                if _ufc.UFC_ScratchPadNumberDisplay then
                    local _ufcFreq = tonumber(_ufc.UFC_ScratchPadNumberDisplay)

                    -- if its the correct radio
                    if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq, 1000) then
                        if _ufc.UFC_OptionCueing1 == ":" then
                            -- GUARD changes based on the tuned frequency
                            if freq > 108 * 1000000 and freq < 135.995 * 1000000 and modulation == 0 then
                                return 121.5 * 1000000
                            end
                            if freq > 108 * 1000000 and freq < 399.975 * 1000000 and modulation == 0 then
                                return 243 * 1000000
                            end

                            return 0
                        else
                            return 0
                        end
                    end
                end
            end

            if currentGuard > 1000 then
                if freq > 108 * 1000000 and freq < 135.995 * 1000000 and modulation == 0 then
                    return 121.5 * 1000000
                end
                if freq > 108 * 1000000 and freq < 399.975 * 1000000 and modulation == 0 then
                    return 243 * 1000000
                end
            end

            return currentGuard
        else
            -- reset state
            return 0
        end
    end

    local getCommChannel = function(currentDisplay, memorizedValue)
        local maybeChannel = currentDisplay

        -- Cue, Guard, Manual, Sea - not channels.
        if string.find(maybeChannel, "^[CGMS]$") then
            return nil -- not channels.
        end

        -- ~0 = 20
        if maybeChannel == "~0" then
            maybeChannel = "20"
        else
            -- leading backtick `n -> 1n.
            maybeChannel = string.gsub(maybeChannel, "^`", "1")
        end

        return tonumber(maybeChannel) or memorizedValue
    end

    -- AN/ARC-210 - 1
    -- Set radio data
    local _radio = _data.radios[2]
    _radio.name = "AN/ARC-210 - COMM1"
    _radio.freq = SR.getRadioFrequency(38)
    _radio.modulation = SR.getRadioModulation(38)
    _radio.volume = SR.getRadioVolume(0, 108, { 0.0, 1.0 }, false)
    -- _radio.encMode = 2 -- Mode 2 is set by aircraft

    _fa18.radio1.channel = getCommChannel(_ufc.UFC_Comm1Display, _fa18.radio1.channel)
    _radio.channel = _fa18.radio1.channel
    _fa18.radio1.guard = getGuardFreq(_radio.freq, _fa18.radio1.guard, _radio.modulation)
    _radio.secFreq = _fa18.radio1.guard

    -- AN/ARC-210 - 2
    -- Set radio data
    _radio = _data.radios[3]
    _radio.name = "AN/ARC-210 - COMM2"
    _radio.freq = SR.getRadioFrequency(39)
    _radio.modulation = SR.getRadioModulation(39)
    _radio.volume = SR.getRadioVolume(0, 123, { 0.0, 1.0 }, false)
    -- _radio.encMode = 2 -- Mode 2 is set by aircraft

    _fa18.radio2.channel = getCommChannel(_ufc.UFC_Comm2Display, _fa18.radio2.channel)
    _radio.channel = _fa18.radio2.channel
    _fa18.radio2.guard = getGuardFreq(_radio.freq, _fa18.radio2.guard, _radio.modulation)
    _radio.secFreq = _fa18.radio2.guard

    -- KY-58 Radio Encryption
    local _ky58Power = SR.round(SR.getButtonPosition(447), 0.1)
    local _ky58PoweredOn = _ky58Power == 0.1
    if _ky58PoweredOn and SR.round(SR.getButtonPosition(444), 0.1) == 0.1 then
        -- mode switch set to C and powered on
        -- Power on!

        -- Get encryption key
        local _channel = SR.getSelectorPosition(446, 0.1) + 1
        if _channel > 6 then
            _channel = 6 -- has two other options - lock to 6
        end

        _radio = _data.radios[2 + SR.getSelectorPosition(144, 0.3)]
        _radio.encMode = 2 -- Mode 2 is set by aircraft
        _radio.encKey = _channel
        _radio.enc = true
    end

    -- MIDS

    -- MIDS A
    _radio = _data.radios[4]
    _radio.name = "MIDS A"
    _radio.modulation = 6
    _radio.volume = SR.getRadioVolume(0, 362, { 0.0, 1.0 }, false)
    _radio.encMode = 2 -- Mode 2 is set by aircraft

    local midsAChannel = _fa18.radio3.channel
    if midsAChannel < 127 and _fa18.link16 then
        _radio.freq = SR.MIDS_FREQ + (SR.MIDS_FREQ_SEPARATION * midsAChannel)
        _radio.channel = midsAChannel
    else
        _radio.freq = 1
        _radio.channel = -1
    end

    -- MIDS B
    _radio = _data.radios[5]
    _radio.name = "MIDS B"
    _radio.modulation = 6
    _radio.volume = SR.getRadioVolume(0, 361, { 0.0, 1.0 }, false)
    _radio.encMode = 2 -- Mode 2 is set by aircraft

    local midsBChannel = _fa18.radio4.channel
    if midsBChannel < 127 and _fa18.link16 then
        _radio.freq = SR.MIDS_FREQ + (SR.MIDS_FREQ_SEPARATION * midsBChannel)
        _radio.channel = midsBChannel
    else
        _radio.freq = 1
        _radio.channel = -1
    end

    -- IFF

    -- set initial IFF status based on cold/hot start since it can't be read directly off the panel
    if _fa18.iff.status == -1 then
        local batterySwitch = SR.getButtonPosition(404)

        if batterySwitch == 0 then
            -- cold start, everything off
            _fa18.iff =
                { status = 0, mode1 = -1, mode2 = -1, mode3 = -1, mode4 = false, control = 0, expansion = false }
        else
            -- hot start, M4 on
            _fa18.iff = { status = 1, mode1 = -1, mode2 = -1, mode3 = -1, mode4 = true, control = 0, expansion = false }
        end
    end

    local iff = _fa18.iff

    if _ufc then
        -- Update current state.
        local scratchpadString = _ufc.UFC_ScratchPadString1Display .. _ufc.UFC_ScratchPadString2Display
        if _ufc.UFC_OptionDisplay4 == "VOCA" then
            -- Link16
            _fa18.link16 = scratchpadString == "ON"
        elseif _ufc.UFC_OptionDisplay2 == "2   " then
            -- IFF transponder
            if scratchpadString == "XP" then
                if iff.status <= 0 then
                    iff.status = 1
                end

                -- Update Mode 1
                if _ufc.UFC_OptionCueing1 == ":" then
                    -- 3-bit digit, followed by a 2-bit one, 5-bit total.
                    local code = string.match(_ufc.UFC_OptionDisplay1, "1%-([0-7][0-3])") -- actual code is displayed in the option display
                    if code then
                        iff.mode1 = tonumber(code)
                    end
                else
                    iff.mode1 = -1
                end

                -- Update Mode 2 and 3
                for modeNumber = 2, 3 do
                    local mode = "mode" .. modeNumber
                    if _ufc["UFC_OptionCueing" .. modeNumber] == ":" then
                        local optionDisplay = _ufc["UFC_OptionDisplay" .. modeNumber]
                        if iff[mode] == -1 or _fa18[mode .. "opt"] ~= optionDisplay then -- just turned on
                            local code = string.match(_ufc.UFC_ScratchPadNumberDisplay, modeNumber .. "%-([0-7]+)")
                            if code then
                                iff[mode] = tonumber(code)
                            end
                            _fa18[mode .. "opt"] = optionDisplay
                        end
                    else
                        iff[mode] = -1
                    end
                end

                -- Update Mode 4
                iff.mode4 = _ufc.UFC_OptionCueing4 == ":"
            elseif scratchpadString == "AI" then
                if iff.status <= 0 then
                    iff.status = 1
                end
            else
                iff.status = 0
            end
        end

        -- Process any updates.
        local clrPressed = SR.getButtonPosition(121) > 0
        if not clrPressed then
            local scratchpad = _ufc.UFC_ScratchPadNumberDisplay
            if scratchpad ~= "" then
                local scratchError = scratchpad == "ERROR"
                if _fa18.scratchpad.blanked then
                    _fa18.scratchpad.blanked = false
                    if not scratchError then
                        -- Updated value valid, try and parse based on what's currently required.
                        -- Find what we're updating.
                        if _ufc.UFC_OptionDisplay4 == "VOCA" then
                            -- Link16
                            if scratchpadString == "ON" then
                                -- Link16 ON
                                local targetRadio = nil

                                if _ufc.UFC_OptionCueing4 == ":" then
                                    targetRadio = "radio3"
                                elseif _ufc.UFC_OptionCueing5 == ":" then
                                    targetRadio = "radio4"
                                end

                                if targetRadio then
                                    local channel = tonumber(scratchpad)
                                    if channel then
                                        _fa18[targetRadio].channel = channel
                                    end
                                end
                            end
                        elseif scratchpadString == "XP" then
                            -- IFF
                            local mode, code = string.match(scratchpad, "([23])%-([0-7]+)")
                            if mode and code then
                                _fa18.iff["mode" .. mode] = tonumber(code)
                            end
                            -- Mode 1 is read from the 'cueing' panels (see above)
                        end
                    end
                elseif not scratchError then
                    -- Register that a value is pending confirmation.
                    _fa18.scratchpad.pending = true
                end
            elseif not _fa18.scratchpad.blanked and _fa18.scratchpad.pending then
                -- Hold value until the screen flashes back
                _fa18.scratchpad.blanked = true
            end
        else
            -- CLR pressed, reset scratchpad.
            _fa18.scratchpad = {}
        end
    end

    -- Mode 1/3 IDENT, requires mode 1 or mode 3 to be on and I/P pushbutton press
    if iff.status > 0 then
        if SR.getButtonPosition(99) == 1 and (iff.mode1 ~= -1 or iff.mode3 ~= -1) then
            _fa18.identEnd = LoGetModelTime() + 18
            iff.status = 2
        elseif iff.status == 2 and LoGetModelTime() >= _fa18.identEnd then
            iff.status = 1
        end
    end

    -- set current IFF settings
    _data.iff = _fa18.iff

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.5 then
            _data.ambient = { vol = 0.3, abType = "fa18" }
        else
            _data.ambient = { vol = 0.2, abType = "fa18" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "fa18" }
    end

    -- Relay (RLY):
    local commRelaySwitch = 350 -- 3-pos: PLAIN/OFF/CIPHER.
    local commGuardXmit = 351 -- 3-pos: COMM1/OFF/COMM2.

    -- If relay is not OFF, it creates a 2-way relay between COMM 1 and COMM 2.
    local commRelaySwitchPosition = SR.getButtonPosition(commRelaySwitch)
    if commRelaySwitchPosition ~= 0 then
        local comm1 = 2
        local comm2 = 3

        local spacing = math.abs(_data.radios[comm1].freq - _data.radios[comm2].freq)

        local ky58Desired = commRelaySwitchPosition == 1

        -- we can retransmit if:
        -- * The two radios are at least 10MHz apart.
        -- * IF cipher is requested, KY-58 must be powered on.
        if spacing >= 10e6 and (not ky58Desired or _ky58PoweredOn) then
            -- Apply params on COMM 1 (index 2) and COMM 2 (index 3)
            for commIdx = 2, 3 do
                -- Force in-cockpit
                _data.radios[commIdx].rtMode = 0
                -- Set as relay
                _data.radios[commIdx].retransmit = true
                -- Pilot can no longer transmit on them.
                _data.radios[commIdx].rxOnly = true

                -- Keep encryption only if relaying through the KY-58.
                _data.radios[commIdx].enc = _data.radios[commIdx].enc and ky58Desired
            end
        end
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["FA-18C_hornet"] = exportRadioFA18C
        SR.exporters["FA-18E"] = exportRadioFA18C
        SR.exporters["FA-18F"] = exportRadioFA18C
        SR.exporters["EA-18G"] = exportRadioFA18C
    end,
}
return result
