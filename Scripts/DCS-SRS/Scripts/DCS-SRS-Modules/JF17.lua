local _jf17 = nil
function exportRadioJF17(_data, SR)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    -- reset state on aircraft switch
    if SR.LastKnownUnitId ~= _data.unitId or not _jf17 then
        _jf17 = {
            radios = {
                [2] = {
                    channel = 1,
                    deviceId = 25,
                    volumeKnobId = 934,
                    enc = false,
                    guard = false,
                },
                [3] = {
                    channel = 1,
                    deviceId = 26,
                    volumeKnobId = 938,
                    enc = false,
                    guard = false,
                },
            }
        }
    end

    -- Read ufcp lines.
    local ufcp = {}

    for line=3,6 do
        ufcp[#ufcp + 1] = SR.getListIndicatorValue(line)["txt_win" .. (line - 2)] or ""
    end

    -- Check the last line to see if we're editing a radio (and which one!)
    -- Looking for "123   ." (editing left radio) or ".   123" (right radio)
    local displayedRadio = nil

    -- Most likely case - radio channels being displayed.
    local comm1Channel, comm2Channel = string.match(ufcp[#ufcp], "^(%d%d%d)%s+(%d%d%d)$")
    comm1Channel = tonumber(comm1Channel)
    comm2Channel = tonumber(comm2Channel)
    if comm1Channel == nil or comm2Channel == nil then
        -- Check if we have a radio page up.
        local commDot = nil
        comm1Channel, commDot = string.match(ufcp[#ufcp], "^(%d%d%d)%s+(%.)$")
        comm1Channel = tonumber(comm1Channel)
        if comm1Channel ~= nil and commDot ~= nil then
            -- COMM1 being showed on the UFCP.
            displayedRadio = _jf17.radios[2]
        else
            commDot, comm2Channel = string.match(ufcp[#ufcp], "^(%.)%s+(%d%d%d)$")
            comm2Channel = tonumber(comm2Channel)
            if commDot ~= nil and comm2Channel ~= nil then
                -- COMM2 showed on the UFCP.
                displayedRadio = _jf17.radios[3]
            end
        end
    end

    -- Update channels if we have the info.
    if comm1Channel ~= nil then
        _jf17.radios[2].channel = comm1Channel
    end
    if comm2Channel ~= nil then
        _jf17.radios[3].channel = comm2Channel
    end

    if displayedRadio then
        -- Line 1: encryption.
        -- Treat CMS as fixed frequency encryption only,
        -- TRS as HAVEQUICK (frequency hopping) + encryption.
        -- For encryption, use Line 3 MAST/SLAV to change encryption key.
        if string.match(ufcp[1], "^PLN") then
            displayedRadio.enc = false
            displayedRadio.encKey = nil
            displayedRadio.modulation = nil
        elseif string.match(ufcp[1], "^CMS") then
            displayedRadio.enc = true
            displayedRadio.encKey = string.match(ufcp[3], "MAST$") and 2 or 1
            displayedRadio.modulation = nil
        elseif string.match(ufcp[1], "^TRS") then
            displayedRadio.enc = true
            displayedRadio.encKey = string.match(ufcp[3], "MAST$") and 4 or 3
            -- treat as HAVEQUICK
            displayedRadio.modulation = 4
        elseif string.match(ufcp[1], "^DATA") then
            displayedRadio.enc = false
            displayedRadio.encKey = nil
            -- Forcibly set to DISABLED - Datalink has the radio, can't talk on it!
            displayedRadio.modulation = 3
        end

        -- Look at line 2 for RT+G.
        displayedRadio.guard = string.match(ufcp[2], "^RT%+G%s+") ~= nil
    end

    for radioId=2,3 do
        local state = _jf17.radios[radioId]
        local dataRadio = _data.radios[radioId]
        dataRadio.name = "R&S M3AR COMM" .. (radioId - 1)
        dataRadio.freq = SR.getRadioFrequency(state.deviceId)
        dataRadio.modulation = state.modulation or SR.getRadioModulation(state.deviceId)
        dataRadio.volume = SR.getRadioVolume(0, state.volumeKnobId, { 0.0, 1.0 }, false)
        dataRadio.encMode = 2 -- Controlled by aircraft.
        dataRadio.channel = state.channel

        -- NOTE: Used to be GetDevice(state.deviceId):get_guard_plus_freq(), but that seems borked.
        if state.guard then
            -- Figure out if we want VHF or UHF guard based on current freq.
            dataRadio.secFreq = dataRadio.freq < 224e6 and 121.5e6 or 243e6
        end
        dataRadio.enc = state.enc
        dataRadio.encKey = state.encKey
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "VHF/UHF Expansion"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 115 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.selected = 1
    _data.control = 0; -- partial radio, allows hotkeys



    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local _iff = GetDevice(15)

    if _iff:is_m1_trs_on() or _iff:is_m2_trs_on() or _iff:is_m3_trs_on() or _iff:is_m6_trs_on() then
        _data.iff.status = 1
    end

    if _iff:is_m1_trs_on() then
        _data.iff.mode1 = _iff:get_m1_trs_code()
    else
        _data.iff.mode1 = -1
    end

    if _iff:is_m3_trs_on() then
        _data.iff.mode3 = _iff:get_m3_trs_code()
    else
        _data.iff.mode3 = -1
    end

    _data.iff.mode4 =  _iff:is_m6_trs_on()

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.2 then 
            _data.ambient = {vol = 0.3,  abType = 'jf17' }
        else
            _data.ambient = {vol = 0.2,  abType = 'jf17' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'jf17' }
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["JF-17"] = exportRadioJF17
    end,
}
return result
