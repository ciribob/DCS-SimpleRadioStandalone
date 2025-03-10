-- Version 2.1.1.0
-- Make sure you COPY this file to the same location as the Export.lua as well! 
-- Otherwise the Overlay will not work


net.log("Loading - DCS-SRS Overlay GameGUI - Ciribob: 2.1.1.0 ")

local base = _G

package.path  = package.path..";.\\LuaSocket\\?.lua;"..'.\\Scripts\\?.lua;'.. '.\\Scripts\\UI\\?.lua;'
package.cpath = package.cpath..";.\\LuaSocket\\?.dll;"

local JSON = loadfile("Scripts\\JSON.lua")()

module("srs_overlay")

local require           = base.require
local os                = base.os
local io                = base.io
local table             = base.table
local string            = base.string
local math              = base.math
local assert            = base.assert
local pairs             = base.pairs

local lfs               = require('lfs')
local socket            = require("socket") 
local net               = require('net')
local DCS               = require("DCS") 
local U                 = require('me_utilities')
local Skin              = require('Skin')
local Gui               = require('dxgui')
local DialogLoader      = require('DialogLoader')
local Static            = require('Static')
local Tools             = require('tools')

local _modes = {     
    hidden = "hidden",
    minimum = "minimum",
    minimum_vol =  "minimum_vol",
    txrx_only = "txrx_only",
    full = "full",
}

local _isWindowCreated = false
local _listenSocket = {}
local _radioState = {}
local _listStatics = {} -- placeholder objects
local _listMessages = {} -- data




local WIDTH = 420
local HEIGHT = 200

local _lastReceived = 0

local srsOverlay = { 
    connection = nil,
    logFile = io.open(lfs.writedir()..[[Logs\DCS-SRS-InGameRadio.log]], "w")
}


srsOverlay.module_specific = {}

srsOverlay.module_specific["M-2000C"] = function(radios)
		for _i,_radio in pairs(radios) do
			local _isReceiving,_sentBy = srsOverlay.isReceiving(_i)
			if _i==2 then
				if _isReceiving>0 then
					base.Export.GetDevice(19):set_ext_rx(true)
				else
					base.Export.GetDevice(19):set_ext_rx(false)
				end
			elseif _i==3 then
				if _isReceiving>0 then
					base.Export.GetDevice(20):set_ext_rx(true)
				else
					base.Export.GetDevice(20):set_ext_rx(false)
				end
			end
		end
	end


srsOverlay.module_specific["UH-60L"] = function(radios) -- for ARC-201 scanning feature, detects RX-ing
		for _i,_radio in pairs(radios) do
			local _isReceiving,_sentBy = srsOverlay.isReceiving(_i)
			if _i==2 then -- ARC-201 FM1
				if _isReceiving>0 then
					base.Export.GetDevice(2):performClickableAction(20101, 1)
				end
			elseif _i==5 then -- ARC-201 FM2
				if _isReceiving>0 then
					base.Export.GetDevice(9):performClickableAction(20102, 1)
				end
			end
		end
	end

-- Display the COMM cues (inverted 'v' triangles on the channel display when active)
srsOverlay.module_specific["FA-18C_hornet"] = function(radios)
    if DCS.isMultiplayer() then
        local _serverSettings = net.get_server_settings()
        if (_serverSettings.advanced.voice_chat_server == true) then
            -- Do nothing, to avoid potential conflicts with in game VoIP.
            return
        end
    end


    -- THROTTLE_COMMS_X_VOIP
    local commands = {

        -- PRI (COMM1)
        [2] = {
            id = 3046,
            value = 0.2
        },

        -- AUX (COMM2)
        [3] = {
            id = 3047,
            value = 0.4
        },

        -- Currently do nothing, but ready to hit the ground running.
        -- MIDS A
        [4] = {
            id = 3048,
            value = 0.3
        },

        -- MIDS B
        [5] = {
            id = 3049,
            value = 0.1
        }
    }

    
    if srsOverlay.module_specific.fa18queue == nil then
        srsOverlay.module_specific.fa18queue = { }
        -- Initialize the round robin with all radios.
        for _i, _ in pairs(commands) do
            table.insert(srsOverlay.module_specific.fa18queue, _i)
        end
    end
    local roundrobin = srsOverlay.module_specific.fa18queue

    local _hotasDevice = base.Export.GetDevice(13)

    -- Find active radios, clear the state of every one as we go.
    local activeRadios = {}
    local anyActive = false
    for _i, _details in pairs(commands) do
        local _radio = radios[_i]
        if _radio then
            _hotasDevice:SetCommand(_details.id, 0)
            
            local _isReceiving = srsOverlay.isReceiving(_i)
            local _isTransmitting = srsOverlay.isTransmitting(_i, _radio)
            local isActive = _isReceiving > 0 or _isTransmitting
            if isActive then
                activeRadios[_i] = _details
                anyActive = true
            end
        end
    end

    -- Limitation - we can currently only show one active radio at a time.
    -- If multiple radios are transmitting simultaneously, take turn showing the activity indicator.
    if anyActive then

        -- Locate the first radio at the head of the round-robin that happens to be active.
        local priorityRadio = nil
        repeat
            priorityRadio = table.remove(roundrobin)
            table.insert(roundrobin, 1, priorityRadio)
        until activeRadios[priorityRadio]

        _hotasDevice:SetCommand(activeRadios[priorityRadio].id, activeRadios[priorityRadio].value)
    end
end

function srsOverlay.loadConfiguration()
    srsOverlay.log("Loading config file...")
    local tbl = Tools.safeDoFile(lfs.writedir() .. 'Config/SRSConfig.lua', false)
    if (tbl and tbl.config) then
        srsOverlay.log("Configuration exists...")
        srsOverlay.config = tbl.config
    else
        srsOverlay.log("Configuration not found, creating defaults...")
        srsOverlay.config = {
            mode = "full",
            restoreAfterRestart = true,
            hotkey = "Ctrl+Shift+escape",
            windowPosition = { x = 200, y = 200 }
        }
        srsOverlay.saveConfiguration()
    end
    -- migration for config values added during an update
    if srsOverlay.config and srsOverlay.config.restoreAfterRestart == nil then
        srsOverlay.config.restoreAfterRestart = true
        srsOverlay.saveConfiguration()
    end
end

function srsOverlay.saveConfiguration()
    U.saveInFile(srsOverlay.config, 'config', lfs.writedir() .. 'Config/SRSConfig.lua')
end

function srsOverlay.log(str)
    if not str then 
        return
    end

    if srsOverlay.logFile then
        srsOverlay.logFile:write("["..os.date("%H:%M:%S").."] "..str.."\r\n")
        srsOverlay.logFile:flush()
    end
end


function srsOverlay.updateRadio()    


    local _compactMode = base.OptionsData.getPlugin("DCS-SRS","srsOverlayCompactModeEnabled")

    _listMessages = {}

    if _radioState and _radioState.RadioInfo and _radioState.RadioInfo.radios then

		--Call module specific updates
		local _dcs_data = base.Export.LoGetSelfData()
		if _dcs_data and _dcs_data.Name then
			if srsOverlay.module_specific[_dcs_data.Name] ~= nil then
				base.pcall(srsOverlay.module_specific[_dcs_data.Name], _radioState.RadioInfo.radios)
			end
		end
		
		
        if srsOverlay.getMode() == _modes.full and _radioState.ClientCountConnected then
            local clientCountMsg = string.format("Connected clients: %i", _radioState.ClientCountConnected)

            local countMsg = {message = clientCountMsg, skin = typesMessage.normal, height = 20 }

            table.insert(_listMessages, countMsg)
        end

        
        local _radioInfo  =_radioState.RadioInfo

        -- IFF_STATUS:  OFF = 0,  NORMAL = 1 , or IDENT = 2 (IDENT means Blink on LotATC) , 3 DISABLED
        -- M1: -1 = OFF, any other number on
        -- M2: -1 = OFF, any other number on 
        -- M3: -1 = OFF, any other number on 
        -- M4:  1 = ON or 0 = OFF
        -- EXPANSION: only enabled if IFF Expansion is enabled
        -- CONTROL: 0 - COCKPIT / Realistic, 1 - OVERLAY / SRS, 
        --IFF STATUS{"control":1,"expansion":false,"mode1":51,"mode3":7700,"mode4":1,"status":2}

        -- Handle IFF
        if _radioInfo.iff and srsOverlay.getMode() ~= _modes.txrx_only then

            if _radioInfo.iff.status == 1 or _radioInfo.iff.status == 2 then

                local _iff = "TRANS:"

                if _radioInfo.iff.status == 2 then
                    _iff = _iff .. " IDENT"
                end
                
                if _radioInfo.iff.mode1 == -1 then
                     _iff = _iff .. " M1:OFF"
                else
                     _iff = _iff .. string.format(" M1:%02d",_radioInfo.iff.mode1)
                end

                if _radioInfo.iff.mode2 == -1 then
                     _iff = _iff.." M2:OFF"
                else 
                     _iff = _iff .. string.format(" M2:%04d",_radioInfo.iff.mode2)
                end

                if _radioInfo.iff.mode3 == -1 then
                     _iff = _iff .. " M3:OFF"
                else
                     _iff = _iff .. string.format(" M3:%04d",_radioInfo.iff.mode3)
                end

                if _radioInfo.iff.mode4 then
                    _iff = _iff.." M4:ON"
                else
                    _iff = _iff.." M4:OFF"
                end

                 local _iffMsg = {message = _iff, skin = typesMessage.normal, height = 20 }

                table.insert(_listMessages, _iffMsg)

               --    srsOverlay.log("Added IFF Message")
--
            else

                if _radioInfo.iff.status == 0 then
                   -- local _iffMsg = {message = "TRANS: OFF", skin = typesMessage.normal, height = 20 }
                  --  table.insert(_listMessages, _iffMsg)
                      -- srsOverlay.log("Added IFF Message OFF")
                else
                   --    srsOverlay.log("IGNORED IFF Message")
                end
                --ELSE DISABLED so dont show
            end         
        end

        for _i,_radio in pairs(_radioInfo.radios) do

            local fullMessage
			

            local _isReceiving,_sentBy = srsOverlay.isReceiving(_i)
			
		

            if  _radio.modulation == 5 or _radio.modulation == 6 then 

                fullMessage = _radio.name.." - "
                if  _radio.channel > 0 then

                    if _compactMode and (_isReceiving == 1 or _isReceiving == 2) and _sentBy ~= "" then
                        fullMessage = fullMessage .._sentBy
                    else
                       
                        fullMessage = fullMessage.." CHN ".._radio.channel

                        if _radio.retransmit then
                            fullMessage = fullMessage.." RT"
                        end
                    
                        if srsOverlay.getMode() == _modes.minimum_vol or srsOverlay.getMode() == _modes.full  then
                            fullMessage  = fullMessage.." - "..string.format("%.1f", _radio.volume*100).."%"
                        end

                    end

                    local tuned = _radioState.TunedClients

                    if tuned then
                        local tunedRadio = tuned[_i]

                        if tunedRadio > 0 then
                            fullMessage  = fullMessage.." ⚡"..tunedRadio
                        end

                    end
                else
                    fullMessage = fullMessage.." OFF"
                end
            elseif _radio.modulation == 3 then
                fullMessage = ""
                    
            elseif _radio.modulation == 2 then 

                fullMessage = "INTERCOM"

                if srsOverlay.getMode() == _modes.minimum_vol or srsOverlay.getMode() == _modes.full  then
                    fullMessage  = fullMessage.." - "..string.format("%.1f", _radio.volume*100).."%"
                end 
            else
                 fullMessage = _radio.name.." - "

                 if _compactMode and (_isReceiving == 1 or _isReceiving == 2) and _sentBy ~= "" then
                    fullMessage = fullMessage .._sentBy
                 else
                     fullMessage = fullMessage..string.format("%.3f", _radio.freq/1000000.0)

         --            srsOverlay.log( _radio.freq)

                     if _radio.modulation == 0 then
                        fullMessage = fullMessage.." AM"
                     elseif _radio.modulation == 1 then
                        fullMessage = fullMessage.." FM"
                     elseif _radio.modulation == 7 then
                        fullMessage = fullMessage.." SG"
                     elseif _radio.modulation == 4 then
                        fullMessage = fullMessage.." HQ"
                     end

                     if _radio.secFreq > 100 then
                        fullMessage = fullMessage.." G"
                     end

                     if _radio.rxOnly == true then
                        fullMessage = fullMessage.." RX"
                     end

                     if _radio.channel >= 0 then
                        fullMessage = fullMessage.." C".._radio.channel
                     end

                     if _radio.enc and _radio.encKey > 0 then
                        fullMessage = fullMessage.." E".._radio.encKey
                     end

                     if _radio.retransmit then
                        fullMessage = fullMessage.." RT"
                     end

                     if srsOverlay.getMode() == _modes.minimum_vol or srsOverlay.getMode() == _modes.full  then
                        fullMessage  = fullMessage.." - "..string.format("%.1f", _radio.volume*100).."%"
                    end
                end

                local tuned = _radioState.TunedClients

                if tuned then
                    local tunedRadio = tuned[_i]

                    if tunedRadio > 0 then
                        fullMessage  = fullMessage.." ⚡"..tunedRadio
                    end
                end
            end

            local _selected = _i == (_radioInfo.selected+1)

            if _selected then
                fullMessage = fullMessage.." *"
            end

            local transmitting = _radioState.RadioSendingState and
                                    _radioState.RadioSendingState.IsSending and
                                    (_radioState.RadioSendingState.SendingOn == _i-1 or _radio.simul)
            

            if transmitting then
                fullMessage = fullMessage.." +TR"
            end

       --     srsOverlay.log(fullMessage)

            local _skin = typesMessage.normal

            if _isReceiving == 1 then
                _skin = typesMessage.receive
                if not _compactMode then 
                    fullMessage = fullMessage .." ".._sentBy
                end
            elseif  _isReceiving == 2 then
                _skin = typesMessage.guard

                if not _compactMode then 
                    fullMessage = fullMessage .." ".._sentBy
                end
            end

            if srsOverlay.getMode() ~= _modes.txrx_only or _isReceiving > 0 or transmitting then
                local msg = {message = fullMessage, skin =_skin, height = 20 }
                table.insert(_listMessages, msg)
            end
        end
    end

    
    if #_listMessages == 0 and srsOverlay.getMode() == _modes.txrx_only then
        local message = "⚡"
        if _radioState.ClientCountConnected then
            message = string.format("⚡%i", _radioState.ClientCountConnected)
        end
        table.insert(_listMessages, { message = message, skin = typesMessage.normal, height = 20 })
    end

    srsOverlay.paintRadio()
end

function srsOverlay.isReceiving(_radioPos )

    if _radioState.RadioReceivingState then

        for  _i, _rxState in pairs(_radioState.RadioReceivingState) do

            -- off by one in lua
            if _rxState ~= nil and _rxState.ReceivedOn+1 == _radioPos and _rxState.IsReceiving then

                if _rxState.IsSecondary then
                    return 2,_rxState.SentBy
                end

                return 1,_rxState.SentBy
            end
        end

    end

    return 0,""

end

function srsOverlay.isTransmitting(_radioPos, _radio)
    return _radioState.RadioSendingState and
                                    _radioState.RadioSendingState.IsSending and
                                    (_radioState.RadioSendingState.SendingOn+1 == _radioPos or _radio.simul)
end


function srsOverlay.paintRadio()

    local offset = 0
   
    for k,v in pairs(_listStatics) do

        v:setText("")
    end

    local curStatic = 1
    offset = 10 -- 10 offset from top
	
	local enabled = base.OptionsData.getPlugin("DCS-SRS","srsOverlayHelpTextEnabled")

    if #_listMessages == 0 then

		if enabled then
			table.insert(_listMessages, {message = "SRS not connected", skin =typesMessage.guard, height = 20 })
			table.insert(_listMessages, {message = "Connect to an SRS server and join a mission", skin =typesMessage.guard, height = 20 })
			table.insert(_listMessages, {message = "SRS DCS settings:", skin =typesMessage.guard, height = 20 })
			table.insert(_listMessages, {message = "Options -> SPECIAL -> DCS-SRS", skin =typesMessage.guard, height = 20 })
			table.insert(_listMessages, {message = "Toggle (by default) with:", skin =typesMessage.guard, height = 20 })
			table.insert(_listMessages, {message = "LEFT CTRL + LEFT SHIFT + ESC", skin =typesMessage.guard, height = 20 })
		else
			table.insert(_listMessages, {message = "SRS not connected", skin =typesMessage.guard, height = 20 })
		end
    end

    for _i,_msg in pairs(_listMessages) do

        if(_msg~=nil and _msg.message ~= nil and  _listStatics[curStatic] ~= nil ) then
            _listStatics[curStatic]:setSkin(_msg.skin)
            _listStatics[curStatic]:setBounds(10,offset,WIDTH-10,_msg.height)
            _listStatics[curStatic]:setText(_msg.message)

            --10 padding
            offset = offset +20
            curStatic = curStatic +1
        end

    end

end

function srsOverlay.createWindow()
    window = DialogLoader.spawnDialogFromFile(lfs.writedir() .. 'Mods\\Services\\DCS-SRS\\UI\\DCS-SRS-Overlay.dlg', cdata)

    box         = window.Box
    pNoVisible  = window.pNoVisible --PlaceHolder - Not Visible
   -- pDown       = box.pDown

    window:addHotKeyCallback(srsOverlay.config.hotkey, srsOverlay.onHotkey)
    
    window:setVisible(true) -- if you make the window invisible, its destroyed
    
    skinModeFull = pNoVisible.windowModeFull:getSkin()
    skinMinimum = pNoVisible.windowModeMin:getSkin()

    typesMessage =
    {
        normal        = pNoVisible.eYellowText:getSkin(),
        receive       = pNoVisible.eWhiteText:getSkin(),
        guard         = pNoVisible.eRedText:getSkin(),
    }
    
    _listStatics = {}
    
    for i = 1, 8 do
        local staticNew = Static.new()
        table.insert(_listStatics, staticNew)
        box:insertWidget(staticNew)
    end

    w, h = Gui.GetWindowSize()
            
    srsOverlay.resize(w, h)
    
    local enabled = base.OptionsData.getPlugin("DCS-SRS","srsOverlayEnabled")

    if enabled then

        if _modes.hidden == srsOverlay.config.mode then
            -- set to minimum
            srsOverlay.setMode(_modes.minimum)
        else
            srsOverlay.setMode(srsOverlay.config.mode) 
        end

    else
        srsOverlay.setMode(_modes.hidden)
    end

   
    window:addPositionCallback(srsOverlay.positionCallback)     
    srsOverlay.positionCallback()

    _isWindowCreated = true

    srsOverlay.log("SRS Window created")
--
--    srsOverlay.addMessage("124.00 *", "AN/ARC-186(V)", typesMessage.sys)
 --   srsOverlay.addMessage("256.00", "AN/ARC-164 UHF", typesMessage.msg)
  --  srsOverlay.addMessage("10.00", "AN/ARC-186(V)FM", typesMessage.msg)
  --  srsOverlay.addMessage("10.00", "INTERCOM", typesMessage.msg)
end


function srsOverlay.setMode(mode)
    srsOverlay.log("setMode called "..mode)
    srsOverlay.config.mode = mode 
    
    if window == nil then
        return
    end
    
    if srsOverlay.config.mode == _modes.hidden then

        box:setVisible(false)
   --     pDown:setVisible(false)
        window:setSize(0,0) -- Make it tiny!
        window:setHasCursor(false) -- hide cursor

        window:setSkin(Skin.windowSkinChatMin())

    else
        box:setVisible(true)
        window:setSize(WIDTH, HEIGHT)

        if srsOverlay.config.mode == _modes.minimum or srsOverlay.config.mode == _modes.minimum_vol or srsOverlay.config.mode == _modes.txrx_only then

            box:setSkin(skinMinimum)

         --   pDown:setVisible(false)

            window:setSkin(Skin.windowSkinChatMin())

            window:setHasCursor(false) -- hide cursor


            --  DCS.banMouse(false)
        end
        
        if srsOverlay.config.mode == _modes.full then
            box:setSkin(skinModeFull)

            box:setVisible(true)
           -- pDown:setVisible(true)

            window:setSkin(Skin.windowSkinChatWrite())

            window:setHasCursor(true) -- show cursor
        end    
    end

    window:setVisible(true) -- if you make the window invisible, its destroyed

    --force window to re-render if the help text status changed
    if not _radioState or not _radioState.RadioInfo then
        _listMessages = {}
    end

    srsOverlay.paintRadio()
    srsOverlay.saveConfiguration()
end

function srsOverlay.getMode()
    return srsOverlay.config.mode
end

function srsOverlay.onHotkey()

    if (srsOverlay.getMode() == _modes.full) then
        srsOverlay.setMode(_modes.minimum)
    elseif (srsOverlay.getMode() == _modes.minimum) then
        srsOverlay.setMode(_modes.minimum_vol)
    elseif (srsOverlay.getMode() == _modes.minimum_vol) then
        srsOverlay.setMode(_modes.txrx_only)
    elseif (srsOverlay.getMode() == _modes.txrx_only) then
        srsOverlay.setMode(_modes.hidden)
    else
        srsOverlay.setMode(_modes.full)
    end 
end

function srsOverlay.resize(w, h)
    window:setBounds(srsOverlay.config.windowPosition.x, srsOverlay.config.windowPosition.y, WIDTH, HEIGHT)
    box:setBounds(0, 0, WIDTH, HEIGHT)
end

function srsOverlay.positionCallback()
    local x, y = window:getPosition()

    x = math.max(math.min(x, w-WIDTH), 0)
    y = math.max(math.min(y, h-HEIGHT), 0)

    window:setPosition(x, y)

    srsOverlay.config.windowPosition = { x = x, y = y }
    srsOverlay.saveConfiguration()
end



function srsOverlay.initListener()

_listenSocket = socket.udp()

--bind for listening for Radio info
_listenSocket:setsockname("*", 7080)
_listenSocket:settimeout(0) 

end

function srsOverlay.listen()

    -- Receive buffer is 8192 in LUA Socket
    -- will contain 10 clients for LOS
    local _received = _listenSocket:receive()

    if _received then

    --KNOWN BUG - Hitting Left Control + Windows Key + L causes lag
    -- Fix by disabling overlay or hitting L CNTRL + L SHIFT + L
        --if srsOverlay.getMode() ~= _modes.hidden then
            local _decoded = JSON:decode(_received)

          --srsOverlay.log(_received)

            if _decoded then

                _lastReceived  = os.clock()

                _radioState = _decoded

                return true
            end

        --end
    end

    return false
end


function srsOverlay.onSimulationFrame()

    -- if not base.OptionsData then
    --     --srsOverlay.log("NO Options Data")
    --     return
    -- end
	
    if srsOverlay.config == nil then
        srsOverlay.loadConfiguration()
    end

    if not window then

        if _isWindowCreated == false then
            srsOverlay.createWindow()
        end

        -- init connection
        srsOverlay.initListener()
    end

    if srsOverlay.listen() then

        local _status, _result = base.pcall(function() 
            srsOverlay.updateRadio()
        end)

        if not _status then
            srsOverlay.log("Error: ".._result)
        end
        
    else
        local _now = os.clock()

        if _now - _lastReceived > 5 and _radioState and _radioState.RadioInfo then
            _radioState = {}

            --repaint lost radio
            srsOverlay.updateRadio()
        end
    end
	


end 

DCS.setUserCallbacks(srsOverlay)

net.log("Loaded - DCS-SRS Overlay GameGUI - Ciribob: 2.1.1.0 ")
