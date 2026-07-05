-- SEEKSILENCE encryption keystate declared outside of function so its state isn't reset each tick
local zeroized = 0

function exportRadioF100D(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "AN/ARC-34"
    _data.radios[2].freq = SR.getRadioFrequency(4)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 363, {0.2, 0.8}, false)
    _data.radios[2].freqMin = 225.000e6
    _data.radios[2].freqMax = 399.900e6
    _data.radios[2].model = SR.RadioModels.AN_ARC51BX

    -- get preset channel selector
    local _channel = SR.getSelectorPosition(360, 0.05) + 1

    if _channel >= 1 then
        _data.radios[2].channel = _channel
    end

    _data.selected = 1

    -- guard mode for UHF Radio
    local functionDial = SR.getSelectorPosition(361, 0.1)
    -- Function dial positions: 0=OFF, 1=T/R, 2=T/R+G, 3=DF
    if functionDial == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    -- Check PTT
    if SR.getButtonPosition(18) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
    end

    -- SEEKSILENCE encryption Not working
    if SR.getButtonPosition(344, 0.1) > 0.5 then
        zeroized = 1 --updates line 2
    end
    local _SEEKSILENCEPower = SR.round(SR.getButtonPosition(340), 0.1)

    -- 341 = Mode switch CRAD1
    if _SEEKSILENCEPower > 0.5 and SR.round(SR.getButtonPosition(341), 0.1) == 0.1 and zeroized == 0 then
        _data.radios[2].encKey = 1 -- need a get_key() on deviceID 23 to read this. Also no options in missin editor to set this for F-100, so just defaulting to 1 for now
        _data.radios[2].enc = true
    end


    -- IFF / Transponder (APX-72)
    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local iffMaster = SR.getSelectorPosition(519, 0.1)
    -- Master knob: 0=OFF, 1=STBY, 2=ON, 3=EMERG

    if iffMaster >= 2 then
        _data.iff.status = 1 -- NORMAL

        local iffIdent = SR.getButtonPosition(533)
        if iffIdent > 0.5 then
            _data.iff.status = 2 -- IDENT
        end

        -- MIC mode tie-in: if audio switch is in MIC position,
        -- transmitting on UHF also triggers IDENT
        local iffAudio = SR.getSelectorPosition(520, 0.1)
        if iffAudio == 1 and _data.ptt and _data.selected == 2 then
            _data.iff.status = 2
        end
    end

    -- Mode 1 code: two octal wheels (arg 527: 8 pos, arg 528: 4 pos) numbers are reversed, so subtract from 7 and 3
    local m1w1 = 7 - SR.getSelectorPosition(527, 0.125)
    local m1w2 = 3 - SR.getSelectorPosition(528, 0.125)
    _data.iff.mode1 = m1w1 * 10 + m1w2

    -- Mode 3A code: four octal wheels (args 529-532, 8 pos each) numbers are reversed, so subtract from 7
    local m3w1 = 7 - SR.getSelectorPosition(529, 0.125)
    local m3w2 = 7 - SR.getSelectorPosition(530, 0.125)
    local m3w3 = 7 - SR.getSelectorPosition(531, 0.125)
    local m3w4 = 7 - SR.getSelectorPosition(532, 0.125)
    _data.iff.mode3 = m3w1 * 1000 + m3w2 * 100 + m3w3 * 10 + m3w4

    if iffMaster == 3 then
        _data.iff.mode3 = 7700 -- EMERG
    end

    -- Mode 4 switch (arg 526)
    local mode4On = SR.getButtonPosition(526)
    if mode4On > 0.5 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = true
    _data.radios[3].model = SR.RadioModels.AN_ARC186

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0

    if SR.getAmbientVolumeEngine() > 10 then
        local _door = SR.getButtonPosition(38)
        if _door > 0.1 then
            _data.ambient = {vol = 0.35, abType = 'f100'}
        else
            _data.ambient = {vol = 0.15, abType = 'f100'}
        end
    else
        _data.ambient = {vol = 0, abType = 'f100'}
    end

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["F-100D"] = exportRadioF100D
    end,
}
return result
