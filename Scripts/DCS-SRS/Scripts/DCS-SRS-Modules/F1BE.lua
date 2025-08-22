function exportRadioF1BE(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    -- Intercom Function
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1.0


    _data.radios[2].name = "TRAP-136 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(6)
    _data.radios[2].modulation = 0
    _data.radios[2].volMode = 0


    _data.radios[3].name = "TRAP-137B UHF"
    _data.radios[3].freq = SR.getRadioFrequency(8)
    _data.radios[3].modulation = 0
    _data.radios[3].volMode = 0
    _data.radios[3].channel = SR.getNonStandardSpinner(348, {[0.000]= "1", [0.050]= "2",[0.100]= "3",[0.150]= "4",[0.200]= "5",[0.250]= "6",[0.300]= "7",[0.350]= "8",[0.400]= "9",[0.450]= "10",[0.500]= "11",[0.550]= "12",[0.600]= "13",[0.650]= "14",[0.700]= "15",[0.750]= "16",[0.800]= "17",[0.850]= "18",[0.900]= "19",[0.950]= "20"},0.05,3)

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(739,0.1)

    local iffIdent =  SR.getButtonPosition(744) -- -1 is off 0 or more is on

    if iffPower >= 7 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    end

    local mode1On =  SR.getButtonPosition(750)

    local _lookupTable = {[0.000]= "0", [0.125] = "1", [0.250] = "2", [0.375] = "3", [0.500] = "4", [0.625] = "5", [0.750] = "6", [0.875] = "7", [1.000] = "0"}
    _data.iff.mode1 = tonumber(SR.getNonStandardSpinner(732,_lookupTable, 0.125,3) .. SR.getNonStandardSpinner(733,{[0.000]= "0", [0.125] = "1", [0.250] = "2", [0.375] = "3", [0.500] = "0", [0.625] = "1", [0.750] = "2", [0.875] = "3", [1.000] = "0"},0.125,3))

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(752)

    _data.iff.mode3 = tonumber(SR.getNonStandardSpinner(734,_lookupTable, 0.125,3) .. SR.getNonStandardSpinner(735,_lookupTable,0.125,3).. SR.getNonStandardSpinner(736,_lookupTable,0.125,3).. SR.getNonStandardSpinner(737,_lookupTable,0.125,3))

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 10 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(745)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end
    
     -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-210"
    _data.radios[4].freq = 251.0 * 1000000 --10-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 110 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

   -- SR.lastKnownSeat = 1

    if SR.lastKnownSeat == 0 then
        _data.radios[2].volume = SR.getRadioVolume(0, 311,{0.0,1.0},false)

        if SR.getSelectorPosition(280,0.2) == 0 and _data.radios[2].freq > 1000 then
            _data.radios[2].secFreq = 121.5 * 1000000
        end

        if SR.getSelectorPosition(282,0.5) == 1 then
            _data.radios[2].channel = SR.getNonStandardSpinner(283, {[0.000]= "1", [0.050]= "2",[0.100]= "3",[0.150]= "4",[0.200]= "5",[0.250]= "6",[0.300]= "7",[0.350]= "8",[0.400]= "9",[0.450]= "10",[0.500]= "11",[0.550]= "12",[0.600]= "13",[0.650]= "14",[0.700]= "15",[0.750]= "16",[0.800]= "17",[0.850]= "18",[0.900]= "19",[0.950]= "20"},0.05,3)
        end

        _data.radios[3].volume = SR.getRadioVolume(0, 314,{0.0,1.0},false)
    else
        _data.radios[2].volume = SR.getRadioVolume(0, 327,{0.0,1.0},false)

        if SR.getSelectorPosition(298,0.2) == 0 and _data.radios[2].freq > 1000 then
            _data.radios[2].secFreq = 121.5 * 1000000
        end

        if SR.getSelectorPosition(300,0.5) == 1 then
            _data.radios[2].channel = SR.getNonStandardSpinner(303, {[0.000]= "1", [0.050]= "2",[0.100]= "3",[0.150]= "4",[0.200]= "5",[0.250]= "6",[0.300]= "7",[0.350]= "8",[0.400]= "9",[0.450]= "10",[0.500]= "11",[0.550]= "12",[0.600]= "13",[0.650]= "14",[0.700]= "15",[0.750]= "16",[0.800]= "17",[0.850]= "18",[0.900]= "19",[0.950]= "20"},0.05,3)
        end

        _data.radios[3].volume = SR.getRadioVolume(0, 330,{0.0,1.0},false)

    end

    _data.control = 0;

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(1)
        local _doorRight = SR.getButtonPosition(6)

        if _doorLeft > 0.2 or _doorRight > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'f1' }
        else
            _data.ambient = {vol = 0.2,  abType = 'f1' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f1' }
    end

    return _data
end

local result = { }

function result.register(SR)
  SR.exporters["Mirage-F1BE"] = exportRadioF1BE
end

return result
