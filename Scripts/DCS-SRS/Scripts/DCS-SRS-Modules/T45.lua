function exportRadioT45(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local radio1Device = GetDevice(1)
    local radio2Device = GetDevice(2)
    local mainFreq1 = 0
    local guardFreq1 = 0
    local mainFreq2 = 0
    local guardFreq2 = 0
    
    local comm1Switch = GetDevice(0):get_argument_value(191) 
    local comm2Switch = GetDevice(0):get_argument_value(192) 
    local comm1PTT = GetDevice(0):get_argument_value(294)
    local comm2PTT = GetDevice(0):get_argument_value(294) 
    local intercomPTT = GetDevice(0):get_argument_value(295)    
    local ICSMicSwitch = GetDevice(0):get_argument_value(196) --0 cold, 1 hot
    
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = GetDevice(0):get_argument_value(198)
    
    local modeSelector1 = GetDevice(0):get_argument_value(256) -- 0:off, 0.25:T/R, 0.5:T/R+G
    if modeSelector1 == 0.5 and comm1Switch == 1 then
        mainFreq1 = SR.round(radio1Device:get_frequency(), 5000)
        if mainFreq1 > 225000000 then
            guardFreq1 = 243.000E6
        elseif mainFreq1 < 155975000 then
            guardFreq1 = 121.500E6
        else
            guardFreq1 = 0
        end
    elseif modeSelector1 == 0.25 and comm1Switch == 1 then
        guardFreq1 = 0
        mainFreq1 = SR.round(radio1Device:get_frequency(), 5000)
    else
        guardFreq1 = 0
        mainFreq1 = 0
    end

    local arc182 = _data.radios[2]
    arc182.name = "AN/ARC-182(V) - 1"
    arc182.freq = mainFreq1
    arc182.secFreq = guardFreq1
    arc182.modulation = radio1Device:get_modulation()  
    arc182.freqMin = 30.000e6
    arc182.freqMax = 399.975e6
    arc182.volume = GetDevice(0):get_argument_value(246)

    local modeSelector2 = GetDevice(0):get_argument_value(280) -- 0:off, 0.25:T/R, 0.5:T/R+G
    if modeSelector2 == 0.5 and comm2Switch == 1 then
        mainFreq2 = SR.round(radio2Device:get_frequency(), 5000)
        if mainFreq2 > 225000000 then
            guardFreq2 = 243.000E6
        elseif mainFreq2 < 155975000 then
            guardFreq2 = 121.500E6
        else
            guardFreq2 = 0
        end
    elseif modeSelector2 == 0.25 and comm2Switch == 1 then
        guardFreq2 = 0
        mainFreq2 = SR.round(radio2Device:get_frequency(), 5000)
    else
        guardFreq2 = 0
        mainFreq2 = 0
    end
    
    local arc182_2 = _data.radios[3]
    arc182_2.name = "AN/ARC-182(V) - 2"
    arc182_2.freq = mainFreq2
    arc182_2.secFreq = guardFreq2
    arc182_2.modulation = radio2Device:get_modulation()  
    arc182_2.freqMin = 30.000e6
    arc182_2.freqMax = 399.975e6
    arc182_2.volume = GetDevice(0):get_argument_value(270)

    if comm1PTT == 1 then
        _data.selected = 1 -- comm 1
        _data.ptt = true
    elseif comm2PTT == -1 then
        _data.selected = 2 -- comm 2
        _data.ptt = true
    elseif intercomPTT == 1 then
        _data.selected = 0 -- intercom
        _data.ptt = true
    else
        _data.selected = -1
        _data.ptt = false
    end
    
    if ICSMicSwitch == 1 then
        _data.intercomHotMic = true
    else
        _data.intercomHotMic = false
    end

    _data.control = 1; -- full radio HOTAS control

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
        _data.ambient = {vol = 0.2,  abType = 'jet' }
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'jet' }
    end
    
    return _data
end


local result = { }

function result.register(SR)
  SR.exporters["T-45"] = exportRadioT45
end

return result