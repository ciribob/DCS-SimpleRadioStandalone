function exportRadioUH60L(_data, SR)
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

    -- Pilots' AN/ARC-201 FM
    local fm1Device = GetDevice(6)
    local fm1Power = GetDevice(0):get_argument_value(601) > 0.01
    local fm1Volume = 0
    local fm1Freq = 0
    local fm1Modulation = 1

    if fm1Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm1Volume = GetDevice(0):get_argument_value(604) * GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(403)
        fm1Freq = fm1Device:get_frequency()
        ARC201FM1Freq = get_param_handle("ARC201FM1param"):get()
        fm1Modulation = get_param_handle("ARC201_FM1_MODULATION"):get()
    end
    
    if not (fm1Power and isDCPower) then
        ARC201FM1Freq = 0
    end

    _data.radios[2].name = "AN/ARC-201 (1)"
    _data.radios[2].freq = ARC201FM1Freq --fm1Freq
    _data.radios[2].modulation = fm1Modulation
    _data.radios[2].volume = fm1Volume
    _data.radios[2].freqMin = 29.990e6
    _data.radios[2].freqMax = 87.985e6
    _data.radios[2].volMode = 0
    _data.radios[2].freqMode = 0
    _data.radios[2].rtMode = 0
    
    -- AN/ARC-164 UHF
    local arc164Device = GetDevice(5)
    local arc164Power = GetDevice(0):get_argument_value(50) > 0
    local arc164Volume = 0
    local arc164Freq = 0
    local arc164SecFreq = 0

    if arc164Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        arc164Volume = GetDevice(0):get_argument_value(51) * GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(404)
        arc164Freq = arc164Device:get_frequency()
        arc164SecFreq = 243e6
    end

    _data.radios[3].name = "AN/ARC-164(V)"
    _data.radios[3].freq = arc164Freq
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = arc164SecFreq
    _data.radios[3].volume = arc164Volume
    _data.radios[3].freqMin = 225e6
    _data.radios[3].freqMax = 399.975e6
    _data.radios[3].volMode = 0
    _data.radios[3].freqMode = 0
    _data.radios[3].rtMode = 0

    -- AN/ARC-186 VHF
    local arc186Device = GetDevice(8)
    local arc186Power = GetDevice(0):get_argument_value(419) > 0
    local arc186Volume = 0
    local arc186Freq = 0
    local arc186SecFreq = 0

    if arc186Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        arc186Volume = GetDevice(0):get_argument_value(410) * GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(405)
        arc186Freq = get_param_handle("ARC186param"):get() --arc186Device:get_frequency()
        arc186SecFreq = 121.5e6
    end
    
    if not (arc186Power and isDCPower) then
        arc186Freq = 0
    arc186SecFreq = 0
    end
    
    _data.radios[4].name = "AN/ARC-186(V)"
    _data.radios[4].freq = arc186Freq
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = arc186SecFreq
    _data.radios[4].volume = arc186Volume
    _data.radios[4].freqMin = 30e6
    _data.radios[4].freqMax = 151.975e6
    _data.radios[4].volMode = 0
    _data.radios[4].freqMode = 0
    _data.radios[4].rtMode = 0

    -- Copilot's AN/ARC-201 FM
    local fm2Device = GetDevice(10)
    local fm2Power = GetDevice(0):get_argument_value(701) > 0.01
    local fm2Volume = 0
    local fm2Freq = 0
    local fm2Modulation = 1

    if fm2Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm2Volume = GetDevice(0):get_argument_value(704) * GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(406)
        fm2Freq = fm2Device:get_frequency()
        ARC201FM2Freq = get_param_handle("ARC201FM2param"):get()
        fm2Modulation = get_param_handle("ARC201_FM2_MODULATION"):get()
    end
    
    if not (fm2Power and isDCPower) then
        ARC201FM2Freq = 0
    end

    _data.radios[5].name = "AN/ARC-201 (2)"
    _data.radios[5].freq = ARC201FM2Freq --fm2Freq
    _data.radios[5].modulation = fm2Modulation
    _data.radios[5].volume = fm2Volume
    _data.radios[5].freqMin = 29.990e6
    _data.radios[5].freqMax = 87.985e6
    _data.radios[5].volMode = 0
    _data.radios[5].freqMode = 0
    _data.radios[5].rtMode = 0

    -- AN/ARC-220 HF radio - not implemented in module, freqs must be changed through SRS UI
    _data.radios[6].name = "AN/ARC-220"
    _data.radios[6].freq = 2e6
    _data.radios[6].modulation = 0
    _data.radios[6].volume = GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(407)
    _data.radios[6].freqMin = 2e6
    _data.radios[6].freqMax = 29.9999e6
    _data.radios[6].volMode = 1
    _data.radios[6].freqMode = 1
    _data.radios[6].encKey = 1 
    _data.radios[6].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting --ANDR0ID Added
    _data.radios[6].rtMode = 1 

    -- Only select radio if power to ICS panel
    local radioXMTSelectorValue = _data.selected or 0
    if isDCPower then
        radioXMTSelectorValue = SR.round(GetDevice(0):get_argument_value(400) * 5, 1)
        -- SR.log(radioXMTSelectorValue)
    end

    _data.selected = radioXMTSelectorValue
    _data.intercomHotMic = GetDevice(0):get_argument_value(402) > 0
    _data.ptt = GetDevice(0):get_argument_value(82) > 0
    _data.control = 1; -- full radio HOTAS control

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
        _data.ambient = {vol = 0.2,  abType = 'uh60' }
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'uh60' }
    end
    
    return _data
end

local result = {
   register = function(SR)
  SR.exporters["UH-60L"] = exportRadioUH60L
  SR.exporters["MH-60R"] = exportRadioUH60L
  end
}
return result
