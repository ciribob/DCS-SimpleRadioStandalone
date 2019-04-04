-- Version 1.5.9.0
-- Make sure you COPY this file to the same location as the Export.lua as well!
-- Otherwise the Overlay will not work

local base = _G

package.path = package.path..";.\\LuaSocket\\?.lua;"..'.\\Scripts\\?.lua;'.. '.\\Scripts\\UI\\?.lua;'
package.cpath = package.cpath..";.\\LuaSocket\\?.dll;"

local JSON = loadfile("Scripts\\JSON.lua")()

module("srs_overlay")

local require = base.require
local os = base.os
local io = base.io
local table = base.table
local string = base.string
local math = base.math
local assert = base.assert
local pairs = base.pairs

local lfs = require('lfs')
local socket = require("socket")
local net = require('net')
local DCS = require("DCS")
local U = require('me_utilities')
local Skin = require('Skin')
local Gui = require('dxgui')
local DialogLoader = require('DialogLoader')
local Static = require('Static')
local Tools = require('tools')

local _modes = {
	hidden = "hidden",
	minimum = "minimum",
	minimum_vol =  "minimum_vol",
	full = "full",
}

local _isWindowCreated = false
local _listenSocket = {}
local _radioState = {}
local _listStatics = {} -- placeholder objects
local _listMessages = {} -- data

local WIDTH = 350
local HEIGHT = 140

local _lastReceived = 0

local srsOverlay = {
	connection = nil,
	logFile = io.open(lfs.writedir()..[[Logs\DCS-SRS-InGameRadio.log]], "w")
}

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

	_listMessages = {}

	if _radioState and _radioState.RadioInfo and _radioState.RadioInfo.radios then

		if srsOverlay.getMode() == _modes.full and _radioState.ClientCountConnected and _radioState.ClientCountIngame then
			local clientCountMsg = string.format("Connected clients: %i (%i ingame)", _radioState.ClientCountConnected, _radioState.ClientCountIngame)

			local countMsg = {message = clientCountMsg, skin = typesMessage.normal, height = 20 }

			table.insert(_listMessages, countMsg)
		end

		local _radioInfo  =_radioState.RadioInfo

		for _i,_radio in pairs(_radioInfo.radios) do

			local fullMessage

			if _radio.modulation == 3 then
					fullMessage = ""
					
			elseif _radio.modulation == 2 then 

					fullMessage = "INTERCOM "
				
			else
					fullMessage = _radio.name.." - "

					fullMessage = fullMessage..string.format("%.3f", _radio.freq/1000000.0)

		-- srsOverlay.log( _radio.freq)

					if _radio.modulation == 0 then
						fullMessage = fullMessage.." AM"
					else
						fullMessage = fullMessage.." FM"
					 end

					if _radio.secFreq > 100 then
						fullMessage = fullMessage.." G"
					end

					if _radio.channel >= 0 then
						fullMessage = fullMessage.." C".._radio.channel
					end

					if _radio.enc and _radio.encKey > 0 then
						fullMessage = fullMessage.." E".._radio.encKey
					end

					if srsOverlay.getMode() == _modes.minimum_vol or srsOverlay.getMode() == _modes.full  then
						fullMessage  = fullMessage.." - "..string.format("%.1f", _radio.volume*100).."%"
					end
			end

			local _selected = _i == (_radioInfo.selected+1)

			if _selected then
				fullMessage = fullMessage.." *"

				if _radioState.RadioSendingState
						and _radioState.RadioSendingState.SendingOn == _i -1
						and _radioState.RadioSendingState.IsSending then

					fullMessage = fullMessage.." +TR"
				end
			end

		-- srsOverlay.log(fullMessage)

			local _skin = typesMessage.normal

			local _isReceiving = srsOverlay.isReceiving(_i)

			if _isReceiving == 1 then
				_skin = typesMessage.receive
			elseif  _isReceiving == 2 then
				_skin = typesMessage.guard
			end


			local msg = {message = fullMessage, skin =_skin, height = 20 }


			table.insert(_listMessages, msg)
		end
	end

	srsOverlay.paintRadio()
end

function srsOverlay.isReceiving(_radioPos )

	if _radioState.RadioReceivingState then

		for  _i, _rxState in pairs(_radioState.RadioReceivingState) do

			-- off by one in lua
			if _rxState ~= nil and _rxState.ReceivedOn+1 == _radioPos and _rxState.IsReceiving then

				if _rxState.IsSecondary then
					return 2
				end

				return 1
			end
		end

	end

	return 0

end


function srsOverlay.paintRadio()

	local offset = 0
   
	for k,v in pairs(_listStatics) do

		v:setText("")
	end

	local curStatic = 1
	offset = 10 -- 10 offset from top

	if #_listMessages == 0 then
		table.insert(_listMessages, {message = "No Radio Connected", skin =typesMessage.guard, height = 20 })
		table.insert(_listMessages, {message = "Connect to SRS server and", skin =typesMessage.guard, height = 20 })
		table.insert(_listMessages, {message = "start or join a mission", skin =typesMessage.guard, height = 20 })
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
	window = DialogLoader.spawnDialogFromFile(lfs.writedir() .. 'Scripts\\DCS-SRS-Overlay.dlg', cdata)

	box = window.Box
	pNoVisible = window.pNoVisible --PlaceHolder - Not Visible
	-- pDown = box.pDown

	window:addHotKeyCallback(srsOverlay.config.hotkey, srsOverlay.onHotkey)


	skinModeFull = pNoVisible.windowModeFull:getSkin()
	skinMinimum = pNoVisible.windowModeMin:getSkin()

	typesMessage = {
		normal = pNoVisible.eYellowText:getSkin(),
		receive = pNoVisible.eWhiteText:getSkin(),
		guard = pNoVisible.eRedText:getSkin(),
	}


	_listStatics = {}
    
	for i = 1, 5 do
		local staticNew = Static.new()
		table.insert(_listStatics, staticNew)
		box:insertWidget(staticNew)
	end

	w, h = Gui.GetWindowSize()
            
	srsOverlay.resize(w, h)
    
	srsOverlay.setMode(srsOverlay.config.mode)    
    
	window:addPositionCallback(srsOverlay.positionCallback)     
	srsOverlay.positionCallback()

	_isWindowCreated = true

	srsOverlay.log("SRS Window created")

	-- srsOverlay.addMessage("124.00 *", "AN/ARC-186(V)", typesMessage.sys)
	-- srsOverlay.addMessage("256.00", "AN/ARC-164 UHF", typesMessage.msg)
	-- srsOverlay.addMessage("10.00", "AN/ARC-186(V)FM", typesMessage.msg)
	-- srsOverlay.addMessage("10.00", "INTERCOM", typesMessage.msg)
end

function srsOverlay.setVisible(b)
	window:setVisible(b)
end

function srsOverlay.setMode(mode)
	srsOverlay.log("setMode called "..mode)
	srsOverlay.config.mode = mode 
  
	if window == nil then
		return
	end

	srsOverlay.setVisible(true) -- if you make the window invisible, its destroyed

	if srsOverlay.config.mode == _modes.hidden then

		box:setVisible(false)
	--  pDown:setVisible(false)
		window:setSize(0,0) -- Make it tiny!
		window:setHasCursor(false) -- hide cursor

		window:setSkin(Skin.windowSkinChatMin())

	else
		box:setVisible(true)
		window:setSize(WIDTH, HEIGHT)

		if srsOverlay.config.mode == _modes.minimum or srsOverlay.config.mode == _modes.minimum_vol then

			box:setSkin(skinMinimum)

		--  pDown:setVisible(false)

			window:setSkin(Skin.windowSkinChatMin())

			window:setHasCursor(false) -- hide cursor


			-- DCS.banMouse(false)
		end

		if srsOverlay.config.mode == _modes.full then
			box:setSkin(skinModeFull)

			box:setVisible(true)
		--  pDown:setVisible(true)

			window:setSkin(Skin.windowSkinChatWrite())

			window:setHasCursor(true) -- show cursor
		end
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

function srsOverlay.show(b)
	if _isWindowCreated == false then
		srsOverlay.createWindow()
	end
    
	if b == false then
		srsOverlay.saveConfiguration()
	end
    
	srsOverlay.setVisible(b)
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
		if srsOverlay.getMode() ~= _modes.hidden then
			local _decoded = JSON:decode(_received)

		--srsOverlay.log(_received)

			if _decoded then

				_lastReceived  = os.clock()

				_radioState = _decoded

				return true
			end

		end
	end

	return false
end


function srsOverlay.onSimulationFrame()
	if srsOverlay.config == nil then
		srsOverlay.loadConfiguration()
	end

	if not window then
		srsOverlay.show(true)

		if srsOverlay.config and srsOverlay.config.restoreAfterRestart then
			if srsOverlay.config.mode then
				srsOverlay.log("Restoring SRS window in " .. srsOverlay.config.mode .. " mode...")
				srsOverlay.setMode(srsOverlay.config.mode)
			else
				srsOverlay.log("Restoring SRS window in fallback full mode...")
				srsOverlay.setMode(_modes.full)
			end
		else
			srsOverlay.log("Creating SRS window hidden...")
			srsOverlay.setMode(_modes.hidden)
		end

		-- init connection
		srsOverlay.initListener()
	end

	if srsOverlay.listen() then

		srsOverlay.updateRadio()
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

net.log("Loaded - DCS-SRS Overlay GameGUI - Ciribob - 1.5.9.0 ")
