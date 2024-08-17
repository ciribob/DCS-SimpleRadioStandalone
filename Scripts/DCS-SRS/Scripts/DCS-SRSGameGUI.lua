-- Version 2.1.0.10
-- Make sure you COPY this file to the same location as the Export.lua as well! 
-- Otherwise the Radio Might not work

net.log("Loading - DCS-SRS GameGUI - Ciribob: 2.1.0.10")
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

SRS.sendUpdate = function(playerID)
	local _update = {
		name = "",
		side = 0,
		seat = 0,
		type = "?",
		slot = 0,
	}

	_update.name = net.get_player_info(playerID, "name" )
	_update.side = net.get_player_info(playerID, "side")

	local _player_slot =  net.get_player_info(playerID, 'slot')

    -- Parsing implimentation thanks to Perun and SpecialK
    -- https://github.com/Special-K-s-Flightsim-Bots/DCSServerBot/blob/695a36824d373e4b92a0559f6466ad566c2ad35b/Scripts/net/DCSServerBot/DCSServerBotUtils.lua#L94
	if _player_slot and _player_slot ~= '' then
        if not(string.find(_player_slot, 'red') or string.find(_player_slot, 'blue')) then
            -- Player took model
            _update.slot = _player_slot
            _update.seat = 0
            if (not tonumber(_player_slot)) then
                -- If this is multiseat slot parse master slot and look for seat number
                _t_start, _t_end = string.find(_player_slot, '_%d+')
                if _t_start then
                    -- This is co-player
                    _update.slot = tonumber(string.sub(_player_slot, 0 , _t_start -1 ))
                    _update.seat = tonumber(string.sub(_player_slot, _t_start + 1, _t_end ))
                end
            else
                _update.slot = tonumber(_player_slot)
            end
            _update.type = tostring(DCS.getUnitType(_update.slot))
        else
            -- Deal with the special slots added by Combined Arms and Spectators
            if string.find(_player_slot, 'artillery_commander') then
                _update.type = "artillery_commander"
            elseif string.find(_player_slot, 'instructor') then
                _update.type = "instructor"
            elseif string.find(_player_slot, 'forward_observer') then
                _update.type = "forward_observer"
            elseif string.find(_player_slot, 'observer') then
                _update.type = "observer"
            end
            _update.slot = -1
            _sub_slot = 0
        end
    else
        _update.slot = -1
        _sub_slot = -1
    end

	local _jsonUpdate = SRS.JSON:encode(_update).." \n"
	-- SRS.log("Update -  Slot  ID:"..playerID.." Name: ".._update.name.." Type: ".._update.type.." Slot: ".._update.slot.." Seat: ".._update.seat.." Side: ".._update.side)
	socket.try(SRS.UDPSendSocket:sendto(_jsonUpdate, "127.0.0.1", 5068))
	socket.try(SRS.UDPSendSocket:sendto(_jsonUpdate, "127.0.0.1", 9087))
end

SRS.MESSAGE_PREFIX_OLD = "This server is running SRS on - " -- DO NOT MODIFY!!!
SRS.MESSAGE_PREFIX = "SRS Running @ " -- DO NOT MODIFY!!!

function string.startsWith(string, prefix)
    return string.sub(string, 1, string.len(prefix)) == prefix
end

function string.trim(_str)
    return string.format( "%s", _str:match( "^%s*(.-)%s*$" ) )
end

function SRS.isAutoConnectMessage(msg)
    return string.startsWith(string.trim(msg), SRS.MESSAGE_PREFIX) or string.startsWith(string.trim(msg), SRS.MESSAGE_PREFIX_OLD)
end

function SRS.getHostFromMessage(msg)
	if string.startsWith(string.trim(msg), SRS.MESSAGE_PREFIX_OLD) then
		return string.trim(string.sub(msg, string.len(SRS.MESSAGE_PREFIX_OLD) + 1))
	else
		return string.trim(string.sub(msg, string.len(SRS.MESSAGE_PREFIX) + 1))
	end
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

	local keys =  {"POWER","PWR","M1","M3","M4","IDENT"}

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
    if SRS.CLIENT_ACCEPT_AUTO_CONNECT
                        and from == 1
            and  SRS.isAutoConnectMessage(msg) then
        local host = SRS.getHostFromMessage(msg)
        SRS.log(string.format("Got SRS Auto Connect message: %s", host))

        local enabled = OptionsData.getPlugin("DCS-SRS","srsAutoLaunchEnabled")
        if srs and enabled then
            local path = srs.get_srs_path()
            if path ~= "" then

                net.log("Trying to Launch SRS @ "..path)
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

net.log("Loaded - DCS-SRS GameGUI - Ciribob: 2.1.0.10")

