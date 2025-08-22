-- For better examples see any function like:
--   function SR.exportRadio...(_data)
-- in file DCS-SimpleRadioStandalone.lua

function exportRadioMyModName(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=0,control=1,expansion=false,mic=-1}

    -- COMM1 Radio
    _data.radios[2].name = "AN/ARC-186(V)"
    _data.radios[2].freq = 124.8 * 1000000
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 121.5 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 116 * 1000000
    _data.radios[2].freqMax = 151.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1
    _data.radios[2].expansion = true

    return _data;
end


local result = {
   register = function(SR)
      SR.exporters["MyModName"] = exportRadioMyModName
  end
}
return result