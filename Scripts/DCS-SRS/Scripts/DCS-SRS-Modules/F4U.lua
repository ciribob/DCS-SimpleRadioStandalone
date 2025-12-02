function exportRadioF4U (_data, SR)

    -- Reference manual: https://www.vmfa251.org/pdffiles/Corsair%20Manual.pdf
    -- p59 of the pdf (p53 for the manual) is the radio section.

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=1,expansion=false,mic=-1}

    local devices = {
        Cockpit = 0,
        Radio = 8
    }

    local buttons = {
        Battery = 120,
        C38_Receiver_A = 82, -- VHF
        C38_Receiver_C = 83, -- MHF
        C30A_CW_Voice = 95,
    }

    local anarc5 = GetDevice(devices.Radio)

    -- TX ON implies RX ON.
    local txOn = anarc5:is_on()
    local vhfOn = SR.getButtonPosition(buttons.C38_Receiver_A) > 0
    local batteryOn = get_param_handle("DC_BUS"):get() > 1 -- SR.getButtonPosition(buttons.Battery) > 0
    local voiceSelected = SR.getButtonPosition(buttons.C30A_CW_Voice) > 0
    local rxOn = txOn or (batteryOn and vhfOn)


    -- AN/ARC-5 Radio
    _data.radios[2].name = "AN/ARC-5"
    _data.radios[2].channel = SR.getSelectorPosition(088, 0.33) + 1
    _data.radios[2].volume = SR.getRadioVolume(devices.Cockpit, 081, {0.0,1.0},false)
    _data.radios[2].rxOnly = rxOn and not txOn
    _data.radios[2].modulation = anarc5:get_modulation()
    _data.radios[2].model = SR.RadioModels.AN_ARC5

    if voiceSelected and (txOn or rxOn) then
        _data.radios[2].freq = SR.round(anarc5:get_frequency(), 5e3) or 0
    end

    _data.selected = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = true

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0; -- no ptt, same as the FW and 109. No connector.

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = get_param_handle("BASE_SENSOR_CANOPY_STATE"):get()

        if _door > 0.5 then
            _data.ambient = {vol = 0.35,  abType = 'f4u' }
        else
            _data.ambient = {vol = 0.15,  abType = 'f4u' }
        end

    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f4u' }
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["F4U-1D"] = exportRadioF4U
        SR.exporters["F4U-1D_CW"] = exportRadioF4U
    end,
}
return result
