function exportRadioF15C(_data, SR)

    _data.radios[2].name = "AN/ARC-164 UHF-1"
    _data.radios[2].freq = 251.0 * 1000000 --225 to 399.975MHZ
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 243.0 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    _data.radios[2].encKey = 1
    _data.radios[2].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.radios[3].name = "AN/ARC-164 UHF-2"
    _data.radios[3].freq = 231.0 * 1000000 --225 to 399.975MHZ
    _data.radios[3].modulation = 0
    _data.radios[3].freqMin = 225 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
    _data.radios[3].model = SR.RadioModels.AN_ARC164


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
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.control = 0;
    _data.selected = 1

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

     --   local _door = SR.getButtonPosition(181)

  --      if _door > 0.2 then 
            _data.ambient = {vol = 0.2,  abType = 'f15' }
      --  else
        --    _data.ambient = {vol = 0.2,  abType = 'f15' }
      --  end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'f15' }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["F-15C"] = exportRadioF15C
    end,
}
return result
