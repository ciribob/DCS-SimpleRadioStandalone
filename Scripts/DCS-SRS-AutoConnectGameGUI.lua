-- Version 2.1.1.0
-- ONLY COPY THIS WHOLE FILE IS YOU ARE GOING TO HOST A SERVER!
-- The file must be in Saved Games\DCS\Scripts\Hooks or Saved Games\DCS.openalpha\Scripts\Hooks
-- Make sure you enter the correct address into SERVER_SRS_HOST and SERVER_SRS_PORT (5002 by default) below.
-- You can optionally enable SERVER_SRS_HOST_AUTO and SRS will attempt to find your public IP address
-- You can also enable SRS Chat commands to list frequencies and a message to all 
-- non SRS connected users to encourage them to connect

-- User options --
local SRSAuto = {}

SRSAuto.SERVER_SRS_HOST_AUTO = false -- if set to true SRS will set the SERVER_SRS_HOST for you!
SRSAuto.SERVER_SRS_PORT = "5002" --  SRS Server default is 5002 TCP & UDP
SRSAuto.SERVER_SRS_HOST = "127.0.0.1" -- overridden if SRS_HOST_AUTO is true -- set to your PUBLIC ipv4 address or domain srs.example.com
SRSAuto.SERVER_SEND_AUTO_CONNECT = true -- set to false to disable auto connect or just remove this file 

---- SRS CHAT COMMANDS ----
SRSAuto.CHAT_COMMANDS_ENABLED = false -- if true type -freq, -freqs or -frequencies in ALL chat in multilayer to see the frequencies

SRSAuto.SRS_FREQUENCIES = {
    ["red"]= "ATC = 124, GCI = 126, Helicopters = 30FM", -- edit this to the red frequency list
    ["blue"]= "ATC = 251, GCI = 264, Helicopters = 30FM", -- edit this to the blue frequency list
    ["neutral"]= "" -- edit this to the spectator frequency list
}


---- SRS NUDGE MESSAGE ----
SRSAuto.SRS_NUDGE_ENABLED = false -- set to true to enable the message below
SRSAuto.SRS_NUDGE_TIME = 600 -- SECONDS between messages to non connected SRS users
SRSAuto.SRS_MESSAGE_TIME = 30 -- SECONDS to show the message for
SRSAuto.SRS_NUDGE_PATH = "C:\\Program Files\\DCS-SimpleRadio-Standalone\\clients-list.json" -- path to SERVER JSON EXPORT - enable Auto Export List on the server
--- EDIT the message below to change what is said to users - DONT USE QUOTES - either single or double due to the injection into SRS it'll fail
--- Newlines must be escaped like so: \\\n with 3 backslashes
SRSAuto.SRS_NUDGE_MESSAGE = "****** DCS IS BETTER WITH COMMS - USE SRS ******\\\n\\\nMake sure to install DCS SimpleRadio Standalone - SRS - free and easy to install. \\\n\\\nSRS gives you VOIP Comms with other players through your aircrafts own radios. This will help you to be more effective, find enemies and wingmen, or call in support \\\n\\\n Google: DCS SimpleRadio Standalone \\\n\\\nGood comms and teamwork will help YOU and your team win! "


-- DO NOT EDIT BELOW HERE --

SRSAuto.unicast = true

-- Utils --
local HOST_PLAYER_ID = 1

SRSAuto.MESSAGE_PREFIX = "SRS Running @ " -- DO NOT MODIFY!!!
SRSAuto.MESSAGE_PREFIX_PORT = "SRS Running on " -- DO NOT MODIFY!!!

package.path  = package.path..";.\\LuaSocket\\?.lua;"
package.cpath = package.cpath..";.\\LuaSocket\\?.dll;"

local JSON = loadfile("Scripts\\JSON.lua")()
SRSAuto.JSON = JSON

local socket = require("socket")
-- local DcsWeb = require('DcsWeb')

require("url") -- defines socket.url, which socket.http looks for
http = require("http") -- socket.http

SRSAuto.UDPSendSocket = socket.udp()
SRSAuto.UDPSendSocket:settimeout(0)

SRSAuto.logFile = io.open(lfs.writedir()..[[Logs\DCS-SRS-AutoConnect.log]], "w")

function SRSAuto.log(str)
    if SRSAuto.logFile then
        SRSAuto.logFile:write(str.."\n")
        SRSAuto.logFile:flush()
    end
end

function SRSAuto.sendAutoConnectMessage(id)
    SRSAuto.log(string.format("Sending auto connect message to player %d on connect ", id))
    if SRSAuto.SERVER_SRS_HOST_AUTO then
        net.send_chat_to(SRSAuto.MESSAGE_PREFIX_PORT .. SRSAuto.SERVER_SRS_PORT, id)
    else    
        net.send_chat_to(SRSAuto.MESSAGE_PREFIX .. SRSAuto.SERVER_SRS_HOST .. ":" .. SRSAuto.SERVER_SRS_PORT, id)
    end
end

-- Register callbacks --

SRSAuto.onPlayerConnect = function(id)
	if not DCS.isServer() then
        return
    end
    if SRSAuto.SERVER_SEND_AUTO_CONNECT and id ~= HOST_PLAYER_ID then
        SRSAuto.sendAutoConnectMessage(id)
    end
end

SRSAuto.onPlayerChangeSlot = function(id)
    if not DCS.isServer() then
        return
    end
    if SRSAuto.SERVER_SEND_AUTO_CONNECT and id ~= HOST_PLAYER_ID then
        SRSAuto.sendAutoConnectMessage(id)
    end
end

SRSAuto.trimStr = function(_str)
    return string.format("%s", _str:match("^%s*(.-)%s*$"))
end

SRSAuto.onChatMessage = function(message, playerID)
    local _msg = string.lower(SRSAuto.trimStr(message))

    if  _msg == "-freq" or _msg == "-frequencies" or _msg == "-freqs" then

        local _player = net.get_player_info(playerID)

        local _freq = ""

        if _player.side == 2 then
            _freq = SRSAuto.SRS_FREQUENCIES.blue
        elseif _player.side == 1 then
            _freq = SRSAuto.SRS_FREQUENCIES.red
        else
            _freq = SRSAuto.SRS_FREQUENCIES.neutral
        end

        local _chatMessage = string.format("*** SRS: %s ***",_freq)
        net.send_chat_to(_chatMessage, _player.id)
        return
    end
end

local _lastSent = 0

SRSAuto.onMissionLoadBegin = function()
end

SRSAuto.onSimulationFrame = function()

    if SRSAuto.SRS_NUDGE_ENABLED then

        if not DCS.isServer() then
            return
        end

        local _now = os.time()

        -- send every 5 seconds
        if _now > _lastSent + SRSAuto.SRS_NUDGE_TIME then
            _lastSent = _now

            SRSAuto.srsNudge()
        end
    end
end

SRSAuto.readFile = function (path)
    local file = io.open(path, "rb") -- r read mode and b binary mode
    if not file then return nil end
    local content = file:read "*a" -- *a or *all reads the whole file
    file:close()
    return content
end

SRSAuto.srsNudge = function()

    SRSAuto.log("SRS NUDGE Running")

    local _status, _result = pcall(function()

        local _playerByName = {}
        for _,v in pairs(net.get_player_list()) do

            local _player = net.get_player_info(v)
           
               
                if _player.side ~= 0  then

                    _playerByName[_player.name] = _player
                     --SRSAuto.log("SRS NUDGE - Added ".._player.name)

                end
            
        end
        local fileContent = SRSAuto.readFile(SRSAuto.SRS_NUDGE_PATH);

        local srs = {}
        srs = SRSAuto.JSON:decode(fileContent)

        if srs and srs.Clients then
        	srs = srs.Clients
            -- loop through SRS and remove players
            for _,_srsPlayer in pairs(srs) do
                _playerByName[_srsPlayer.Name] = nil
                --SRSAuto.log("SRS NUDGE - Removed ".._srsPlayer.Name)
            end

            -- loop through remaining and nudge
            for _name,_player in pairs(_playerByName) do

                local _group = DCS.getUnitProperty(_player.slot, DCS.UNIT_GROUP_MISSION_ID)

                if _group then

                    SRSAuto.log("SRS NUDGE - Messaging ".._player.name)
                    SRSAuto.sendMessage(SRSAuto.SRS_NUDGE_MESSAGE,SRSAuto.SRS_MESSAGE_TIME,_group)

                end
            end
        end
    end)

    if not _status then
        SRSAuto.log('ERROR: ' .. _result)
    end



end

SRSAuto.sendMessage = function(msg, showTime, gid)
    if gid then
        local str = "trigger.action.outTextForGroup(" .. gid .. ",'" .. msg .. "'," .. showTime .. ",true); return true;"
        local _status, _error = net.dostring_in('server', str)
        if not _status and _error then
            SRSAuto.log("Error! " .. _error)
        end
    else
        local str = "trigger.action.outText('" .. msg .. "'," .. showTime .. ",true); return true;"
        local _status, _error = net.dostring_in('server', str)
        if not _status and _error then
            SRSAuto.log("Error! " .. _error)
        end
    end
end

DCS.setUserCallbacks(SRSAuto)
net.log("Loaded - DCS-SRS-AutoConnect 2.0.7.1")
