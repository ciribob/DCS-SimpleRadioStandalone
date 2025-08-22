local _mirageEncStatus = false
local _previousEncState = 0

function exportRadioM2000C(_data, SR)

    local RED_devid = 20
    local GREEN_devid = 19
    local RED_device = GetDevice(RED_devid)
    local GREEN_device = GetDevice(GREEN_devid)
    
    local has_cockpit_ptt = false;
    
    local RED_ptt = false
    local GREEN_ptt = false
    local GREEN_guard = 0
    
    pcall(function() 
        RED_ptt = RED_device:is_ptt_pressed()
        GREEN_ptt = GREEN_device:is_ptt_pressed()
        has_cockpit_ptt = true
        end)
        
    pcall(function() 
        GREEN_guard = tonumber(GREEN_device:guard_standby_freq())
        end)

        
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    _data.control = 0 
    
    -- Different PTT/select control if the module version supports cockpit PTT
    if has_cockpit_ptt then
        _data.control = 1
        _data.capabilities.dcsPtt = true
        _data.capabilities.dcsRadioSwitch = true
        if (GREEN_ptt) then
            _data.selected = 1 -- radios[2] GREEN V/UHF
            _data.ptt = true
        elseif (RED_ptt) then
            _data.selected = 2 -- radios[3] RED UHF
            _data.ptt = true
        else
            _data.selected = -1
            _data.ptt = false
        end
    end
    
    

    _data.radios[2].name = "TRT ERA 7000 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(19)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 707, { 0.0, 1.0 }, false)

    --guard mode for V/UHF Radio
    if GREEN_guard>0 then
        _data.radios[2].secFreq = GREEN_guard
    end
    

    -- get channel selector
    local _selector = SR.getSelectorPosition(448, 0.50)

    if _selector == 1 then
        _data.radios[2].channel = SR.getSelectorPosition(445, 0.05)  --add 1 as channel 0 is channel 1
    end

    _data.radios[3].name = "TRT ERA 7200 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(20)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 706, { 0.0, 1.0 }, false)

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- 3 is Incockpit toggle + Gui Enc Key setting

    --  local _switch = SR.getButtonPosition(700) -- remmed, the connectors are being coded, maybe soon will be a full radio.

    --    if _switch == 1 then
    --      _data.selected = 0
    --  else
    --     _data.selected = 1
    -- end



    -- reset state on aircraft switch
    if _lastUnitId ~= _data.unitId then
        _mirageEncStatus = false
        _previousEncState = 0
    end

    -- handle the button no longer toggling...
    if SR.getButtonPosition(432) > 0.5 and _previousEncState < 0.5 then
        --431

        if _mirageEncStatus then
            _mirageEncStatus = false
        else
            _mirageEncStatus = true
        end
    end

    _data.radios[3].enc = _mirageEncStatus

    _previousEncState = SR.getButtonPosition(432)



    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}


    local _iffDevice = GetDevice(42)

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
    
      --  SR.log(JSON:encode(_data.iff)..'\n\n')

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.3 then 
            _data.ambient = {vol = 0.3,  abType = 'm2000' }
        else
            _data.ambient = {vol = 0.2,  abType = 'm2000' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'm2000' }
    end

    return _data
end

local result = {
   register = function(SR)
  SR.exporters["M-2000C"] = exportRadioM2000C
  SR.exporters["M-2000D"] = exportRadioM2000C
  end
}
return result