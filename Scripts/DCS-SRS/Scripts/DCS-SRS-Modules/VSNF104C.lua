function exportRadioVSNF104C(_data, SR)
    _data.capabilities	= { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    _data.control		= 0 -- full radio			

    -- UHF radio
    local arc66		= {}
    arc66.DeviceId	= 1
    arc66.Frequency	= 0
    arc66.Volume	= GetDevice(0):get_argument_value(506)--TEST-- --0		-- no volume control in cockpit definitions
    arc66.Mode		= { OFF = 0, TR = 1, TRG = 2 }
    arc66.ModeSw	= (GetDevice(0):get_argument_value(505))*2
    arc66.ChannelSw	= GetDevice(0):get_argument_value(302)
    arc66.PttSw		= 0 -- GetDevice(arc66.DeviceId):is_ptt_pressed()
    arc66.Power		= GetDevice(arc66.DeviceId):is_on()
    arc66.Manual	= GetDevice(0):get_argument_value(504)
    --new
    arc66.Channel	= math.floor(arc66.ChannelSw * 100.0)

    -- UHF guard channel
    local guard		= {}
    guard.DeviceId	= 2
    guard.Frequency	= 0

    -- intercom
    local ics		= {}
    ics.DeviceId	= 3
    ics.Frequency	= 0

    local iff		= {}
    iff.DeviceId	= 24	-- defined in devices, but not used by this script
    iff.MstrMode	= { OFF = 0, STBY = 1, LOW = 2, NORM = 3, EMER = 4 }
    iff.MstrModeSw	= (GetDevice(0):get_argument_value(243))*4
    iff.Ident		= { MIC = 0, OUT = 1, IP = 2 }
    iff.IdentSw		= (GetDevice(0):get_argument_value(240))*2
    iff.Mode1Code	= 0	-- not in cockpit definitions yet
    iff.Mode		= { OUT = 0, ON = 1 }
    iff.Mode2Sw		= GetDevice(0):get_argument_value(242)
    iff.Mode3Sw		= GetDevice(0):get_argument_value(241)
    iff.Mode3Code	= 0	-- not in cockpit definitions yet

    if (arc66.PttSw == 1) then
        _data.ptt = true
    end

    if arc66.Power and (arc66.ModeSw ~= arc66.Mode.OFF) then
        arc66.Frequency	= math.floor((GetDevice(arc66.DeviceId):get_frequency() + 5000 / 2) / 5000) * 5000
        ics.Frequency	= 100.0
        --arc66.Volume	= 1.0

        if arc66.ModeSw == arc66.Mode.TRG then
            guard.Frequency = math.floor((GetDevice(guard.DeviceId):get_frequency() + 5000 / 2) / 5000) * 5000
        end
    end

    -- ARC-66 Preset Channel Selector changes interval between channels above Channel 13
    --if (arc66.ChannelSw > 0.45) then
    --	arc66.Channel = math.floor((arc66.ChannelSw - 0.44) * 25.5) + 13
    --else
    --	arc66.Channel = math.floor(arc66.ChannelSw * 28.9)
    --end



    -- Intercom
    _data.radios[1].name		= "Intercom"
    _data.radios[1].freq		= ics.Frequency
    _data.radios[1].modulation	= 2 --Special intercom modulation
    _data.radios[1].volume		= arc66.Volume

    -- ARC-66 UHF radio
    _data.radios[2].name		= "AN/ARC-66"
    _data.radios[2].freq		= arc66.Frequency
    _data.radios[2].secFreq		= guard.Frequency
    _data.radios[2].modulation	= 0  -- AM only
    _data.radios[2].freqMin		= 225.000e6
    _data.radios[2].freqMax		= 399.900e6
    _data.radios[2].volume		= arc66.Volume

    if (arc66.Channel >= 1) and (arc66.Manual == 0) then
        _data.radios[2].channel = arc66.Channel
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = true

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    -- HANDLE TRANSPONDER
    _data.iff = {status=0,mode1=0,mode2=0,mode3=0,mode4=false,control=0,expansion=false}

    if iff.MstrModeSw >= iff.MstrMode.LOW then
        _data.iff.status = 1 -- NORMAL

        if iff.IdentSw == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- MODE set to MIC
        if iff.IdentSw == 0 then
            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT due to MIC switch
            end
        end
    end

    -- IFF Mode 1
    _data.iff.mode1 = iff.Mode1Code

    -- IFF Mode 2
    if iff.Mode2Sw == iff.Mode.OUT then
        _data.iff.mode2 = -1
    end

    -- IFF Mode 3
    _data.iff.mode3 = iff.Mode3Code

    if iff.Mode3Sw == iff.Mode.OUT then
        _data.iff.mode3 = -1
    elseif iff.MstrModeSw == iff.MstrMode.EMER then
        _data.iff.mode3 = 7700
    end

    return _data;
end

local result = {
    register = function(SR)
        SR.exporters["VSN_F104C"] = exportRadioVSNF104C
    end,
}
return result
