function exportRadioC130J30(_data, SR)
    --[[
    TODO:
        - Implement IFF capability
        - Wait for ARC-210 implementation
    ]]

    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "Use COMMON PTT and SPECIAL INTERCOM for HOTAS controls"
    }
    _data.control = 1

    _data.iff = {
        status=0,
        mode1=-1,
        mode2=-1,
        mode3=-1,
        mode4=false,
        control=1,
        expansion=false,
    }

    local UHF1_devid = 7
    local UHF2_devid = 9
    local VHF1_devid = 6
    local VHF2_devid = 8
    local HF1_devid = 10
    local HF2_devid = 11
    local SAT_devid = 91 -- ARC-210

    local function getVHFModulation(radio)
        if radio.freq >= 30000000 and radio.freq <= 87975000 then
            return 1
        elseif radio.freq >= 108000000 and radio.freq <= 115975000 then
            return 0
        elseif radio.freq >= 116000000 and radio.freq <= 151975000 then
            return 0
        elseif radio.freq >= 152000000 and radio.freq <= 173975000 then
            return 1
        else
            return 0
        end
    end

    local function isVHFRxOnly(radio)
        if radio.freq >= 108000000 and radio.freq <= 115975000 then
            return true
        else
            return false
        end
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation

    _data.radios[2].name = "UHF1"
    _data.radios[2].freq = SR.getRadioFrequency(UHF1_devid) or 0
    _data.radios[2].modulation = SR.getRadioModulation(UHF1_devid) or 3

    _data.radios[3].name = "UHF2"
    _data.radios[3].freq = SR.getRadioFrequency(UHF2_devid) or 0
    _data.radios[3].modulation = SR.getRadioModulation(UHF2_devid) or 3

    _data.radios[4].name = "VHF1"
    _data.radios[4].freq = SR.getRadioFrequency(VHF1_devid) or 0
    _data.radios[4].modulation = getVHFModulation(_data.radios[4])
    _data.radios[4].rxOnly = isVHFRxOnly(_data.radios[4])

    _data.radios[5].name = "VHF2"
    _data.radios[5].freq = SR.getRadioFrequency(VHF2_devid) or 0
    _data.radios[5].modulation = getVHFModulation(_data.radios[5])
    _data.radios[5].rxOnly = isVHFRxOnly(_data.radios[5])

    _data.radios[6].name = "HF1"
    _data.radios[6].freq = SR.getRadioFrequency(HF1_devid) or 0
    _data.radios[6].modulation = SR.getRadioModulation(HF1_devid) or 3

    _data.radios[7] = {}
    _data.radios[7].name = "HF2"
    _data.radios[7].freq = SR.getRadioFrequency(HF2_devid) or 0
    _data.radios[7].modulation = SR.getRadioModulation(HF2_devid) or 3

    -- ARC-210 isn't implemented yet - update this when it is.
    _data.radios[8] = {}
    _data.radios[8].name = "SAT"
    _data.radios[8].freq = 269.0 * 1000000
    _data.radios[8].modulation = 0
    _data.radios[8].freqMin = 225.0 * 1000000
    _data.radios[8].freqMax = 399.975 * 1000000
    _data.radios[8].freqMode = 1
    _data.radios[8].encKey = 1
    _data.radios[8].encMode = 1

    -- WIP - Second intercom not supported by SRS currently.
    _data.radios[9] = {}
    _data.radios[9].name = "PVT"
    _data.radios[9].freq = 100.5
    _data.radios[9].modulation = 2

    local _seat = SR.lastKnownSeat -- from 0: P/CP/LM/OBS/LMF

    -- Button definitions
    local _masterVolumeId, _ICSVolumeId, _UHF1VolumeId, _UHF2VolumeId, _VHF1VolumeId, _VHF2VolumeId, _HF1VolumeId, _HF2VolumeId, _SATVolumeId, _PVTVolumeId
    local _TXSelectorId, _ICSSelectorId, _PTTRockerId

    local function handleCockpitButtons()
        local _masterVolume = SR.getRadioVolume(0, _masterVolumeId, { 0.0, 1.0})
        _data.radios[1].volume = SR.getRadioVolume(0, _ICSVolumeId[1], { 0.0, 1.0 }) * SR.getRadioVolume(0, _ICSVolumeId[2], { 0.0, 1.0 }) * _masterVolume -- PULL * ROTARY * MASTER
        _data.radios[2].volume = SR.getRadioVolume(0, _UHF1VolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _UHF1VolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[3].volume = SR.getRadioVolume(0, _UHF2VolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _UHF2VolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[4].volume = SR.getRadioVolume(0, _VHF1VolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _VHF1VolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[5].volume = SR.getRadioVolume(0, _VHF2VolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _VHF2VolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[6].volume = SR.getRadioVolume(0, _HF1VolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _HF1VolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[7].volume = SR.getRadioVolume(0, _HF2VolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _HF2VolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[8].volume = SR.getRadioVolume(0, _SATVolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _SATVolumeId[2], { 0.0, 1.0 }) * _masterVolume
        _data.radios[9].volume = SR.getRadioVolume(0, _PVTVolumeId[1], { 0.0, 1.0 }, false) * SR.getRadioVolume(0, _PVTVolumeId[2], { 0.0, 1.0 }) * _masterVolume

        local _ICSPosition = SR.getSelectorPosition(_ICSSelectorId, 1/3)
        if _ICSPosition > 1 then
            _data.intercomHotMic = true
        else
            _data.intercomHotMic = false
        end

        -- INT:1 -> PVT:9
        local _TXPosition = math.floor(SR.getButtonPosition(_TXSelectorId) * 9 + 0.5) + 1 -- +1 offset to make INT 0

        local _PTTRockerPosition = SR.getButtonPosition(_PTTRockerId)
        if _PTTRockerPosition == 1 then -- Radio rocker
            _data.selected = _TXPosition
            _data.ptt = true
        elseif _PTTRockerPosition == -1 then -- ICS rocker
            _data.selected = 0
            _data.ptt = true
        else
            _data.selected = _TXPosition
            _data.ptt = false
        end
    end

    if _seat == 0 then -- Left Seat
        _masterVolumeId = 1355
        _ICSVolumeId = { 204, 205 } -- PULL, VOLUME
        _UHF1VolumeId = { 222, 223 }
        _UHF2VolumeId = { 224, 225 }
        _VHF1VolumeId = { 212, 213 }
        _VHF2VolumeId = { 214, 215 }
        _HF1VolumeId = { 206, 207 }
        _HF2VolumeId = { 208, 209 }
        _SATVolumeId = { 216, 217 } -- Using the SATCOM knob
        _PVTVolumeId = { 218, 219 }

        _TXSelectorId = 294
        _ICSSelectorId = 293
        _PTTRockerId = 291

        handleCockpitButtons()
    elseif _seat == 1 then -- Right Seat
        _masterVolumeId = 1358 
        _ICSVolumeId = { 226, 227 }
        _UHF1VolumeId = { 244, 245 }
        _UHF2VolumeId = { 246, 247 }
        _VHF1VolumeId = { 234, 235 }
        _VHF2VolumeId = { 236, 237 }
        _HF1VolumeId = { 228, 229 }
        _HF2VolumeId = { 230, 231 }
        _SATVolumeId = { 238, 239 }
        _PVTVolumeId = { 240, 241 }

        _TXSelectorId = 296
        _ICSSelectorId = 295

         _PTTRockerId = 292

         handleCockpitButtons()
    elseif _seat == 2 then -- Load Master
        _data.control = 0
        for _, _radio in pairs(_data.radios) do
            _radio.volMode = 1
        end
    elseif _seat == 3 then -- Aug Seat
        _masterVolumeId = 1361 
        _ICSVolumeId = { 268, 269 }
        _UHF1VolumeId = { 286, 287 }
        _UHF2VolumeId = { 288, 289 }
        _VHF1VolumeId = { 276, 277 }
        _VHF2VolumeId = { 278, 279 }
        _HF1VolumeId = { 270, 271 }
        _HF2VolumeId = { 272, 273 }
        _SATVolumeId = { 280, 281 }
        _PVTVolumeId = { 282, 283 }

        _TXSelectorId = 298
        _ICSSelectorId = 297

        _PTTRockerId = 290

        handleCockpitButtons()
    elseif _seat == 4 then -- Load Master Front
        _data.control = 0
        for _, _radio in pairs(_data.radios) do
            _radio.volMode = 1
        end
    end

    -- DCS only supports 2 engines in export, so we have to do it ourselves.
    local _rampPos = get_param_handle("RAMP"):get()
    local _eng1RPM = get_param_handle("ENG_1_RPM"):get()
    local _eng2RPM = get_param_handle("ENG_2_RPM"):get()
    local _eng3RPM = get_param_handle("ENG_3_RPM"):get()
    local _eng4RPM = get_param_handle("ENG_4_RPM"):get()

    local _engineVolFactor = 0.3 -- Controls how loud the primary engine sounds are
    local _rampVolFactor   = 0.4 -- Controls how much the ramp affects the engines sounds

    local _ambVolume = (((_eng1RPM + _eng2RPM + _eng3RPM + _eng4RPM) / 10) * _engineVolFactor) -- 

    _ambVolume = _ambVolume * ((_rampPos * _rampVolFactor) + 1)

    _data.ambient = {vol = _ambVolume, abType = 'hercules' }

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["C-130J-30"] = exportRadioC130J30
    end,
}
return result
