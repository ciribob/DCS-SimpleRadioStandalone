function exportRadioSU25(_data, SR)
    _data.radios[2].name = "R-862"
    _data.radios[2].freq = 251.0 *
    1000000                                --V/UHF, frequencies are: VHF range of 100 to 149.975 MHz and UHF range of 220 to 399.975 MHz
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 121.5 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 100 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1

    _data.radios[3].name = "R-828"
    _data.radios[3].freq = 30.0 * 1000000 --20 - 60 MHz.
    _data.radios[3].modulation = 1
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 20 * 1000000
    _data.radios[3].freqMax = 59.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        --   local _door = SR.getButtonPosition(181)

        --    if _door > 0.15 then
        _data.ambient = { vol = 0.3, abType = 'su25' }
        --   else
        --      _data.ambient = {vol = 0.2,  abType = 'su25' }
        --    end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = 'su25' }
    end

    return _data
end

local result = {
   register = function(SR)
  SR.exporters["Su-25"] = exportRadioSU25
  SR.exporters["Su-25T"] = exportRadioSU25
  end
}
return result
