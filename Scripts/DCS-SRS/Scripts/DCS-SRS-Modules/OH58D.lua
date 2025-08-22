local _oh58RetranPersist = nil -- For persistence of retrans variable
function exportRadioOH58D(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "VOX control for intercom volume" }


    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0

    _data.radios[2].name = "AN/ARC-201D FM1"
    _data.radios[2].freq = SR.getRadioFrequency(29)
    _data.radios[2].modulation = SR.getRadioModulation(29)
    _data.radios[2].volMode = 0
    _data.radios[2].encMode = 2

    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(30)
    _data.radios[3].modulation = SR.getRadioModulation(30)
    _data.radios[3].volMode = 0
    _data.radios[3].encMode = 2

    _data.radios[4].name = "AN/ARC-186 VHF"
    _data.radios[4].freq = SR.getRadioFrequency(31)
    _data.radios[4].modulation = SR.getRadioModulation(31)
    _data.radios[4].volMode = 0
    _data.radios[4].encMode = 2


    _data.radios[5].name = "AN/ARC-201D FM2"
    _data.radios[5].freq = SR.getRadioFrequency(32)
    _data.radios[5].modulation = SR.getRadioModulation(32)
    _data.radios[5].volMode = 0
    _data.radios[5].encMode = 2

    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()
    local _hotMic = 0
    local _selector = 0
    local _cyclicICSPtt = false
    local _cyclicPtt = false
    local _footPtt = false

    local _radioDisplay = SR.getListIndicatorValue(8)
    local _mpdRight = SR.getListIndicatorValue(3)
    local _mpdLeft = SR.getListIndicatorValue(4)
    local _activeRadioParamPrefix = nil

    local _getActiveRadio = function () -- Probably a better way to do this, but it works...
        for i = 1, 5 do
            if tonumber(get_param_handle(_activeRadioParamPrefix .. i):get()) == 1 then
                if i >= 5 then
                    return i - 1
                else
                    return i
                end
            end
        end
    end

    if _seat == 0 then
        _data.radios[1].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 187, { 0.0, 0.8 }, false) 
        _data.radios[2].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 175, { 0.0, 0.8 }, false) * SR.getButtonPosition(174)
        _data.radios[3].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 177, { 0.0, 0.8 }, false) * SR.getButtonPosition(176)
        _data.radios[4].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 179, { 0.0, 0.8 }, false) * SR.getButtonPosition(178)
        _data.radios[5].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 183, { 0.0, 0.8 }, false) * SR.getButtonPosition(182)
        -- 186 hotmic
        -- 188 radio selector

        _hotMic = SR.getSelectorPosition(186, 0.1)
        _selector = SR.getSelectorPosition(188, 0.1)

        -- right cyclic ICS (intercom) 1st detent trigger PTT: 400
        -- right cyclic radio 2nd detent trigger PTT: 401
        -- right foot pedal PTT: 404

        _cyclicICSPtt = SR.getButtonPosition(400)
        _cyclicPtt = SR.getButtonPosition(401)
        _footPtt = SR.getButtonPosition(404)

        _activeRadioParamPrefix = 'PilotSelect_vis'

    else
        _data.radios[1].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 830, { 0.0, 0.8 }, false) 
        _data.radios[2].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 814, { 0.0, 0.8 }, false) * SR.getButtonPosition(813)
        _data.radios[3].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 817, { 0.0, 0.8 }, false) * SR.getButtonPosition(816)
        _data.radios[4].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 819, { 0.0, 0.8 }, false) * SR.getButtonPosition(818)
        _data.radios[5].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false) * SR.getRadioVolume(0, 823, { 0.0, 0.8 }, false) * SR.getButtonPosition(822)

        --- 828 Hotmic wheel
        --- 831 radio selector

        _hotMic = SR.getSelectorPosition(828, 0.1)
        _selector = SR.getSelectorPosition(831, 0.1)

        -- left cyclic ICS (intercom) 1st detent trigger PTT: 402
        -- left cyclic radio 2nd detent trigger PTT: 403
        -- left foot pedal PTT: 405

        _cyclicICSPtt = SR.getButtonPosition(402)
        _cyclicPtt = SR.getButtonPosition(403)
        _footPtt = SR.getButtonPosition(405)

        _activeRadioParamPrefix = 'CopilotSelect_vis'
    end

    if _hotMic == 0 or _hotMic == 1 then
        _data.intercomHotMic = true
    end

    -- ACTIVE RADIO START
    _selector = _selector > 7 and 7 or _selector -- Sometimes _selector == 10 on start; clamp to 7
    local _mapSelector = {
        [0] = -1, -- PVT
        [1] = 0, -- Intercom
        [2] = 1, -- FM1
        [3] = 2, -- UHF
        [4] = 3, -- VHF
        [5] = -1, -- Radio not implemented (HF/SATCOM)
        [6] = 4, -- FM2
        [7] = _getActiveRadio() -- RMT
    }
    _data.selected = _mapSelector[_selector]
    -- ACTIVE RADIO END

    -- ENCRYPTION START
    for i = 1, 5 do
        if _radioDisplay == nil then break end -- Probably no battery power so break
            local _radioTranslate = i < 5 and i + 1 or i
            local _radioChannel = _radioDisplay["CHNL" .. i]
            local _channelToEncKey = function ()
                if _radioChannel == 'M' or _radioChannel == 'C' then
                    return 1
                else
                    return tonumber(_radioChannel)
                end
            end

            _data.radios[_radioTranslate].enc = tonumber(get_param_handle('Cipher_vis' .. i):get()) == 1
            _data.radios[_radioTranslate].encKey = _channelToEncKey()

            if _radioChannel ~= 'M' and _radioChannel ~= 'C' then
                _data.radios[_radioTranslate].channel = _data.radios[_radioTranslate].encKey
            end
    end
    -- ENCRYPTION END

    -- FM RETRAN START
    if _mpdLeft ~= nil then
        if _mpdRight["R4_TEXT"] then
            if _mpdRight["R4_TEXT"] == 'FM' and _mpdRight["R4_BORDERCONTAINER"] then
                _oh58RetranPersist = true
            elseif _mpdRight["R4_TEXT"] == 'FM' and _mpdRight["R4_BORDERCONTAINER"] == nil then
                _oh58RetranPersist = false
            end
        end

        if _mpdLeft["R4_TEXT"] then
            if _mpdLeft["R4_TEXT"] == 'FM' and _mpdLeft["R4_BORDERCONTAINER"] then
                _oh58RetranPersist = true
            elseif _mpdLeft["R4_TEXT"] == 'FM' and _mpdLeft["R4_BORDERCONTAINER"] == nil then
                _oh58RetranPersist = false
            end
        end

        if _oh58RetranPersist then
            _data.radios[2].rtMode = 0
            _data.radios[5].rtMode = 0
            _data.radios[2].retransmit = true
            _data.radios[5].retransmit = true
            _data.radios[2].rxOnly = true
            _data.radios[5].rxOnly = true
        end
    end
    -- FM RETRAN END

    if _cyclicICSPtt > 0.5 then
        _data.ptt = true
        _data.selected = 0
    end

    if _cyclicPtt > 0.5  then
        _data.ptt = true
    end

    if _footPtt > 0.5  then
        _data.ptt = true
    end

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on
       _data.ambient = {vol = 0.3,  abType = 'oh58d' }
--       
--       local _door = SR.getButtonPosition(800)
--
--        if _door > 0.2 then 
--            _data.ambient = {vol = 0.35,  abType = 'oh58d' }
--        else
--            _data.ambient = {vol = 0.2,  abType = 'oh58d' }
--        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'oh58d' }
    end

    _data.control = 1

    return _data
end

local result = { }

function result.register(SR)
  SR.exporters["OH58D"] = exportRadioOH58D
end

return result
