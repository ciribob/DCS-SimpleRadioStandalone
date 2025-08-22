--for F-14
function exportRadioF14(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local ics_devid = 2
    local arc159_devid = 3
    local arc182_devid = 4

    local ICS_device = GetDevice(ics_devid)
    local ARC159_device = GetDevice(arc159_devid)
    local ARC182_device = GetDevice(arc182_devid)

    local intercom_transmit = ICS_device:intercom_transmit()
    local ARC159_ptt = ARC159_device:is_ptt_pressed()
    local ARC182_ptt = ARC182_device:is_ptt_pressed()

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = ICS_device:get_volume()

    _data.radios[2].name = "AN/ARC-159(V)"
    _data.radios[2].freq = ARC159_device:is_on() and SR.round(ARC159_device:get_frequency(), 5000) or 1
    _data.radios[2].modulation = ARC159_device:get_modulation()
    _data.radios[2].volume = ARC159_device:get_volume()
    if ARC159_device:is_guard_enabled() then
        _data.radios[2].secFreq = 243.0 * 1000000
    else
        _data.radios[2].secFreq = 0
    end
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].encKey = ICS_device:get_ky28_key()
    _data.radios[2].enc = ICS_device:is_arc159_encrypted()
    _data.radios[2].encMode = 2

    _data.radios[3].name = "AN/ARC-182(V)"
    _data.radios[3].freq = ARC182_device:is_on() and SR.round(ARC182_device:get_frequency(), 5000) or 1
    _data.radios[3].modulation = ARC182_device:get_modulation()
    _data.radios[3].volume = ARC182_device:get_volume()
    if ARC182_device:is_guard_enabled() then
        _data.radios[3].secFreq = SR.round(ARC182_device:get_guard_freq(), 5000)
    else
        _data.radios[3].secFreq = 0
    end
    _data.radios[3].freqMin = 30 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].encKey = ICS_device:get_ky28_key()
    _data.radios[3].enc = ICS_device:is_arc182_encrypted()
    _data.radios[3].encMode = 2

    --TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

 --   RADIO_ICS_Func_RIO = 402,
--   RADIO_ICS_Func_Pilot = 2044,
    
    local _hotMic = false
    if _seat == 0 then
        if SR.getButtonPosition(2044) > -1 then
            _hotMic = true
        end

    else
        if SR.getButtonPosition(402) > -1 then
            _hotMic = true
        end
     end

    _data.intercomHotMic = _hotMic 

    if (ARC182_ptt) then
        _data.selected = 2 -- radios[3] ARC-182
        _data.ptt = true
    elseif (ARC159_ptt) then
        _data.selected = 1 -- radios[2] ARC-159
        _data.ptt = true
    elseif (intercom_transmit and not _hotMic) then

        -- CHECK ICS Function Selector
        -- If not set to HOT MIC - switch radios and PTT
        -- if set to hot mic - dont switch and ignore
        
        _data.selected = 0 -- radios[1] intercom
        _data.ptt = true
    else
        _data.selected = -1
        _data.ptt = false
    end

    -- handle simultaneous transmission
    if _data.selected ~= 0 and _data.ptt then
        local xmtrSelector = SR.getButtonPosition(381) --402

        if xmtrSelector == 0 then
            _data.radios[2].simul =true
            _data.radios[3].simul =true
        end

    end

    _data.control = 1 -- full radio

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(184,0.25)

    local iffIdent =  SR.getButtonPosition(167)

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL


        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        if iffIdent == -1 then
            if ARC159_ptt then -- ONLY on UHF radio PTT press
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(162)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(201,0.11111), 0.1)*10+SR.round(SR.getSelectorPosition(200,0.11111), 0.1)


    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(164)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(199,0.11111), 0.1) * 1000 + SR.round(SR.getSelectorPosition(198,0.11111), 0.1) * 100 + SR.round(SR.getSelectorPosition(2261,0.11111), 0.1)* 10 + SR.round(SR.getSelectorPosition(2262,0.11111), 0.1)

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(181)

    if mode4On == 0 then
        _data.iff.mode4 = false
    else
        _data.iff.mode4 = true
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(403)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'f14' }
        else
            _data.ambient = {vol = 0.2,  abType = 'f14' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f14' }
    end

    return _data
end

local result = {
   register = function(SR)
  SR.exporters["F-14B"] = exportRadioF14
  SR.exporters["F-14A-135-GR"] = exportRadioF14
  end
}
return result