function exportRadioPUCARA(_data)
   _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
   
   _data.radios[1].name = "Intercom"
   _data.radios[1].freq = 100.0
   _data.radios[1].modulation = 2 --Special intercom modulation
   _data.radios[1].volume = GetDevice(0):get_argument_value(764)
    
    local comm1Switch = GetDevice(0):get_argument_value(762) 
    local comm2Switch = GetDevice(0):get_argument_value(763) 
    local comm1PTT = GetDevice(0):get_argument_value(765)
    local comm2PTT = GetDevice(0):get_argument_value(7655) 
    local modeSelector1 = GetDevice(0):get_argument_value(1080) -- 0:off, 0.25:T/R, 0.5:T/R+G
    local amfm = GetDevice(0):get_argument_value(770)

    _data.radios[2].name = "SUNAIR ASB-850 COM1"
    _data.radios[2].modulation = amfm
    _data.radios[2].volume = SR.getRadioVolume(0, 1079, { 0.0, 1.0 }, false)

    if comm1Switch == 0 then 
        _data.radios[2].freq = 246.000e6
        _data.radios[2].secFreq = 0
    elseif comm1Switch == 1 then 
        local one = 100.000e6 * SR.getSelectorPosition(1090, 1 / 4)
        local two = 10.000e6 * SR.getSelectorPosition(1082, 1 / 10)
        local three = 1.000e6 * SR.getSelectorPosition(1084, 1 / 10)
        local four = 0.1000e6 * SR.getSelectorPosition(1085, 1 / 10)
        local five = 0.010e6 * SR.getSelectorPosition(1087, 1 / 10)
        local six = 0.0010e6 * SR.getSelectorPosition(1086, 1 / 10)
        mainFreq =  one + two + three + four + five - six
        _data.radios[2].freq = mainFreq
        _data.radios[2].secFreq = 0
    
    end
    
    _data.radios[3].name = "RTA-42A BENDIX COM2"
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 1100, { 0.0, 1.0 }, false)

    if comm2Switch == 0 then 
        _data.radios[3].freq = 140.000e6
        _data.radios[3].secFreq = 0
    elseif comm2Switch == 1 then 
        local onea = 100.000e6 * SR.getSelectorPosition(1104, 1 / 4)
        local twoa = 10.000e6 * SR.getSelectorPosition(1103, 1 / 10)
                
        mainFreqa =  onea + twoa 
        _data.radios[3].freq = mainFreqa
        _data.radios[3].secFreq = 0
    
    end
   
    _data.control = 1 -- Hotas Controls radio
    
    
     _data.control = 0;
    _data.selected = 1

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
    SR.exporters["PUCARA"] = exportRadioPUCARA
end

return result
