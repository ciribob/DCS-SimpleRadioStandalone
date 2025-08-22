function exportRadioKA50(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    local _panel = GetDevice(0)

    _data.radios[2].name = "R-800L14 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(48)

    -- Get modulation mode
    local switch = _panel:get_argument_value(417)
    if SR.nearlyEqual(switch, 0.0, 0.03) then
        _data.radios[2].modulation = 1
    else
        _data.radios[2].modulation = 0
    end
    _data.radios[2].volume = SR.getRadioVolume(0, 353, { 0.0, 1.0 }, false) -- using ADF knob for now

    _data.radios[3].name = "R-828"
    _data.radios[3].freq = SR.getRadioFrequency(49, 50000)
    _data.radios[3].modulation = 1
    _data.radios[3].volume = SR.getRadioVolume(0, 372, { 0.0, 1.0 }, false)
    _data.radios[3].channel = SR.getSelectorPosition(371, 0.1) + 1

    --expansion radios
    _data.radios[4].name = "SPU-9 SW"
    _data.radios[4].freq = 5.0 * 1000000
    _data.radios[4].freqMin = 1.0 * 1000000
    _data.radios[4].freqMax = 10.0 * 1000000
    _data.radios[4].modulation = 0
    _data.radios[4].volume = 1.0
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1

    local switch = _panel:get_argument_value(428)

    if SR.nearlyEqual(switch, 0.0, 0.03) then
        _data.selected = 1
    elseif SR.nearlyEqual(switch, 0.1, 0.03) then
        _data.selected = 2
    elseif SR.nearlyEqual(switch, 0.2, 0.03) then
        _data.selected = 3
    else
        _data.selected = -1
    end

    _data.control = 1;

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'ka50' }
        else
            _data.ambient = {vol = 0.2,  abType = 'ka50' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'ka50' }
    end

    return _data
end

local result = {
   register = function(SR)
  SR.exporters["Ka-50"] = exportRadioKA50
  SR.exporters["Ka-50_3"] = exportRadioKA50
  end
}
return result