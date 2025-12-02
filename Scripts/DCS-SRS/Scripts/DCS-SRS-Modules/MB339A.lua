function exportRadioMB339A(_data, SR)
    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = false, desc = "To enable the Intercom HotMic pull the INT knob located on ICS" }

    local main_panel = GetDevice(0)

    local intercom_device_id = main_panel:get_srs_device_id(0)
    local comm1_device_id = main_panel:get_srs_device_id(1)
    local comm2_device_id = main_panel:get_srs_device_id(2)
    local iff_device_id = main_panel:get_srs_device_id(3)
    
    local ARC150_device = GetDevice(comm1_device_id)
    local SRT_651_device = GetDevice(comm2_device_id)
    local intercom_device = GetDevice(intercom_device_id)
    local iff_device = GetDevice(iff_device_id)

    -- Intercom Function
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = intercom_device:get_volume()
    _data.radios[1].model = SR.RadioModels.Intercom

    -- AN/ARC-150(V)2 - COMM1 Radio
    _data.radios[2].name = "AN/ARC-150(V)2 - UHF COMM1"
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].freq = ARC150_device:is_on() and SR.round(ARC150_device:get_frequency(), 5000) or 0
    _data.radios[2].secFreq = ARC150_device:is_on_guard() and 243.0 * 1000000 or 0
    _data.radios[2].modulation = ARC150_device:get_modulation()
    _data.radios[2].volume = ARC150_device:get_volume()

    -- SRT-651/N - COMM2 Radio
    _data.radios[3].name = "SRT-651/N - V/UHF COMM2"
    _data.radios[3].freqMin = 30 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].freq = SRT_651_device:is_on() and SR.round(SRT_651_device:get_frequency(), 5000) or 0
    _data.radios[3].secFreq = SRT_651_device:is_on_guard() and 243.0 * 1000000 or 0
    _data.radios[3].modulation = SRT_651_device:get_modulation()
    _data.radios[3].volume = SRT_651_device:get_volume()

    _data.intercomHotMic = intercom_device:is_hot_mic()

    if intercom_device:is_ptt_pressed() then
        _data.selected = 0
        _data.ptt = true
    elseif ARC150_device:is_ptt_pressed() then
        _data.selected = 1
        _data.ptt = true
    elseif SRT_651_device:is_ptt_pressed() then
        _data.selected = 2
        _data.ptt = true
    else
        _data.ptt = false
    end

    _data.control = 1 -- enables complete radio control

    -- IFF status depend on ident switch as well
    local iff_status
    if iff_device:is_identing() then
        iff_status = 2 -- IDENT
    elseif iff_device:is_working() then
        iff_status = 1 -- NORMAL
    else
        iff_status = 0 -- OFF
    end

    -- IFF trasponder
    _data.iff = {
        status = iff_status,
        mode1 = iff_device:get_mode1_code(),
        mode2=-1,
        mode3 = iff_device:get_mode3_code(),
        -- Mode 4 - not available in real MB-339 but we have decided to include it for gameplay
        mode4 = iff_device:is_mode4_working() > 0,
        control = 0,
        expansion = false
    }

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
        _data.ambient = {vol = 0.15,  abType = 'MB339' }
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'MB339' }
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["MB-339A"] = exportRadioMB339A
        SR.exporters["MB-339APAN"] = exportRadioMB339A
    end,
}
return result
