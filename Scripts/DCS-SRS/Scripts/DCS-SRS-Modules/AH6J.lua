function exportRadioAH6J(_data, SR)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    -- All param handles, from Cockpit/Scripts/Radios/
    local devices = {
        AN_ARC210 = 23,
        AN_ARC182 = 24,
        AN_ARC186 = 25,
    }

    local modulation = {
        AM = 0,
        FM = 1,
        INTERCOM = 2,
        DISABLED = 3,
        HAVEQUICK = 4,
        SATCOM = 5,
        MIDS = 6,
        SINCGARS = 7
    }

    local radioPower = get_param_handle("RADIO_POWER_AVAIL"):get() > 0.5
    local anarc210 = {
        device = {
            id = devices.AN_ARC210,
            volumeKnob = 651,
            modeKnob = 652
        },
        power = get_param_handle("ANARC210_POWER"),
        freq = {
            hz1e8 = get_param_handle("ANARC210_FREQ_0"),
            hz1e7 = get_param_handle("ANARC210_FREQ_1"),
            hz1e6 = get_param_handle("ANARC210_FREQ_2"),
            hz1e5 = get_param_handle("ANARC210_FREQ_3"),
            hz1e4 = get_param_handle("ANARC210_FREQ_4"),
            hz1e3 = get_param_handle("ANARC210_FREQ_5"),
        },
        preset = get_param_handle("ANARC210_PRESET"),
        preset10 = get_param_handle("ANARC210_PRESET_1"),
        preset01 = get_param_handle("ANARC210_PRESET_2"),
        modulation = get_param_handle("ANARC210_TC_1"),
        ics = get_param_handle("RADIO3_ICS"),

        isOn = function(self)
            return radioPower and self.power:get() == 1 and SR.getSelectorPosition(self.device.modeKnob, 0.2) == 3
        end,

        getFrequency = function(self)
            local freq = 0
            if self:isOn() then
                for i=3,8 do
                    freq = freq + self.freq['hz1e' .. i]:get() * 10^i
                end
            end

            return freq
        end,

        getModulation = function(self)
            return self.modulation:get() == 0 and modulation.FM or modulation.AM
        end,

        getChannel = function(self)
            if not self:isOn() or self.preset:get() == 0 then
                return -1
            end

            return self.preset10:get() * 10 + self.preset01:get()
        end,

        getVolume = function(self)
            return self.ics:get() * SR.getRadioVolume(0, self.device.volumeKnob)
        end,

        getGuardFrequency = function(self)
            if self:isOn() then
                return 243e6
            end

            return 0
        end,
    }

    local anarc182 = {
         device = {
            id = devices.AN_ARC182,
            volumeKnob = 627,
            amfmSwitch = 626,
            modeKnob = 630,
        },
        valid = get_param_handle("ANARC182_VALID"),
        power = get_param_handle("ANARC182_DISP_POWER"),
        freq = {
            hz1e6 = get_param_handle("ANARC182_MHZ"),
            hz1e5 = get_param_handle("ANARC182_100KHZ"),
            hz1e4 = get_param_handle("ANARC182_10KHZ"),
        },
        ics = get_param_handle("RADIO2_ICS"),

        getMode = function(self)
            return SR.getSelectorPosition(self.device.modeKnob, 1/7)
        end,

        isOn = function(self)
            local mode = self:getMode()
            return radioPower and self.power:get() == 1 and self.valid:get() == 1 and (mode == 2 or mode == 3)
        end,

        getFrequency = function(self)
            local freq = 0
            if self:isOn() then
                for i=4,6 do
                    freq = freq + self.freq['hz1e' .. i]:get() * 10^i
                end
            end

            return freq
        end,

        getModulation = function(self)
            -- Lifted from anarc_182.lua from the mod.
            local frequency = self:getFrequency()
            local mod = modulation.AM
            if frequency < 88e6 then
                mod = modulation.FM
            elseif frequency >= 118e6 and frequency < 156e6 then
                mod = modulation.AM
            elseif frequency >= 225e6 then
                local switch = SR.getButtonPosition(self.device.amfmSwitch)
                mod = switch < 0.5 and modulation.FM or modulation.AM
            end
            
            return mod
        end,

        getChannel = function(self)
            return -1
        end,

         getVolume = function(self)
            -- FIXME: ICS2 is always 0. Just use the radio volume for now.
            return SR.getRadioVolume(0, self.device.volumeKnob)
        end,

        getGuardFrequency = function(self)
            local guard = 0
            local mode = self:getMode()
            if self:isOn() and mode == 3 then
                local freq = self:getFrequency()
                if freq < 88e6 then
                    guard = 40.5e6
                elseif freq < 225e6 then
                    guard = 121.5e6
                else
                    guard = 243e6
                end
            end

            return guard
        end,
    }

    local anarc186 = {
         device = {
            id = devices.AN_ARC186,
            power = 607,
            volumeKnob = 601,
            presetWheel = 608,
            mode = 602,
            freq_hz1e7 = 603,
            freq_hz1e6 = 604,
            freq_hz1e5 = 605,
            freq_hz1e4 = 606
        },
        freq = {
            hz1e6 = get_param_handle("ANARC182_MHZ"),
            hz1e5 = get_param_handle("ANARC182_100KHZ"),
            hz1e4 = get_param_handle("ANARC182_10KHZ"),
        },

        ics = get_param_handle("RADIO1_ICS"),

        isOn = function(self)
            return radioPower and SR.getSelectorPosition(self.device.power, 1/3) == 2
        end,

        getMode = function(self)
            return SR.getSelectorPosition(self.device.mode, 1/3)
        end,

        getFrequency = function(self)
            local freq = 0
            if self:isOn() then
                local mode = self:getMode()
                if mode == 0 then
                    -- EMER FM
                    freq = 40.5e6
                elseif mode == 1 then
                    -- EMER AM
                    freq = 121.5e6
                elseif mode == 2 then
                    -- MAN
                    freq = (SR.getSelectorPosition(self.device.freq_hz1e7, 1/12) + 3) * 1e7
                    freq = freq + SR.getSelectorPosition(self.device.freq_hz1e6, 1/9) * 1e6
                    freq = freq + SR.getSelectorPosition(self.device.freq_hz1e5, 1/9) * 1e5
                    freq = freq + SR.getSelectorPosition(self.device.freq_hz1e4, 1/3) * 1e4
                elseif mode == 3 then
                    -- PRE(?) unsupported atm, can't reliably get the value.
                    freq = 0
                end

            end
            
            return freq
        end,

        getModulation = function(self)
            -- From anarc_186.lua in the mod.
            local frequency = self:getFrequency()
            local mod = modulation.AM
            if frequency < 90e6 then
                mod = modulation.FM
            end
            
            return mod
        end,

        getChannel = function(self)
            if self:getMode() == 3 then
                return SR.getSelectorPosition(self.device.presetWheel, 1/19) + 1
            end
            return -1
        end,

         getVolume = function(self)
            return self.ics:get() * SR.getRadioVolume(0, self.device.volumeKnob)
        end,
    }
    

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0

    _data.radios[2].name = "AN/ARC-186"
    _data.radios[2].freq = anarc186:getFrequency()
    _data.radios[2].modulation = anarc186:getModulation()
    _data.radios[2].volume = anarc186:getVolume()
    _data.radios[2].volMode = 0
    _data.radios[2].channel = anarc186:getChannel()
    _data.radios[2].model = SR.RadioModels.AN_ARC186
    
    _data.radios[3].name = "AN/ARC-182"
    _data.radios[3].freq = anarc182:getFrequency()
    _data.radios[3].modulation = anarc182:getModulation()
    _data.radios[3].volume = anarc182:getVolume()
    _data.radios[3].volMode = 0
    _data.radios[3].channel = anarc182:getChannel()
    _data.radios[3].model = SR.RadioModels.AN_ARC182
    _data.radios[3].secFreq = anarc182:getGuardFrequency()

    _data.radios[4].name = "AN/ARC-210"
    _data.radios[4].freq = anarc210:getFrequency()
    _data.radios[4].modulation = anarc210:getModulation()
    _data.radios[4].volume = anarc210:getVolume()
    _data.radios[4].volMode = 0
    _data.radios[4].channel = anarc210:getChannel()
    _data.radios[4].model = SR.RadioModels.AN_ARC210
    _data.radios[4].secFreq = anarc210:getGuardFrequency()

    _data.selected = get_param_handle("INTERCOM_SELECT"):get() - 1
    if _data.selected < 0 then
        _data.selected = 0
    elseif _data.selected > 3 then
        _data.selected = nil
    end

    local icsModeKnob = 680
    _data.intercomHotMic = SR.getSelectorPosition(icsModeKnob, 1/3) >= 2 -- VOX ON or HOT MIC

    _data.control = 0

    return _data
end

local result = {
    register = function(SR)
        SR.exporters["AH-6J"] = exportRadioAH6J
        SR.exporters["MH-6J"] = exportRadioAH6J
    end,
}
return result
