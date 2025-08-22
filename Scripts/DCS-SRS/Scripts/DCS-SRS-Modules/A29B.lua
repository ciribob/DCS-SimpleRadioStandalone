function exportRadioA29B(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    local com1_freq = 0
    local com1_mod = 0
    local com1_sql = 0
    local com1_pwr = 0
    local com1_mode = 2

    local com2_freq = 0
    local com2_mod = 0
    local com2_sql = 0
    local com2_pwr = 0
    local com2_mode = 1

    local _ufcp = SR.getListIndicatorValue(4)
    if _ufcp then
        if _ufcp.com1_freq then com1_freq = (_ufcp.com1_freq * 1000000) end
        if _ufcp.com1_mod then com1_mod = _ufcp.com1_mod * 1 end
        if _ufcp.com1_sql then com1_sql = _ufcp.com1_sql end
        if _ufcp.com1_pwr then com1_pwr = _ufcp.com1_pwr end
        if _ufcp.com1_mode then com1_mode = _ufcp.com1_mode * 1 end

        if _ufcp.com2_freq then com2_freq = (_ufcp.com2_freq * 1000000) end
        if _ufcp.com2_mod then com2_mod = _ufcp.com2_mod * 1 end
        if _ufcp.com2_sql then com2_sql = _ufcp.com2_sql end
        if _ufcp.com2_pwr then com2_pwr = _ufcp.com2_pwr end
        if _ufcp.com2_mode then com2_mode = _ufcp.com2_mode * 1 end
    end

    _data.radios[2].name = "XT-6013 COM1"
    _data.radios[2].modulation = com1_mod
    _data.radios[2].volume = SR.getRadioVolume(0, 762, { 0.0, 1.0 }, false)

    if com1_mode == 0 then
        _data.radios[2].freq = 0
        _data.radios[2].secFreq = 0
    elseif com1_mode == 1 then
        _data.radios[2].freq = com1_freq
        _data.radios[2].secFreq = 0
    elseif com1_mode == 2 then
        _data.radios[2].freq = com1_freq
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    _data.radios[3].name = "XT-6313D COM2"
    _data.radios[3].modulation = com2_mod
    _data.radios[3].volume = SR.getRadioVolume(0, 763, { 0.0, 1.0 }, false)

    if com2_mode == 0 then
        _data.radios[3].freq = 0
        _data.radios[3].secFreq = 0
    elseif com2_mode == 1 then
        _data.radios[3].freq = com2_freq
        _data.radios[3].secFreq = 0
    elseif com2_mode == 2 then
        _data.radios[3].freq = com2_freq
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    _data.radios[4].name = "KTR-953 HF"
    _data.radios[4].freq = 15.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 764, { 0.0, 1.0 }, false)
    _data.radios[4].freqMin = 2 * 1000000
    _data.radios[4].freqMax = 30 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
    _data.radios[4].rtMode = 1

    _data.control = 0 -- Hotas Controls radio
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(26)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = 'a29' }
        else
            _data.ambient = { vol = 0.2, abType = 'a29' }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = 'a29' }
    end

    return _data
end


local result = {
   register = function(SR)
      SR.exporters["A-29B"] = exportRadioA29B
  end
}
return result