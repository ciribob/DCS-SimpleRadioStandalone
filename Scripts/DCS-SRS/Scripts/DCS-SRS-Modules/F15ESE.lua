function exportRadioF15ESE(_data)


    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = true, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
 
 

    _data.radios[2].name = "AN/ARC-164 UHF-1"
    _data.radios[2].freq = SR.getRadioFrequency(7)
    _data.radios[2].modulation = SR.getRadioModulation(7)


    _data.radios[3].name = "AN/ARC-164 UHF-2"
    _data.radios[3].freq = SR.getRadioFrequency(8)
    _data.radios[3].modulation = SR.getRadioModulation(8)

    -- TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    -- {"UFC_CC_01":"","UFC_CC_02":"","UFC_CC_03":"","UFC_CC_04":"","UFC_DISPLAY":"","UFC_SC_01":"LAW 250'","UFC_SC_02":"TCN OFF","UFC_SC_03":"IFF 4","UFC_SC_04":"TF OFF","UFC_SC_05":"*U243000G","UFC_SC_05A":".","UFC_SC_06":" G","UFC_SC_07":"GV ","UFC_SC_08":"U133000*","UFC_SC_08A":".","UFC_SC_09":"N-F OFF","UFC_SC_10":"A4/E4","UFC_SC_11":"A/P OFF","UFC_SC_12":"STR B"}

    local setGuard = function(freq)
        -- GUARD changes based on the tuned frequency
        if freq > 108*1000000
                and freq < 135.995*1000000 then
            return 121.5 * 1000000
        end
        if freq > 108*1000000
                and freq < 399.975*1000000 then
            return 243 * 1000000
        end

        return -1
    end

    local _ufc = SR.getListIndicatorValue(9)

    if _ufc and _ufc.UFC_SC_05 and string.find(_ufc.UFC_SC_05, "G",1,true) and _data.radios[2].freq > 1000 then
         _data.radios[2].secFreq = setGuard(_data.radios[2].freq)
    end

    if _ufc and _ufc.UFC_SC_08 and string.find(_ufc.UFC_SC_08, "G",1,true) and _data.radios[3].freq > 1000 then
         _data.radios[3].secFreq = setGuard(_data.radios[3].freq)
    end
   
    if _seat == 0 then
        _data.radios[1].volume =  SR.getRadioVolume(0, 504, { 0.0, 1.0 }, false)
        _data.radios[2].volume = SR.getRadioVolume(0, 282, { 0.0, 1.0 }, false)
        _data.radios[3].volume = SR.getRadioVolume(0, 283, { 0.0, 1.0 }, false)

        if SR.getButtonPosition(509) == 0.5 then
            _data.intercomHotMic = true
        end
    else
        _data.radios[1].volume =  SR.getRadioVolume(0, 1422, { 0.0, 1.0 }, false)
        _data.radios[2].volume = SR.getRadioVolume(0, 1307, { 0.0, 1.0 }, false)
        _data.radios[3].volume = SR.getRadioVolume(0, 1308, { 0.0, 1.0 }, false)

        if SR.getButtonPosition(1427) == 0.5 then
            _data.intercomHotMic = true
        end
    end

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'f15' }
        else
            _data.ambient = {vol = 0.2,  abType = 'f15' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f15' }
    end

      -- HANDLE TRANSPONDER
    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local _iffDevice = GetDevice(68)

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

    -- local temp = {}
    -- temp.mode4 = string.format("%04d",_iffDevice:getModeCode(4)) -- mode 4
    -- temp.mode1 = string.format("%02d",_iffDevice:getModeCode(1))
    -- temp.mode3 = string.format("%04d",_iffDevice:getModeCode(3))
    -- temp.mode2 = string.format("%04d",_iffDevice:getModeCode(2))
    -- temp.mode4Active = _iffDevice:isModeActive(4)
    -- temp.mode1Active = _iffDevice:isModeActive(1)
    -- temp.mode3Active = _iffDevice:isModeActive(3)
    -- temp.mode2Active = _iffDevice:isModeActive(2)
    -- temp.ident = _iffDevice:isIdentActive()
    -- temp.power = _iffDevice:hasPower()


    return _data
end

local result = { }

function result.register(SR)
  SR.exporters["F-15ESE"] = exportRadioF15ESE
end

return result

