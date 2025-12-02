function exportRadioF1CE(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "TRAP-136 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(6)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 311,{0.0,1.0},false)
    _data.radios[2].volMode = 0

    if SR.getSelectorPosition(280,0.2) == 0 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    if SR.getSelectorPosition(282,0.5) == 1 then
        _data.radios[2].channel = SR.getNonStandardSpinner(283, {[0.000]= 1, [0.050]= 2,[0.100]= 3,[0.150]= 4,[0.200]= 5,[0.250]= 6,[0.300]= 7,[0.350]= 8,[0.400]= 9,[0.450]= 10,[0.500]= 11,[0.550]= 12,[0.600]= 13,[0.650]= 14,[0.700]= 15,[0.750]= 16,[0.800]= 17,[0.850]= 18,[0.900]= 19,[0.950]= 20},0.05,3)
    end

    _data.radios[3].name = "TRAP-137B UHF"
    _data.radios[3].freq = SR.getRadioFrequency(8)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 314,{0.0,1.0},false)
    _data.radios[3].volMode = 0
    _data.radios[3].channel = SR.getNonStandardSpinner(348, {[0.000]= 1, [0.050]= 2,[0.100]= 3,[0.150]= 4,[0.200]= 5,[0.250]= 6,[0.300]= 7,[0.350]= 8,[0.400]= 9,[0.450]= 10,[0.500]= 11,[0.550]= 12,[0.600]= 13,[0.650]= 14,[0.700]= 15,[0.750]= 16,[0.800]= 17,[0.850]= 18,[0.900]= 19,[0.950]= 20},0.05,3)


    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}


    local _iffDevice = GetDevice(7)

    if _iffDevice:hasPower() then
        _data.iff.status = 1 -- NORMAL

        if _iffDevice:isIdentActive() then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    else
        _data.iff.status = -1
    end
    
    
    if _iffDevice:isModeActive(4) then 
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if _iffDevice:isModeActive(3) then 
        _data.iff.mode3 = tonumber(_iffDevice:getModeCode(3))
    else
        _data.iff.mode3 = -1
    end

    if _iffDevice:isModeActive(2) then 
        _data.iff.mode2 = tonumber(_iffDevice:getModeCode(2))
    else
        _data.iff.mode2 = -1
    end

    if _iffDevice:isModeActive(1) then 
        _data.iff.mode1 = tonumber(_iffDevice:getModeCode(1))
    else
        _data.iff.mode1 = -1
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
    _data.radios[4].model = SR.RadioModels.AN_ARC210

    _data.control = 0;

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(1)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'f1' }
        else
            _data.ambient = {vol = 0.15,  abType = 'f1' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f1' }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["Mirage-F1CE"] = exportRadioF1CE
        SR.exporters["Mirage-F1EE"] = exportRadioF1CE
    end,
}
return result
