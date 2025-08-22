function exportRadioSA342(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    -- Check for version
    local _newVersion = false
    local _uhfId = 31
    local _fmId = 28

    pcall(function()
        local temp = SR.getRadioFrequency(30, 500)

        if temp ~= nil then
            _newVersion = true
            _fmId = 27
            _uhfId = 30
        end
    end)

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1

    -- TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    local vhfVolume = 68 -- IC1_VHF
    local uhfVolume = 69 -- IC1_UHF
    local fm1Volume = 70 -- IC1_FM1

    local vhfPush = 452 -- IC1_VHF_Push
    local fm1Push = 453 -- IC1_FM1_Push
    local uhfPush = 454 -- IC1_UHF_Push

    if _seat == 1 then
        -- Copilot.
        vhfVolume = 79 -- IC2_VHF
        uhfVolume = 80 -- IC2_UHF
        fm1Volume = 81 -- IC2_FM1

        vhfPush = 455 -- IC2_VHF_Push
        fm1Push = 456 -- IC2_FM1_Push
        uhfPush = 457 -- IC2_UHF_Push
    end

    _data.radios[2].name = "TRAP 138A"
    local MHZ = 1000000
    local _hundreds = SR.round(SR.getKnobPosition(0, 133, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 100 * MHZ
    local _tens = SR.round(SR.getKnobPosition(0, 134, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 10 * MHZ
    local _ones = SR.round(SR.getKnobPosition(0, 136, { 0.0, 0.9 }, { 0, 9 }), 0.1) * MHZ
    local _tenth = SR.round(SR.getKnobPosition(0, 138, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 100000
    local _hundreth = SR.round(SR.getKnobPosition(0, 139, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 10000

    if SR.getSelectorPosition(128, 0.33) > 0.65 then -- Check VHF ON?
        _data.radios[2].freq = _hundreds + _tens + _ones + _tenth + _hundreth
    else
        _data.radios[2].freq = 1
    end
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, vhfVolume, { 1.0, 0.0 }, true)
    _data.radios[2].rtMode = 1

    _data.radios[3].name = "TRA 6031 UHF"

    -- deal with odd radio tune & rounding issue... BUG you cannot set frequency 243.000 ever again
    local freq = SR.getRadioFrequency(_uhfId, 500)
    freq = (math.floor(freq / 1000) * 1000)

    _data.radios[3].freq = freq

    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, uhfVolume, { 0.0, 1.0 }, false)

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- 3 is Incockpit toggle + Gui Enc Key setting
    _data.radios[3].rtMode = 1

    _data.radios[4].name = "TRC 9600 PR4G"
    _data.radios[4].freq = SR.getRadioFrequency(_fmId)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, fm1Volume, { 0.0, 1.0 }, false)

    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 3 -- Variable Enc key but turned on by sim
    _data.radios[4].rtMode = 1

    --- is UHF ON?
    if SR.getSelectorPosition(383, 0.167) == 0 then
        _data.radios[3].freq = 1
    elseif SR.getSelectorPosition(383, 0.167) == 2 then
        --check UHF encryption
        _data.radios[3].enc = true
    end

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(383, 0.167)
    if uhfModeKnob == 5 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    --- is FM ON?
    if SR.getSelectorPosition(272, 0.25) == 0 then
        _data.radios[4].freq = 1
    elseif SR.getSelectorPosition(272, 0.25) == 2 then
        --check FM encryption
        _data.radios[4].enc = true
    end

    if _seat < 2 then
        -- Pilot or Copilot have cockpit controls

        if SR.getButtonPosition(vhfPush) > 0.5 then
            _data.selected = 1
        elseif SR.getButtonPosition(uhfPush) > 0.5 then
            _data.selected = 2
        elseif SR.getButtonPosition(fm1Push) > 0.5 then
            _data.selected = 3
        end

        _data.control = 1 -- COCKPIT Controls
    else
        -- Neither Pilot nor copilot - everything overlay.
        _data.capabilities.dcsRadioSwitch = false
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1

        _data.control = 0 -- OVERLAY Controls
    end

    -- The option reads 'disable HOT_MIC', true means off.
    _data.intercomHotMic = not SR.getSpecialOption("SA342.HOT_MIC")

    -- HANDLE TRANSPONDER
    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getButtonPosition(246)

    local iffIdent = SR.getButtonPosition(240) -- -1 is off 0 or more is on

    if iffPower > 0 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    end

    local mode1On = SR.getButtonPosition(248)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(234, 0.1), 0.1) * 10
        + SR.round(SR.getSelectorPosition(235, 0.1), 0.1)

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(250)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(236, 0.1), 0.1) * 1000
        + SR.round(SR.getSelectorPosition(237, 0.1), 0.1) * 100
        + SR.round(SR.getSelectorPosition(238, 0.1), 0.1) * 10
        + SR.round(SR.getSelectorPosition(239, 0.1), 0.1)

    if mode3On == 0 then
        _data.iff.mode3 = -1
    end

    local mode4On = SR.getButtonPosition(251)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "sa342" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "sa342" }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["SA342M"] = exportRadioSA342
        SR.exporters["SA342L"] = exportRadioSA342
        SR.exporters["SA342Mistral"] = exportRadioSA342
        SR.exporters["SA342Minigun"] = exportRadioSA342
    end,
}
return result
