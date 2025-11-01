function exportRadioMosquitoFBMkVI (_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "SCR522A" 
    _data.radios[2].freq = SR.getRadioFrequency(24)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 364, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.SCR522A

    --TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    if _seat == 0 then

         _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

        local ptt =  SR.getButtonPosition(4)

        if ptt == 1 then
            _data.ptt = true
        end
    else
         _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    end

    _data.radios[3].name = "R1155" 
    _data.radios[3].freq = SR.getRadioFrequency(27,500,true)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 229, { 0.0, 1.0 }, false)
    _data.radios[3].model = SR.RadioModels.R1155

    _data.radios[4].name = "T1154" 
    _data.radios[4].freq = SR.getRadioFrequency(26,500,true)
    _data.radios[4].modulation = 0
    _data.radios[4].volume = 1
    _data.radios[4].volMode = 1
    _data.radios[4].model = SR.RadioModels.T1154


    -- Expansion Radio - Server Side Controlled
    _data.radios[5].name = "AN/ARC-210"
    _data.radios[5].freq = 124.8 * 1000000 
    _data.radios[5].modulation = 0
    _data.radios[5].secFreq = 121.5 * 1000000
    _data.radios[5].volume = 1.0
    _data.radios[5].freqMin = 116 * 1000000
    _data.radios[5].freqMax = 300 * 1000000
    _data.radios[5].volMode = 1
    _data.radios[5].freqMode = 1
    _data.radios[5].expansion = true
    _data.radios[5].model = SR.RadioModels.AN_ARC210

    _data.control = 0; -- no ptt, same as the FW and 109. No connector.
    _data.selected = 1

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(250)
        local _doorRight = SR.getButtonPosition(252)

        if _doorLeft > 0.7 or _doorRight > 0.7 then 
            _data.ambient = {vol = 0.35,  abType = 'mosquito' }
        else
            _data.ambient = {vol = 0.15,  abType = 'mosquito' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'mosquito' }
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["MosquitoFBMkVI"] = exportRadioMosquitoFBMkVI
    end,
}
return result
