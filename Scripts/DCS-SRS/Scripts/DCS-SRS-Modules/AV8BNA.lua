local _av8 = {}
_av8.radio1 = {}
_av8.radio2 = {}
_av8.radio1.guard = 0
_av8.radio1.encKey = -1
_av8.radio1.enc = false
_av8.radio2.guard = 0
_av8.radio2.encKey = -1
_av8.radio2.enc = false

function exportRadioAV8BNA(_data, SR)
    
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    local _ufc = SR.getListIndicatorValue(6)

    --{
    --    "ODU_DISPLAY":"",
    --    "ODU_Option_1_Text":TR-G",
    --    "ODU_Option_2_Text":" ",
    --    "ODU_Option_3_Slc":":",
    --    "ODU_Option_3_Text":"SQL",
    --    "ODU_Option_4_Text":"PLN",
    --    "ODU_Option_5_Text":"CD 0"
    -- }

    --SR.log("UFC:\n"..SR.JSON:encode(_ufc).."\n\n")
    local _ufcScratch = SR.getListIndicatorValue(5)

    --{
    --    "UFC_DISPLAY":"",
    --    "ufc_chnl_1_m":"M",
    --    "ufc_chnl_2_m":"M",
    --    "ufc_right_position":"127.500"
    -- }

    --SR.log("UFC Scratch:\n"..SR.JSON:encode(SR.getListIndicatorValue(5)).."\n\n")

    if SR.LastKnownUnitId ~= _data.unitId then
        _av8.radio1.guard = 0
        _av8.radio2.guard = 0
    end

    local getGuardFreq = function (freq,currentGuard)


        if freq > 1000000 then

            -- check if LEFT UFC is currently displaying the TR-G for this radio
            --and change state if so

            if _ufcScratch and _ufc and _ufcScratch.ufc_right_position then
                local _ufcFreq = tonumber(_ufcScratch.ufc_right_position)

                if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq,1000) then
                    if _ufc.ODU_Option_1_Text == "TR-G" then
                        return 243.0 * 1000000
                    else
                        return 0
                    end
                end
            end


            return currentGuard

        else
            -- reset state
            return 0
        end

    end

    local getEncryption = function ( freq, currentEnc,currentEncKey )
    if freq > 1000000 then

            -- check if LEFT UFC is currently displaying the encryption for this radio
 

            if _ufcScratch and _ufcScratch and _ufcScratch.ufc_right_position then
                local _ufcFreq = tonumber(_ufcScratch.ufc_right_position)

                if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq,1000) then
                    if _ufc.ODU_Option_4_Text == "CIPH" then

                        -- validate number
                        -- ODU_Option_5_Text
                        if string.find(_ufc.ODU_Option_5_Text, "CD",1,true) then

                          local cduStr = string.gsub(_ufc.ODU_Option_5_Text, "CD ", ""):gsub("^%s*(.-)%s*$", "%1")

                            --remove CD and trim
                            local encNum = tonumber(cduStr)

                            if encNum and encNum > 0 then 
                                return true,encNum
                            else
                                return false,-1
                            end
                        else
                            return false,-1
                        end
                    else
                        return false,-1
                    end
                end
            end


            return currentEnc,currentEncKey

        else
            -- reset state
            return false,-1
        end
end



    _data.radios[2].name = "ARC-210 - COMM1"
    _data.radios[2].freq = SR.getRadioFrequency(2)
    _data.radios[2].modulation = SR.getRadioModulation(2)
    _data.radios[2].volume = SR.getRadioVolume(0, 298, { 0.0, 1.0 }, false)
    _data.radios[2].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[2].model = SR.RadioModels.AN_ARC210

    local radio1Guard = getGuardFreq(_data.radios[2].freq, _av8.radio1.guard)

    _av8.radio1.guard = radio1Guard
    _data.radios[2].secFreq = _av8.radio1.guard

    local radio1Enc, radio1EncKey = getEncryption(_data.radios[2].freq, _av8.radio1.enc, _av8.radio1.encKey)

    _av8.radio1.enc = radio1Enc
    _av8.radio1.encKey = radio1EncKey

    if _av8.radio1.enc then
        _data.radios[2].enc = _av8.radio1.enc 
        _data.radios[2].encKey = _av8.radio1.encKey 
    end

    
    -- get channel selector
    --  local _selector  = SR.getSelectorPosition(448,0.50)

    --if _selector == 1 then
    --_data.radios[2].channel =  SR.getSelectorPosition(178,0.01)  --add 1 as channel 0 is channel 1
    --end

    _data.radios[3].name = "ARC-210 - COMM2"
    _data.radios[3].freq = SR.getRadioFrequency(3)
    _data.radios[3].modulation = SR.getRadioModulation(3)
    _data.radios[3].volume = SR.getRadioVolume(0, 299, { 0.0, 1.0 }, false)
    _data.radios[3].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[3].model = SR.RadioModels.AN_ARC210

    local radio2Guard = getGuardFreq(_data.radios[3].freq, _av8.radio2.guard)

    _av8.radio2.guard = radio2Guard
    _data.radios[3].secFreq = _av8.radio2.guard

    local radio2Enc, radio2EncKey = getEncryption(_data.radios[3].freq, _av8.radio2.enc, _av8.radio2.encKey)

    _av8.radio2.enc = radio2Enc
    _av8.radio2.encKey = radio2EncKey

    if _av8.radio2.enc then
        _data.radios[3].enc = _av8.radio2.enc 
        _data.radios[3].encKey = _av8.radio2.encKey 
    end

    --https://en.wikipedia.org/wiki/AN/ARC-210

    -- EXTRA Radio - temporary extra radio
    --https://en.wikipedia.org/wiki/AN/ARC-210
    --_data.radios[4].name = "ARC-210 COM 3"
    --_data.radios[4].freq = 251.0*1000000 --225-399.975 MHZ
    --_data.radios[4].modulation = 0
    --_data.radios[4].secFreq = 243.0*1000000
    --_data.radios[4].volume = 1.0
    --_data.radios[4].freqMin = 108*1000000
    --_data.radios[4].freqMax = 512*1000000
    --_data.radios[4].expansion = false
    --_data.radios[4].volMode = 1
    --_data.radios[4].freqMode = 1
    --_data.radios[4].encKey = 1
    --_data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting


    _data.selected = 1
    _data.control = 0; -- partial radio, allows hotkeys

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.35,  abType = 'av8bna' }
        else
            _data.ambient = {vol = 0.2,  abType = 'av8bna' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'av8bna' }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["AV8BNA"] = exportRadioAV8BNA
    end,
}
return result
