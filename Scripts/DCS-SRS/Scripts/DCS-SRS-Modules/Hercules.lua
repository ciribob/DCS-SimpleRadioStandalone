function exportRadioHercules(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = true, desc = "" }
    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=1,expansion=false,mic=-1}

    -- Intercom
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1 -- Overlay control

    -- AN/ARC-164(V) Radio
    -- Use the Pilot's volume for any station other
    -- than the copilot.
    local volumeKnob = 1430 -- PILOT_ICS_Volume_Rot
    if SR.lastKnownSeat == 1 then
        volumeKnob = 1432 -- COPILOT_ICS_Volume_Rot
    end
    local arc164 = GetDevice(18)
    _data.radios[2].name = "AN/ARC-164 UHF"
    if arc164:is_on() then
        _data.radios[2].freq = arc164:get_frequency()
        _data.radios[2].secFreq = 243e6
    else
        _data.radios[2].freq = 0
        _data.radios[2].secFreq = 0
    end
    _data.radios[2].modulation = arc164:get_modulation()

    _data.radios[2].volume = SR.getRadioVolume(0, volumeKnob, { -1.0, 1.0 })
    _data.radios[2].freqMin = 225e6
    _data.radios[2].freqMax = 399.975e6
    _data.radios[2].volMode = 0
    _data.radios[2].freqMode = 0

    -- Expansions - Server Side Controlled
    -- VHF AM - 116-151.975MHz
    _data.radios[3].name = "AN/ARC-186(V) AM"
    _data.radios[3].freq = 124.8e6 
    _data.radios[3].modulation = 0 -- AM
    _data.radios[3].secFreq = 121.5e6
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116e6
    _data.radios[3].freqMax = 151.975e6
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = false

    -- VHF FM - 30-87.975MHz
    _data.radios[4].name = "AN/ARC-186(V) FM"
    _data.radios[4].freq = 30e6
    _data.radios[4].modulation = 1 -- FM
    _data.radios[4].secFreq = 0
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30e6
    _data.radios[4].freqMax = 87.975e6
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = false

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
        _data.ambient = {vol = 0.2,  abType = 'hercules' }
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'hercules' }
    end

    return _data;
end

local result = {
   register = function(SR)
  SR.exporters["Hercules"] = exportRadioHercules
  end
}
return result