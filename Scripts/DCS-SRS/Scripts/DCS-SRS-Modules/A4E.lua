function exportRadioA4E(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    --local intercom = GetDevice(4) --commented out for now, may be useful in future
    local uhf_radio = GetDevice(5) --see devices.lua or Devices.h

    local mainFreq = 0
    local guardFreq = 0

    -- Can directly check the radio device.
    local hasPower = uhf_radio:is_on()

    -- "Function Select Switch" near the right edge controls radio power
    local functionSelect = SR.getButtonPosition(372)

    -- All frequencies are set by the radio in the A-4 so no extra checking required here.
    if hasPower then
        mainFreq = SR.round(uhf_radio:get_frequency(), 5000)

        -- Additionally, enable guard monitor if Function knob is in position T/R+G
        if 0.15 < functionSelect and functionSelect < 0.25 then
            guardFreq = 243.000e6
        end
    end

    local arc51 = _data.radios[2]
    arc51.name = "AN/ARC-51BX"
    arc51.freq = mainFreq
    arc51.secFreq = guardFreq
    arc51.channel = nil -- what is this used for?
    arc51.modulation = 0  -- AM only
    arc51.freqMin = 220.000e6
    arc51.freqMax = 399.950e6

    -- TODO Check if there are other volume knobs in series
    arc51.volume = SR.getRadioVolume(0, 365, {0.2, 0.8}, false)
    if arc51.volume < 0.0 then
        -- The knob position at startup is 0.0, not 0.2, and it gets scaled to -33.33
        arc51.volume = 0.0
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].expansion = true
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-186(V)FM"
    _data.radios[4].freq = 30.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
    _data.radios[4].modulation = 1
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30 * 1000000
    _data.radios[4].freqMax = 76 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true

    _data.control = 0;
    _data.selected = 1


    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(26)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'A4' }
        else
            _data.ambient = {vol = 0.2,  abType = 'A4' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'A4' }
    end

    return _data
end

local result = {
   register = function(SR)
      SR.exporters["A-4E-C"] = exportRadioA4E
  end
}
return result