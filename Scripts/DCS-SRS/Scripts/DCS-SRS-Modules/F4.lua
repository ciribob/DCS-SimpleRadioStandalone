--for F-4
function exportRadioF4(_data, SR)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "Expansion Radio requires Always allow SRS Hotkeys on. 2nd radio is receive only",
    }

    local ics_devid = 2
    local arc164_devid = 3
    local iff_devid = 4

    local ICS_device = GetDevice(ics_devid)
    local ARC164_device = GetDevice(arc164_devid)
    local IFF_device = GetDevice(iff_devid)

    local intercom_hot_mic = ICS_device:intercom_transmit()
    local ARC164_ptt = ARC164_device:is_ptt_pressed()
    local radio_modulation = ARC164_device:get_modulation()
    local ky28_key = ICS_device:get_ky28_key()
    local is_encrypted = ICS_device:is_arc164_encrypted()

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = ICS_device:get_volume()

    _data.radios[2].name = "AN/ARC-164 COMM"
    _data.radios[2].freq = ARC164_device:is_on() and SR.round(ARC164_device:get_frequency(), 5000) or 1
    _data.radios[2].modulation = radio_modulation
    _data.radios[2].volume = ARC164_device:get_volume()
    if ARC164_device:is_guard_enabled() then
        _data.radios[2].secFreq = 243.0 * 1000000
    else
        _data.radios[2].secFreq = 0
    end
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.950 * 1000000
    _data.radios[2].encKey = ky28_key
    _data.radios[2].enc = is_encrypted
    _data.radios[2].encMode = 2

    -- RECEIVE ONLY RADIO  https://f4.manuals.heatblur.se/systems/nav_com/uhf.html
    _data.radios[3].name = "AN/ARC-164 AUX"
    _data.radios[3].freq = ARC164_device:is_aux_on() and SR.round(ARC164_device:get_aux_frequency(), 5000) or 1
    _data.radios[3].modulation = radio_modulation
    _data.radios[3].volume = ARC164_device:get_aux_volume()
    _data.radios[3].secFreq = 0
    _data.radios[3].freqMin = 265 * 1000000
    _data.radios[3].freqMax = 284.9 * 1000000
    _data.radios[3].encKey = ky28_key
    _data.radios[3].enc = is_encrypted
    _data.radios[3].encMode = 2
    _data.radios[3].rxOnly = true

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-186(V)"
    _data.radios[4].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 121.5 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 116 * 1000000
    _data.radios[4].freqMax = 151.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1

    _data.intercomHotMic = intercom_hot_mic

    if ARC164_ptt then
        _data.selected = 1 -- radios[2] ARC-164
        _data.ptt = true
    else
        _data.selected = -1
        _data.ptt = false
    end

    _data.control = 1 -- full radio

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iff_power = IFF_device:get_is_on()
    local iff_ident = IFF_device:get_ident()

    if iff_power then
        _data.iff.status = 1 -- NORMAL

        if iff_ident then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    else
        _data.iff.status = -1
    end

    _data.iff.mode1 = IFF_device:get_mode1()
    _data.iff.mode2 = IFF_device:get_mode2()
    _data.iff.mode3 = IFF_device:get_mode3()
    _data.iff.mode4 = IFF_device:get_mode4_is_on()

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        -- Pilot_Canopy = 87,
        local _door = SR.getButtonPosition(87)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "f4" }
        else
            _data.ambient = { vol = 0.2, abType = "f4" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f4" }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["F-4E-45MC"] = exportRadioF4
    end,
}
return result
