-- Version 1.2.0.0
-- Make sure you COPY this file to the same location as the Export.lua as well! 
-- Otherwise the Radio Might not work
local SRS = {}

SRS.unicast = true

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

local socket = require("socket")

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
    }

    _update.name = net.get_player_info(playerID, "name" )
	_update.side = net.get_player_info(playerID,"side")

    --SRS.log("Update -  Slot  ID:"..playerID.." Name: ".._update.name.." Side: ".._update.side)

	if SRS.unicast then
		socket.try(SRS.UDPSendSocket:sendto(SRS.JSON:encode(_update).." \n", "127.0.0.1", 5068))
	else
		socket.try(SRS.UDPSendSocket:sendto(SRS.JSON:encode(_update).." \n", "127.255.255.255", 5068))
	end

   
end

DCS.setUserCallbacks(SRS)

net.log("Loaded - DCS-SRS GameGUI")