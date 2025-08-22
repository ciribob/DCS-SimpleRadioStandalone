function exportRadioSH60B(_data, SR)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local isDCPower = SR.getButtonPosition(17) > 0 -- just using battery switch position for now, could tie into DC ESS BUS later?
    local intercomVolume = 0
    if isDCPower then
        -- ics master volume
        intercomVolume = GetDevice(0):get_argument_value(401)
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = intercomVolume
    _data.radios[1].volMode = 0
    _data.radios[1].freqMode = 0
    _data.radios[1].rtMode = 0

    -- Copilot's AN/ARC-182 FM (COM1)
    local fm2Device = GetDevice(8)
    local fm2Power = GetDevice(0):get_argument_value(3113) > 0 --NEEDS UPDATE
    local fm2Volume = 0
    local fm2Freq = 0
    local fm2Mod = 0

    if fm2Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm2Volume = GetDevice(0):get_argument_value(3167)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(403)
        fm2Freq = fm2Device:get_frequency()
        ARC182FM2Freq = get_param_handle("ARC182_1_param"):get()
        fm2Mod = GetDevice(0):get_argument_value(3119)
    end

    if not (fm2Power and isDCPower) then
        ARC182FM2Freq = 0
    end

    if fm2Mod == 1 then --Nessecary since cockpit switches are inverse from SRS settings
        fm2ModCorrected = 0
    else
        fm2ModCorrected = 1
    end

    _data.radios[2].name = "AN/ARC-182 (1)"
    _data.radios[2].freq = ARC182FM2Freq --fm2Freq
    _data.radios[2].modulation = fm2ModCorrected
    _data.radios[2].volume = fm2Volume
    _data.radios[2].freqMin = 30e6
    _data.radios[2].freqMax = 399.975e6
    _data.radios[2].volMode = 0
    _data.radios[2].freqMode = 0
    _data.radios[2].rtMode = 0

    -- Pilots' AN/ARC-182 FM (COM2)
    local fm1Device = GetDevice(6)
    local fm1Power = GetDevice(0):get_argument_value(3113) > 0 --NEEDS UPDATE
    local fm1Volume = 0
    local fm1Freq = 0
    local fm1Mod = 0

    if fm1Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm1Volume = GetDevice(0):get_argument_value(3168)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(404)
        fm1Freq = fm1Device:get_frequency()
        ARC182FM1Freq = get_param_handle("ARC182_2_param"):get()
        fm1Mod = GetDevice(0):get_argument_value(3120)
    end

    if not (fm1Power and isDCPower) then
        ARC182FM1Freq = 0
    end

    if fm1Mod == 1 then
        fm1ModCorrected = 0
    else
        fm1ModCorrected = 1
    end

    _data.radios[3].name = "AN/ARC-182 (2)"
    _data.radios[3].freq = ARC182FM1Freq --fm1Freq
    _data.radios[3].modulation = fm1ModCorrected
    _data.radios[3].volume = fm1Volume
    _data.radios[3].freqMin = 30e6
    _data.radios[3].freqMax = 399.975e6
    _data.radios[3].volMode = 0
    _data.radios[3].freqMode = 0
    _data.radios[3].rtMode = 0

    --D/L not implemented in module, using a "dummy radio" for now
    _data.radios[4].name = "DATA LINK (D/L)"
    _data.radios[4].freq = 0
    _data.radios[4].modulation = 0
    _data.radios[4].volume = GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(409)
    _data.radios[4].freqMin = 15e9
    _data.radios[4].freqMax = 15e9
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 0
    _data.radios[4].rtMode = 1

    -- AN/ARC-174A HF radio - not implemented in module, freqs must be changed through SRS UI
    _data.radios[5].name = "AN/ARC-174(A)"
    _data.radios[5].freq = 2e6
    _data.radios[5].modulation = 0
    _data.radios[5].volume = GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(407)
    _data.radios[5].freqMin = 2e6
    _data.radios[5].freqMax = 29.9999e6
    _data.radios[5].volMode = 1
    _data.radios[5].freqMode = 1
    _data.radios[5].encKey = 1
    _data.radios[5].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting --ANDR0ID Added
    _data.radios[5].rtMode = 1

    -- Only select radio if power to ICS panel
    local radioXMTSelectorValue = _data.selected or 0
    if isDCPower then
        radioXMTSelectorValue = SR.round(GetDevice(0):get_argument_value(400) * 5, 1)
        -- SR.log(radioXMTSelectorValue)
    end

    -- UHF/VHF BACKUP
    local arc164Device = GetDevice(5)
    local arc164Power = GetDevice(0):get_argument_value(3091) > 0
    local arc164Volume = 0
    local arc164Freq = 0
    local arc164Mod = 0
    --local arc164SecFreq = 0

    if arc164Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        arc164Volume = GetDevice(0):get_argument_value(3089)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(405)
        arc164Freq = get_param_handle("VUHFB_FREQ"):get()
        arc164Mod = GetDevice(0):get_argument_value(3094)
        --arc164SecFreq = 243e6
    end

    if arc164Mod == 1 then
        arc164ModCorrected = 0
    else
        arc164ModCorrected = 1
    end

    _data.radios[6].name = "UHF/VHF BACKUP"
    _data.radios[6].freq = arc164Freq * 1000
    _data.radios[6].modulation = arc164ModCorrected
    --_data.radios[6].secFreq = arc164SecFreq
    _data.radios[6].volume = arc164Volume
    _data.radios[6].freqMin = 30e6
    _data.radios[6].freqMax = 399.975e6
    _data.radios[6].volMode = 0
    _data.radios[6].freqMode = 0
    _data.radios[6].rtMode = 0

    _data.selected = radioXMTSelectorValue
    _data.intercomHotMic = GetDevice(0):get_argument_value(402) > 0
    _data.ptt = GetDevice(0):get_argument_value(82) > 0
    _data.control = 1 -- full radio HOTAS control

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "uh60" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "uh60" }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["SH60B"] = exportRadioSH60B
    end,
}
return result
