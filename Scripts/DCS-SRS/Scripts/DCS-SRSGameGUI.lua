-- Version 2.1.1.0
-- Make sure you COPY this file to the same location as the Export.lua as well! 
-- Otherwise the Radio Might not work

net.log("Loading - DCS-SRS GameGUI - Ciribob: 2.1.1.0")
local SRS = {}

SRS.CLIENT_ACCEPT_AUTO_CONNECT = true --- Set to false if you want to disable AUTO CONNECT

SRS.dbg = {}
SRS.logFile = io.open(lfs.writedir()..[[Logs\DCS-SRS-GameGUI.log]], "w")
function SRS.log(str)
    if SRS.logFile then
        SRS.logFile:write(str.."\n")
        SRS.logFile:flush()
    end
end

package.path  = package.path..";.\\LuaSocket\\?.lua;"
package.cpath = package.cpath..";.\\LuaSocket\\?.dll;"
package.cpath = package.cpath..";"..lfs.writedir().."Mods\\Services\\DCS-SRS\\bin\\?.dll;"

local socket = require("socket")

local srs = nil

pcall(function()
	srs = require("srs")

	SRS.log("Loaded SRS.dll")
end)

if not srs then
	SRS.log("Couldnt load SRS.dll")
end

local JSON = loadfile("Scripts\\JSON.lua")()
SRS.JSON = JSON

SRS.UDPSendSocket = socket.udp()
SRS.UDPSendSocket:settimeout(0)

local _lastSent = 0;

SRS.onPlayerChangeSlot = function(_id)

    -- send when there are changes
    local _myPlayerId = net.get_my_player_id()

    if _id == _myPlayerId then
        SRS.sendUpdate(net.get_my_player_id())
    end
  
end

SRS.onSimulationFrame = function()

    local _now = DCS.getRealTime()

    -- send every 5 seconds
    if _now > _lastSent + 5.0 then
        _lastSent = _now 
     --    SRS.log("sending update")
        SRS.sendUpdate(net.get_my_player_id())
    end

end

-- Slot Data Examples:
-- 3271
-- 12122
-- 13312_2
-- 13312_3
-- forward_observer_blue_6
-- artillery_commander_blue_1
-- instructor_red_1
-- Spectators are Null

SRS.sendUpdate = function(playerID)
  
    local _update = {
        name = "",
        side = 0,
        seat = 0,
		slotNum = 0,
		slotName = "?",
    }

    _update.name = net.get_player_info(playerID, "name" )
	_update.side = net.get_player_info(playerID, "side")

	local rawSlot =  net.get_player_info(playerID, "slot")

	if rawSlot and rawSlot ~= '' then
		slot = tostring(rawSlot)
	    
	    -- If there is an Underscore in the Slot, then special cases:
	    if string.find(tostring(slot), "_", 1, true) then
            -- Deal with the special slots added by Combined Arms, Else Aircrew.
            if string.find(rawSlot, 'artillery_commander') then
                _update.slotNum = 0
                _update.slotName = "Tactical Cmdr"
            elseif string.find(rawSlot, 'instructor') then
                _update.slotNum = 0
                _update.slotName = "Game Master" --"Game Master"
            elseif string.find(rawSlot, 'forward_observer') then
                _update.slotNum = 0
                _update.slotName = "JTAC/Operator" -- "JTAC"
            elseif string.find(rawSlot, 'observer') then
                _update.slotNum = 0
                _update.slotName = "Observer"
            else
                -- extract Slot # from the start.
                slotBeginning = string.sub(slot, 0, string.find(slot, "_", 1, true))
                if slotBeginning ~= nil then
                    _update.slotNum = tonumber(slotBeginning)
                    _update.slotName = "Aircrew"
                end
            end
            
            -- Slot 2744_2 -- backseat slot is Unit ID  _2
            -- forward_observer_blue_6 -- Seat 6
            
            --extract ending substring - get the seat ID
            local last_underscore = string.find(string.reverse(slot), "_", 1, true)-2
            slotEnding = string.sub(slot, string.len(slot)-last_underscore, string.len(slot))

            seatNum = tonumber(slotEnding)

            if seatNum ~= nil and seatNum >= 1 then
                _update.seat = seatNum -1 -- -1 as seat starts at 2
            end
        else
            -- Slot Data is valid, but does not have an Underscore.
            _update.slotNum = slot
            _update.slotName = "Pilot"
        end
    else
        -- If Slot info is invalid, we must be a Spectator or similar.
        _update.slotName = "Spectator"
	end

	local _jsonUpdate = SRS.JSON:encode(_update).." \n"
    --SRS.log("Update -  Slot  ID:"..playerID.." Name: ".._update.name.." Side: ".._update.side.." Seat: ".._update.seat.." slot #: ".._update.slotNum.." slotName: ".._update.slotName)
	socket.try(SRS.UDPSendSocket:sendto(_jsonUpdate, "127.0.0.1", 5068))
	socket.try(SRS.UDPSendSocket:sendto(_jsonUpdate, "127.0.0.1", 9087))
end

SRS.MESSAGE_PATTERN_OLDER = "This server is running SRS on - (%d+%.%d+%.%d+%.%d+:?%d*)" -- DO NOT MODIFY!!!
SRS.MESSAGE_PATTERN_OLD = "SRS Running @ (%d+%.%d+%.%d+%.%d+:?%d*)"                     -- DO NOT MODIFY!!!
SRS.MESSAGE_PATTERN = "SRS Running on (%d+)"                                            -- DO NOT MODIFY!!!

function string.startsWith(string, prefix)
    return string.sub(string, 1, string.len(prefix)) == prefix
end

function string.trim(_str)
    return string.format( "%s", _str:match( "^%s*(.-)%s*$" ) )
end

function SRS.getPortFromMessage(msg)
	return msg:match(SRS.MESSAGE_PATTERN)
end

function SRS.getHostFromMessage(msg)
	local host = msg:match(SRS.MESSAGE_PATTERN_OLD)
	if host ~= nil then return host end

	return msg:match(SRS.MESSAGE_PATTERN_OLDER)
end

-- Register callbacks --

SRS.sendConnect = function(_message)
	socket.try(SRS.UDPSendSocket:sendto(_message.."\n", "127.0.0.1", 5069))
end

SRS.sendCommand = function(_message)

    socket.try(SRS.UDPSendSocket:sendto(SRS.JSON:encode(_message).."\n", "127.0.0.1", 9040))
   
end

SRS.findCommandValue = function(key, list)

	for index,str in ipairs(list) do
			
		if str == key then
			
			return list[index+1]
		end
	end
	return nil
end

SRS.handleTransponder = function(msg)

	local transMsg = msg:gsub(':',' ')

	local split = {}
	for token in string.gmatch(transMsg, "[^%s]+") do
	 
	  table.insert(split,token)
	
	end

	local keys =  {"POWER","PWR","M1","M2","M3","M4","IDENT"}

	local commands = {}

	--search for keys
	for _,key in ipairs(keys) do

		local val = SRS.findCommandValue(key, split)

		if val then
			if key == "POWER" or key == "PWR" then
				if val == "ON" then
					table.insert(commands, {Command = 6, Enabled = true})
				elseif val == "OFF" then
					table.insert(commands, {Command = 6, Enabled = false})
				end
			elseif key == "M1" then

				if val == "OFF" then
					table.insert(commands, {Command = 7, Code = -1})
				else
					local code = tonumber(val)

					if code ~= nil then
						table.insert(commands, {Command = 7, Code = code})
					end
				end			
				elseif key == "M2" then
					 if val == "OFF" then
						--Command 13 matches to Enum in the UDPInterfaceCommand Enum
						table.insert(commands, {Command = 13, Code = -1})
					else
						local code = tonumber(val)
	   
						if code ~= nil then
							--Command 13 matches to Enum in the UDPInterfaceCommand Enum
							table.insert(commands, {Command = 13, Code = code})
						end
					end
				
			elseif key == "M3" then
				 if val == "OFF" then
					table.insert(commands, {Command = 8, Code = -1})
				else
					local code = tonumber(val)

					if code ~= nil then
						table.insert(commands, {Command = 8, Code = code})
					end
				end

			elseif key == "M4" then
				if val == "ON" then
					table.insert(commands, {Command = 9, Enabled = true})
				elseif val == "OFF" then
					table.insert(commands, {Command = 9, Enabled = false})
				end
			elseif key == "IDENT" then
				if val == "ON" then
					table.insert(commands, {Command = 10, Enabled = true})
				elseif val == "OFF" then
					table.insert(commands, {Command = 10, Enabled = false})
				end
			end
		end
	end

	return commands

end


SRS.handleRadio = function(msg)

	local transMsg = msg:gsub(':',' ')

	local split = {}
	for token in string.gmatch(transMsg, "[^%s]+") do
	 
	  table.insert(split,token)
	
	end

	local keys =  {"SELECT",
					"RADIO","FREQ","GUARD",
					"FREQUENCY","GRD","FRQ", "VOL","VOLUME","CHANNEL","CHN"}

	local commands = {}

	local radioId = -1

	--search for keys
	for _,key in ipairs(keys) do

		local val = SRS.findCommandValue(key, split)

		if val then
			if key == "SELECT" or key == "RADIO" then

				local code = tonumber(val)
				if code ~= nil then
					radioId  = code
					if key == "SELECT" then
						table.insert(commands, {Command = 1, RadioId = radioId})
					end
				end

			elseif key == "FREQ" or key == "FREQUENCY" or key == "FRQ" then

				if radioId > 0 then
					local frq = tonumber(val)

					if frq ~= nil then
						table.insert(commands, {Command = 12,  RadioId = radioId, Frequency = frq})
					end
				end
			elseif key == "VOL" or key == "VOLUME" then

				if radioId > 0 then
					local vol = tonumber(val)

					if vol ~= nil then

						if vol > 1.0 then
							vol = 1.0
						elseif vol < 0 then
							vol = 0
						end

						table.insert(commands, {Command = 5,  RadioId = radioId, Volume = vol})
					end
				end
			elseif key == "CHN" or key == "CHANNEL" then

				if radioId > 0 then
					if val == "UP" or val == "+" then
						table.insert(commands, {Command = 3,  RadioId = radioId})
					elseif val == "DOWN" or val == "-" then
						table.insert(commands, {Command = 4,  RadioId = radioId})
					end
				end
			elseif key == "GUARD" or key == "GRD" then
				if val == "ON" then
					table.insert(commands, {Command = 11, Enabled = true, RadioId = radioId})
				elseif val == "OFF" then
					table.insert(commands, {Command = 11, Enabled = false, RadioId = radioId})
				end
			end
		end
	end

	if radioId > 0 then
		return commands
	end

	return {}

end

SRS.onChatMessage = function(msg, from)
	-- Only accept auto connect message coming from host.
	if SRS.CLIENT_ACCEPT_AUTO_CONNECT and from == 1 then
		local host = nil
		local port = SRS.getPortFromMessage(msg)
		if port ~= nil then 
			local ip = net.get_server_host()
			host = ip .. ':' .. port
		else
			host = SRS.getHostFromMessage(msg)
		end
		if host == nil then 
			SRS.log("Error getting host from message: " .. msg)
			return
		end

		SRS.log(string.format("Got SRS Auto Connect message: %s", host))

		local enabled = OptionsData.getPlugin("DCS-SRS", "srsAutoLaunchEnabled")
		if srs and enabled then
			local path = srs.get_srs_path()
			if path ~= "" then
				net.log("Trying to Launch SRS @ " .. path)
				srs.start_srs(host)
			end
		end
		SRS.sendConnect(host)
	end

    -- MESSAGE FROM MYSELF
    if from == net.get_my_player_id() then
		
		msg = msg:upper()

		if string.find(msg,"SRSTRANS",1,true) then
			local commands = SRS.handleTransponder(msg) 

			for _,command in pairs(commands) do
				SRS.sendCommand(command)
			end
		elseif string.find(msg,"SRSRADIO",1,true) then
			local commands = SRS.handleRadio(msg) 

			for _,command in pairs(commands) do
				SRS.sendCommand(command)
			end
		end
	end

end


DCS.setUserCallbacks(SRS)

net.log("Loaded - DCS-SRS GameGUI - Ciribob: 2.1.1.0")

