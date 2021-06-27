-- Version 1.9.7.0
-- Special thanks to Cap. Zeen, Tarres and Splash for all the help
-- with getting the radio information :)
-- Run the installer to correctly install this file
local SR = {}

SR.LOS_RECEIVE_PORT = 9086
SR.LOS_SEND_TO_PORT = 9085
SR.RADIO_SEND_TO_PORT = 9084

SR.LOS_HEIGHT_OFFSET = 20.0 -- sets the line of sight offset to simulate radio waves bending
SR.LOS_HEIGHT_OFFSET_MAX = 200.0 -- max amount of "bend"
SR.LOS_HEIGHT_OFFSET_STEP = 20.0 -- Interval to "bend" in

SR.unicast = true --DONT CHANGE THIS

SR.lastKnownPos = { x = 0, y = 0, z = 0 }

SR.MIDS_FREQ = 1030.0 * 1000000 -- Start at UHF 300
SR.MIDS_FREQ_SEPARATION = 1.0 * 100000 -- 0.1 MHZ between MIDS channels

SR.logFile = io.open(lfs.writedir() .. [[Logs\DCS-SimpleRadioStandalone.log]], "w")
function SR.log(str)
    if SR.logFile then
        SR.logFile:write(str .. "\n")
        SR.logFile:flush()
    end
end

package.path = package.path .. ";.\\LuaSocket\\?.lua;"
package.cpath = package.cpath .. ";.\\LuaSocket\\?.dll;"

---- DCS Search Paths - So we can load Terrain!
local guiBindPath = './dxgui/bind/?.lua;' ..
        './dxgui/loader/?.lua;' ..
        './dxgui/skins/skinME/?.lua;' ..
        './dxgui/skins/common/?.lua;'

package.path = package.path .. ";"
        .. guiBindPath
        .. './MissionEditor/?.lua;'
        .. './MissionEditor/themes/main/?.lua;'
        .. './MissionEditor/modules/?.lua;'
        .. './Scripts/?.lua;'
        .. './LuaSocket/?.lua;'
        .. './Scripts/UI/?.lua;'
        .. './Scripts/UI/Multiplayer/?.lua;'
        .. './Scripts/DemoScenes/?.lua;'

local socket = require("socket")

local JSON = loadfile("Scripts\\JSON.lua")()
SR.JSON = JSON

SR.UDPSendSocket = socket.udp()
SR.UDPLosReceiveSocket = socket.udp()

--bind for listening for LOS info
SR.UDPLosReceiveSocket:setsockname("*", SR.LOS_RECEIVE_PORT)
SR.UDPLosReceiveSocket:settimeout(0) --receive timer was 0001

local terrain = require('terrain')

if terrain ~= nil then
    SR.log("Loaded Terrain - SimpleRadio Standalone!")
end

-- Prev Export functions.
local _prevExport = {}
_prevExport.LuaExportActivityNextEvent = LuaExportActivityNextEvent
_prevExport.LuaExportBeforeNextFrame = LuaExportBeforeNextFrame

local _send = false

local _lastUnitId = "" -- used for a10c volume

LuaExportActivityNextEvent = function(tCurrent)
    local tNext = tCurrent + 0.1 -- for helios support
    -- we only want to send once every 0.2 seconds 
    -- but helios (and other exports) require data to come much faster
    -- so we just flip a boolean every run through to reduce to 0.2 rather than 0.1 seconds
    if _send then
  
        _send = false

    local _status, _result = pcall(function()

        local _update = nil

            local _data = LoGetSelfData()

        if _data ~= nil then

            _update = {
                name = "",
                unit = "",
                selected = 1,
                simultaneousTransmissionControl = 0,
                unitId = 0,
                ptt = false,
                capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" },
                radios = {
                    -- Radio 1 is always Intercom
                    { name = "", freq = 100, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                    { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 }, -- enc means encrypted
                    { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                    { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                    { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                    { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                },
                control = 0, -- HOTAS
            }

            _update.name = _data.UnitName
            _update.unit = _data.Name
            _update.unitId = LoGetPlayerPlaneId()

            local _latLng,_point = SR.exportPlayerLocation(_data)

            _update.latLng = _latLng
            SR.lastKnownPos = _point

            -- IFF_STATUS:  OFF = 0,  NORMAL = 1 , or IDENT = 2 (IDENT means Blink on LotATC)
            -- M1:-1 = off, any other number on
            -- M3: -1 = OFF, any other number on
            -- M4: 1 = ON or 0 = OFF
            -- EXPANSION: only enabled if IFF Expansion is enabled
            -- CONTROL: 1 - OVERLAY / SRS, 0 - COCKPIT / Realistic, 2 = DISABLED / NOT FITTED AT ALL
            -- MIC - -1 for OFF or ID of the radio to trigger IDENT Mode if the PTT is used
            -- IFF STATUS{"control":1,"expansion":false,"mode1":51,"mode3":7700,"mode4":1,"status":2,mic=1}

            _update.iff = {status=0,mode1=0,mode3=0,mode4=0,control=1,expansion=false,mic=-1}

            --SR.log(_update.unit.."\n\n")

            if _update.unit == "UH-1H" then
                _update = SR.exportRadioUH1H(_update)
            elseif string.find(_update.unit, "SA342",1,true) then
                _update = SR.exportRadioSA342(_update)
            elseif _update.unit == "Ka-50" then
                _update = SR.exportRadioKA50(_update)
            elseif _update.unit == "Mi-8MT" then
                _update = SR.exportRadioMI8(_update)
            elseif _update.unit == "Mi-24P" then
                _update = SR.exportRadioMI24P(_update)
            elseif string.find(_update.unit, "L-39",1,true)  then
                _update = SR.exportRadioL39(_update)
            elseif _update.unit == "Yak-52" then
                _update = SR.exportRadioYak52(_update)
            elseif string.find(_update.unit, "A-10C",1,true) then
                _update = SR.exportRadioA10C(_update)
            elseif _update.unit == "FA-18C_hornet" then
                _update = SR.exportRadioFA18C(_update)
            elseif string.find(_update.unit, "F-14",1,true)  then
                _update = SR.exportRadioF14(_update)
            elseif _update.unit == "F-86F Sabre" then
                _update = SR.exportRadioF86Sabre(_update)
            elseif _update.unit == "MiG-15bis" then
                _update = SR.exportRadioMIG15(_update)
            elseif _update.unit == "MiG-19P" then
                _update = SR.exportRadioMIG19(_update)
            elseif _update.unit == "MiG-21Bis" then
                _update = SR.exportRadioMIG21(_update)
            elseif _update.unit == "F-5E-3" then
                _update = SR.exportRadioF5E(_update)
            elseif string.find(_update.unit, "P-51",1,true) or _update.unit == "TF-51D" then
                _update = SR.exportRadioP51(_update)
            elseif string.find(_update.unit, "P-47",1,true) then
                _update = SR.exportRadioP47(_update)
            elseif _update.unit == "FW-190D9" or _update.unit == "FW-190A8" then
                _update = SR.exportRadioFW190(_update)
            elseif _update.unit == "Bf-109K-4" then
                _update = SR.exportRadioBF109(_update)
            elseif string.find(_update.unit, "SpitfireLFMkIX",1,true)  then
                _update = SR.exportRadioSpitfireLFMkIX(_update)
            elseif _update.unit == "C-101EB" then
                _update = SR.exportRadioC101EB(_update)
            elseif _update.unit == "C-101CC" then
                _update = SR.exportRadioC101CC(_update)
            elseif _update.unit == "Hawk" then
                _update = SR.exportRadioHawk(_update)
            elseif _update.unit == "Christen Eagle II" then
                _update = SR.exportRadioEagleII(_update)
            elseif _update.unit == "M-2000C" then
                _update = SR.exportRadioM2000C(_update)
            elseif _update.unit == "JF-17"  then
                _update = SR.exportRadioJF17(_update)
            elseif _update.unit == "AV8BNA" then
                _update = SR.exportRadioAV8BNA(_update)
            elseif _update.unit == "AJS37" then
                _update = SR.exportRadioAJS37(_update)
            elseif _update.unit == "A-10A" then
                _update = SR.exportRadioA10A(_update)
            elseif _update.unit == "A-4E-C" then
                _update = SR.exportRadioA4E(_update)
            elseif _update.unit == "T-45" then
                _update = SR.exportRadioT45(_update)
			elseif _update.unit == "A-29B" then
                _update = SR.exportRadioA29B(_update)
            elseif _update.unit == "F-15C" then
                _update = SR.exportRadioF15C(_update)
            elseif _update.unit == "MiG-29A" or _update.unit == "MiG-29S" or _update.unit == "MiG-29G" then
                _update = SR.exportRadioMiG29(_update)
            elseif _update.unit == "Su-27" or _update.unit == "Su-33" then
                _update = SR.exportRadioSU27(_update)
            elseif _update.unit == "Su-25" or _update.unit == "Su-25T" then
                _update = SR.exportRadioSU25(_update)
            elseif _update.unit == "F-16C_50" then
                _update = SR.exportRadioF16C(_update)
            else
                -- FC 3
                _update.radios[2].name = "FC3 VHF"
                _update.radios[2].freq = 124.8 * 1000000 --116,00-151,975 MHz
                _update.radios[2].modulation = 0
                _update.radios[2].secFreq = 121.5 * 1000000
                _update.radios[2].volume = 1.0
                _update.radios[2].freqMin = 116 * 1000000
                _update.radios[2].freqMax = 151.975 * 1000000
                _update.radios[2].volMode = 1
                _update.radios[2].freqMode = 1
                _update.radios[2].rtMode = 1

                _update.radios[3].name = "FC3 UHF"
                _update.radios[3].freq = 251.0 * 1000000 --225-399.975 MHZ
                _update.radios[3].modulation = 0
                _update.radios[3].secFreq = 243.0 * 1000000
                _update.radios[3].volume = 1.0
                _update.radios[3].freqMin = 225 * 1000000
                _update.radios[3].freqMax = 399.975 * 1000000
                _update.radios[3].volMode = 1
                _update.radios[3].freqMode = 1
                _update.radios[3].rtMode = 1
                _update.radios[3].encKey = 1
                _update.radios[3].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

                _update.radios[4].name = "FC3 FM"
                _update.radios[4].freq = 30.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
                _update.radios[4].modulation = 1
                _update.radios[4].volume = 1.0
                _update.radios[4].freqMin = 30 * 1000000
                _update.radios[4].freqMax = 76 * 1000000
                _update.radios[4].volMode = 1
                _update.radios[4].freqMode = 1
                _update.radios[4].encKey = 1
                _update.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
                _update.radios[4].rtMode = 1

                _update.control = 0;
                _update.selected = 1
                _update.iff = {status=0,mode1=0,mode3=0,mode4=0,control=0,expansion=false,mic=-1}
            end

            _lastUnitId = _update.unitId
        else
            --Ground Commander or spectator
            _update = {
                name = "Unknown",
                unit = "CA",
                selected = 1,
                ptt = false,
                capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" },
                simultaneousTransmissionControl = 1,
                latLng = { lat = 0, lng = 0, alt = 0 },
                unitId = 100000001, -- pass through starting unit id here
                radios = {
                    --- Radio 0 is always intercom now -- disabled if AWACS panel isnt open
                    { name = "SATCOM", freq = 100, modulation = 2, volume = 1.0, secFreq = 0, freqMin = 100, freqMax = 100, encKey = 0, enc = false, encMode = 0, freqMode = 0, volMode = 1, expansion = false, rtMode = 2 },
                    { name = "UHF Guard", freq = 251.0 * 1000000, modulation = 0, volume = 1.0, secFreq = 243.0 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 1, enc = false, encMode = 1, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "UHF Guard", freq = 251.0 * 1000000, modulation = 0, volume = 1.0, secFreq = 243.0 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 1, enc = false, encMode = 1, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "VHF FM", freq = 30.0 * 1000000, modulation = 1, volume = 1.0, secFreq = 1, freqMin = 1 * 1000000, freqMax = 76 * 1000000, encKey = 1, enc = false, encMode = 1, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "UHF Guard", freq = 251.0 * 1000000, modulation = 0, volume = 1.0, secFreq = 243.0 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 1, enc = false, encMode = 1, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "UHF Guard", freq = 251.0 * 1000000, modulation = 0, volume = 1.0, secFreq = 243.0 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 1, enc = false, encMode = 1, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "VHF Guard", freq = 124.8 * 1000000, modulation = 0, volume = 1.0, secFreq = 121.5 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 0, enc = false, encMode = 0, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "VHF Guard", freq = 124.8 * 1000000, modulation = 0, volume = 1.0, secFreq = 121.5 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 0, enc = false, encMode = 0, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "VHF FM", freq = 30.0 * 1000000, modulation = 1, volume = 1.0, secFreq = 1, freqMin = 1 * 1000000, freqMax = 76 * 1000000, encKey = 1, enc = false, encMode = 1, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "VHF Guard", freq = 124.8 * 1000000, modulation = 0, volume = 1.0, secFreq = 121.5 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 0, enc = false, encMode = 0, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                    { name = "VHF Guard", freq = 124.8 * 1000000, modulation = 0, volume = 1.0, secFreq = 121.5 * 1000000, freqMin = 1 * 1000000, freqMax = 400 * 1000000, encKey = 0, enc = false, encMode = 0, freqMode = 1, volMode = 1, expansion = false, rtMode = 1 },
                },
                radioType = 3,
                iff = {status=0,mode1=0,mode3=0,mode4=0,control=0,expansion=false,mic=-1}
            }

            local _latLng,_point = SR.exportCameraLocation()

            _update.latLng = _latLng
            SR.lastKnownPos = _point

            _lastUnitId = ""
        end

        if SR.unicast then
            socket.try(SR.UDPSendSocket:sendto(SR.JSON:encode(_update) .. " \n", "127.0.0.1", SR.RADIO_SEND_TO_PORT))
        else
            socket.try(SR.UDPSendSocket:sendto(SR.JSON:encode(_update) .. " \n", "127.255.255.255", SR.RADIO_SEND_TO_PORT))
        end

    end)

    if not _status then
        SR.log('ERROR: ' .. _result)
    end

    else
        _send = true
    end


    -- call
    local _status, _result = pcall(function()
        -- Call original function if it exists
        if _prevExport.LuaExportActivityNextEvent then
            _prevExport.LuaExportActivityNextEvent(tCurrent)
        end

    end)

    if not _status then
        SR.log('ERROR Calling other LuaExportActivityNextEvent from another script: ' .. _result)
    end

    if terrain == nil then
        SR.log("Terrain Export is not working")
        --SR.log("EXPORT CHECK "..tostring(terrain.isVisible(1,100,1,1,100,1)))
        --SR.log("EXPORT CHECK "..tostring(terrain.isVisible(1,1,1,1,-100,-100)))
    end

	 --SR.log(SR.tableShow(_G).."\n\n")

    return tNext
end

local _lastCheck = 0;

LuaExportBeforeNextFrame = function()

    -- read from socket
    local _status, _result = pcall(function()

        -- Receive buffer is 8192 in LUA Socket
        -- will contain 10 clients for LOS
        local _received = SR.UDPLosReceiveSocket:receive()

        if _received then
            local _decoded = SR.JSON:decode(_received)

            if _decoded then

                local _losList = SR.checkLOS(_decoded)

                --DEBUG
                -- SR.log('LOS check ' .. SR.JSON:encode(_losList))
                if SR.unicast then
                    socket.try(SR.UDPSendSocket:sendto(SR.JSON:encode(_losList) .. " \n", "127.0.0.1", SR.LOS_SEND_TO_PORT))
                else
                    socket.try(SR.UDPSendSocket:sendto(SR.JSON:encode(_losList) .. " \n", "127.255.255.255", SR.LOS_SEND_TO_PORT))
                end
            end

        end
    end)

    if not _status then
        SR.log('ERROR LuaExportBeforeNextFrame SRS: ' .. _result)
    end

    -- call original
    _status, _result = pcall(function()
        -- Call original function if it exists
        if _prevExport.LuaExportBeforeNextFrame then
            _prevExport.LuaExportBeforeNextFrame()
            end
     end)

    if not _status then
        SR.log('ERROR Calling other LuaExportBeforeNextFrame from another script: ' .. _result)
end

end

function SR.checkLOS(_clientsList)

    local _result = {}

    for _, _client in pairs(_clientsList) do
        -- add 10 meter tolerance
        --Coordinates convertion :
        --{x,y,z}                 = LoGeoCoordinatesToLoCoordinates(longitude_degrees,latitude_degrees)
        local _point = LoGeoCoordinatesToLoCoordinates(_client.lng,_client.lat)
        -- Encoded Point: {"x":3758906.25,"y":0,"z":-1845112.125}

        local _los = 1.0 -- 1.0 is NO line of sight as in full signal loss - 0.0 is full signal, NO Loss

        local _hasLos = terrain.isVisible(SR.lastKnownPos.x, SR.lastKnownPos.y + SR.LOS_HEIGHT_OFFSET, SR.lastKnownPos.z, _point.x, _client.alt + SR.LOS_HEIGHT_OFFSET, _point.z)

        if _hasLos then
            table.insert(_result, { id = _client.id, los = 0.0 })
        else
        
            -- find the lowest offset that would provide line of sight
            for _losOffset = SR.LOS_HEIGHT_OFFSET + SR.LOS_HEIGHT_OFFSET_STEP, SR.LOS_HEIGHT_OFFSET_MAX - SR.LOS_HEIGHT_OFFSET_STEP, SR.LOS_HEIGHT_OFFSET_STEP do

                _hasLos = terrain.isVisible(SR.lastKnownPos.x, SR.lastKnownPos.y + _losOffset, SR.lastKnownPos.z, _point.x, _client.alt + SR.LOS_HEIGHT_OFFSET, _point.z)

                if _hasLos then
                    -- compute attenuation as a percentage of LOS_HEIGHT_OFFSET_MAX
                    -- e.g.: 
                    --    LOS_HEIGHT_OFFSET_MAX = 500   -- max offset
                    --    _losOffset = 200              -- offset actually used
                    --    -> attenuation would be 200 / 500 = 0.4
                    table.insert(_result, { id = _client.id, los = (_losOffset / SR.LOS_HEIGHT_OFFSET_MAX) })
                    break ;
                end
            end
            
            -- if there is still no LOS            
            if not _hasLos then

              -- then check max offset gives LOS
              _hasLos = terrain.isVisible(SR.lastKnownPos.x, SR.lastKnownPos.y + SR.LOS_HEIGHT_OFFSET_MAX, SR.lastKnownPos.z, _point.x, _client.alt + SR.LOS_HEIGHT_OFFSET, _point.z)

              if _hasLos then
                  -- but make sure that we do not get 1.0 attenuation when using LOS_HEIGHT_OFFSET_MAX
                  -- (LOS_HEIGHT_OFFSET_MAX / LOS_HEIGHT_OFFSET_MAX would give attenuation of 1.0)
                  -- I'm using 0.99 as a placeholder, not sure what would work here
                  table.insert(_result, { id = _client.id, los = (0.99) })
              else
                  -- otherwise set attenuation to 1.0
                  table.insert(_result, { id = _client.id, los = 1.0 }) -- 1.0 Being NO line of sight - FULL signal loss
              end
            end
        end

    end
    return _result
end

--Coordinates convertion :
--{latitude,longitude}  = LoLoCoordinatesToGeoCoordinates(x,z);

function SR.exportPlayerLocation(_data)

    if _data ~= nil and _data.Position ~= nil then

        local latLng  = LoLoCoordinatesToGeoCoordinates(_data.Position.x,_data.Position.z)
        --LatLng: {"latitude":25.594814853729,"longitude":55.938746498011}

        return { lat = latLng.latitude, lng = latLng.longitude, alt = _data.Position.y },_data.Position
    else
        return { lat = 0, lng = 0, alt = 0 },{ x = 0, y = 0, z = 0 }
    end
end

function SR.exportCameraLocation()
    local _cameraPosition = LoGetCameraPosition()

    if _cameraPosition ~= nil and _cameraPosition.p ~= nil then

        local latLng = LoLoCoordinatesToGeoCoordinates(_cameraPosition.p.x, _cameraPosition.p.z)

        return { lat = latLng.latitude, lng = latLng.longitude, alt = _cameraPosition.p.y },_cameraPosition.p
    end

    return { lat = 0, lng = 0, alt = 0 },{ x = 0, y = 0, z = 0 }
end

function SR.exportRadioA10A(_data)

    _data.radios[2].name = "AN/ARC-186(V)"
    _data.radios[2].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 121.5 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 116 * 1000000
    _data.radios[2].freqMax = 151.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1

    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 243.0 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 225 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.radios[4].name = "AN/ARC-186(V)FM"
    _data.radios[4].freq = 30.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
    _data.radios[4].modulation = 1
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30 * 1000000
    _data.radios[4].freqMax = 76 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1

    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    return _data
end

function SR.exportRadioMiG29(_data)

    _data.radios[2].name = "R-862"
    _data.radios[2].freq = 251.0 * 1000000 --V/UHF, frequencies are: VHF range of 100 to 149.975 MHz and UHF range of 220 to 399.975 MHz
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 121.5 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 100 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].expansion = true
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    return _data
end

function SR.exportRadioSU25(_data)

    _data.radios[2].name = "R-862"
    _data.radios[2].freq = 251.0 * 1000000 --V/UHF, frequencies are: VHF range of 100 to 149.975 MHz and UHF range of 220 to 399.975 MHz
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 121.5 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 100 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1

    _data.radios[3].name = "R-828"
    _data.radios[3].freq = 30.0 * 1000000 --20 - 60 MHz.
    _data.radios[3].modulation = 1
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 20 * 1000000
    _data.radios[3].freqMax = 59.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    return _data
end

function SR.exportRadioSU27(_data)

    _data.radios[2].name = "R-800"
    _data.radios[2].freq = 251.0 * 1000000 --V/UHF, frequencies are: VHF range of 100 to 149.975 MHz and UHF range of 220 to 399.975 MHz
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 121.5 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 100 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1

    _data.radios[3].name = "R-864"
    _data.radios[3].freq = 3.5 * 1000000 --HF frequencies in the 3-10Mhz, like the Jadro
    _data.radios[3].modulation = 0
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 3 * 1000000
    _data.radios[3].freqMax = 10 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

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
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    return _data
end

function SR.exportRadioA4E(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    --local intercom = GetDevice(4) --commented out for now, may be useful in future
    local uhf_radio = GetDevice(5) --see devices.lua or Devices.h

    local mainFreq = 0
    local guardFreq = 0

    -- Can directly check the radio device.
    local hasPower = uhf_radio:is_on()

    -- "Function Select Switch" near the right edge controls radio power
    local functionSelect = SR.getButtonPosition(372)

    -- All frequencies are set by the radio in the A-4 so no extra checking required here.
    if hasPower then
        mainFreq = SR.round(uhf_radio:get_frequency(), 5000)

        -- Additionally, enable guard monitor if Function knob is in position T/R+G
        if 0.15 < functionSelect and functionSelect < 0.25 then
            guardFreq = 243.000e6
        end
    end

    local arc51 = _data.radios[2]
    arc51.name = "AN/ARC-51BX"
    arc51.freq = mainFreq
    arc51.secFreq = guardFreq
    arc51.channel = nil -- what is this used for?
    arc51.modulation = 0  -- AM only
    arc51.freqMin = 220.000e6
    arc51.freqMax = 399.950e6

    -- TODO Check if there are other volume knobs in series
    arc51.volume = SR.getRadioVolume(0, 365, {0.2, 0.8}, false)
    if arc51.volume < 0.0 then
        -- The knob position at startup is 0.0, not 0.2, and it gets scaled to -33.33
        arc51.volume = 0.0
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[3].name = "AN/ARC-186(V)"
    _data.radios[3].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 121.5 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116 * 1000000
    _data.radios[3].freqMax = 151.975 * 1000000
    _data.radios[3].expansion = true
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-186(V)FM"
    _data.radios[4].freq = 30.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
    _data.radios[4].modulation = 1
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30 * 1000000
    _data.radios[4].freqMax = 76 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true

    _data.control = 0;
    _data.selected = 1

    return _data
end


function SR.exportRadioT45(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local radio1Device = GetDevice(1)
    local radio2Device = GetDevice(2)
    local mainFreq1 = 0
    local guardFreq1 = 0
    local mainFreq2 = 0
    local guardFreq2 = 0
    
    local comm1Switch = GetDevice(0):get_argument_value(191) 
    local comm2Switch = GetDevice(0):get_argument_value(192) 
    local comm1PTT = GetDevice(0):get_argument_value(294)
    local comm2PTT = GetDevice(0):get_argument_value(294) 
    local intercomPTT = GetDevice(0):get_argument_value(295)    
    local ICSMicSwitch = GetDevice(0):get_argument_value(196) --0 cold, 1 hot
    
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = GetDevice(0):get_argument_value(198)
    
    local modeSelector1 = GetDevice(0):get_argument_value(256) -- 0:off, 0.25:T/R, 0.5:T/R+G
    if modeSelector1 == 0.5 and comm1Switch == 1 then
        mainFreq1 = SR.round(radio1Device:get_frequency(), 5000)
        if mainFreq1 > 225000000 then
            guardFreq1 = 243.000E6
        elseif mainFreq1 < 155975000 then
            guardFreq1 = 121.500E6
        else
            guardFreq1 = 0
        end
    elseif modeSelector1 == 0.25 and comm1Switch == 1 then
        guardFreq1 = 0
        mainFreq1 = SR.round(radio1Device:get_frequency(), 5000)
    else
        guardFreq1 = 0
        mainFreq1 = 0
    end

    local arc182 = _data.radios[2]
    arc182.name = "AN/ARC-182(V) - 1"
    arc182.freq = mainFreq1
    arc182.secFreq = guardFreq1
    arc182.modulation = radio1Device:get_modulation()  
    arc182.freqMin = 30.000e6
    arc182.freqMax = 399.975e6
    arc182.volume = GetDevice(0):get_argument_value(246)

    local modeSelector2 = GetDevice(0):get_argument_value(280) -- 0:off, 0.25:T/R, 0.5:T/R+G
    if modeSelector2 == 0.5 and comm2Switch == 1 then
        mainFreq2 = SR.round(radio2Device:get_frequency(), 5000)
        if mainFreq2 > 225000000 then
            guardFreq2 = 243.000E6
        elseif mainFreq2 < 155975000 then
            guardFreq2 = 121.500E6
        else
            guardFreq2 = 0
        end
    elseif modeSelector2 == 0.25 and comm2Switch == 1 then
        guardFreq2 = 0
        mainFreq2 = SR.round(radio2Device:get_frequency(), 5000)
    else
        guardFreq2 = 0
        mainFreq2 = 0
    end
    
    local arc182_2 = _data.radios[3]
    arc182_2.name = "AN/ARC-182(V) - 2"
    arc182_2.freq = mainFreq2
    arc182_2.secFreq = guardFreq2
    arc182_2.modulation = radio2Device:get_modulation()  
    arc182_2.freqMin = 30.000e6
    arc182_2.freqMax = 399.975e6
    arc182_2.volume = GetDevice(0):get_argument_value(270)

    if comm1PTT == 1 then
        _data.selected = 1 -- comm 1
        _data.ptt = true
    elseif comm2PTT == -1 then
        _data.selected = 2 -- comm 2
        _data.ptt = true
    elseif intercomPTT == 1 then
        _data.selected = 0 -- intercom
        _data.ptt = true
    else
        _data.selected = -1
        _data.ptt = false
    end
    
    if ICSMicSwitch == 1 then
        _data.intercomHotMic = true
    else
        _data.intercomHotMic = false
    end

    _data.control = 1; -- full radio HOTAS control
    
    return _data
end

function SR.exportRadioA29B(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    local com1_freq = 0
    local com1_mod = 0
    local com1_sql = 0
    local com1_pwr = 0
    local com1_mode = 2

    local com2_freq = 0
    local com2_mod = 0
    local com2_sql = 0
    local com2_pwr = 0
    local com2_mode = 1

    local _ufcp = SR.getListIndicatorValue(4)
    if _ufcp then 
        if _ufcp.com1_freq then com1_freq = (_ufcp.com1_freq * 1000000) end
        if _ufcp.com1_mod then com1_mod = _ufcp.com1_mod * 1 end
        if _ufcp.com1_sql then com1_sql = _ufcp.com1_sql end
        if _ufcp.com1_pwr then com1_pwr = _ufcp.com1_pwr end
        if _ufcp.com1_mode then com1_mode = _ufcp.com1_mode * 1 end

        if _ufcp.com2_freq then com2_freq = (_ufcp.com2_freq * 1000000) end
        if _ufcp.com2_mod then com2_mod = _ufcp.com2_mod * 1 end
        if _ufcp.com2_sql then com2_sql = _ufcp.com2_sql end
        if _ufcp.com2_pwr then com2_pwr = _ufcp.com2_pwr end
        if _ufcp.com2_mode then com2_mode = _ufcp.com2_mode * 1 end
    end

    _data.radios[2].name = "XT-6013 COM1"
    _data.radios[2].modulation = com1_mod
    _data.radios[2].volume = SR.getRadioVolume(0, 762, { 0.0, 1.0 }, false)

    if com1_mode == 0 then 
        _data.radios[2].freq = 0
        _data.radios[2].secFreq = 0
    elseif com1_mode == 1 then 
        _data.radios[2].freq = com1_freq
        _data.radios[2].secFreq = 0
    elseif com1_mode == 2 then 
        _data.radios[2].freq = com1_freq
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    _data.radios[3].name = "XT-6313D COM2"
    _data.radios[3].modulation = com2_mod
    _data.radios[3].volume = SR.getRadioVolume(0, 763, { 0.0, 1.0 }, false)

    if com2_mode == 0 then 
        _data.radios[3].freq = 0
        _data.radios[3].secFreq = 0
    elseif com2_mode == 1 then 
        _data.radios[3].freq = com2_freq
        _data.radios[3].secFreq = 0
    elseif com2_mode == 2 then 
        _data.radios[3].freq = com2_freq
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    _data.radios[4].name = "KTR-953 HF"
    _data.radios[4].freq = 15.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 764, { 0.0, 1.0 }, false)
    _data.radios[4].freqMin = 2 * 1000000
    _data.radios[4].freqMax = 30 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
    _data.radios[4].rtMode = 1

    _data.control = 0 -- Hotas Controls radio
    _data.selected = 1

    return _data
end

function SR.exportRadioF15C(_data)

    _data.radios[2].name = "AN/ARC-164 UHF-1"
    _data.radios[2].freq = 251.0 * 1000000 --225 to 399.975MHZ
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 243.0 * 1000000
    _data.radios[2].volume = 1.0
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 1

    _data.radios[2].encKey = 1
    _data.radios[2].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.radios[3].name = "AN/ARC-164 UHF-2"
    _data.radios[3].freq = 231.0 * 1000000 --225 to 399.975MHZ
    _data.radios[3].modulation = 0
    _data.radios[3].freqMin = 225 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting


    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-186(V)"
    _data.radios[4].freq = 124.8 * 1000000 --116,00-151,975 MHz
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 121.5 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 116 * 1000000
    _data.radios[4].freqMax = 151.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1

    _data.control = 0;
    _data.selected = 1

    return _data
end

function SR.exportRadioUH1H(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    local intercomOn =  SR.getButtonPosition(27)
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume =  SR.getRadioVolume(0, 29, { 0.3, 1.0 }, true)

    if intercomOn < 0.5 then
        _data.radios[1].modulation = 3
    end

    local fmOn =  SR.getButtonPosition(23)
    _data.radios[2].name = "AN/ARC-131"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].modulation = 1
    _data.radios[2].volume = SR.getRadioVolume(0, 37, { 0.3, 1.0 }, true)

    if fmOn < 0.5 then
        _data.radios[2].freq = 1
    end

    local uhfOn =  SR.getButtonPosition(24)
    _data.radios[3].name = "AN/ARC-51BX - UHF"
    _data.radios[3].freq = SR.getRadioFrequency(22)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 21, { 0.0, 1.0 }, true)

    -- get channel selector
    local _selector = SR.getSelectorPosition(15, 0.1)

    if _selector < 1 then
        _data.radios[3].channel = SR.getSelectorPosition(16, 0.05) + 1 --add 1 as channel 0 is channel 1
    end

    if uhfOn < 0.5 then
        _data.radios[3].freq = 1
        _data.radios[3].channel = -1
    end

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(17, 0.1)
    if uhfModeKnob == 2 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    local vhfOn =  SR.getButtonPosition(25)
    _data.radios[4].name = "AN/ARC-134"
    _data.radios[4].freq = SR.getRadioFrequency(20)
    _data.radios[4].modulation = 0
    _data.radios[4].volume = SR.getRadioVolume(0, 9, { 0.0, 0.60 }, false)

    if vhfOn < 0.5 then
        _data.radios[4].freq = 1
    end

    --_device:get_argument_value(_arg)

    local _panel = GetDevice(0)

    local switch = _panel:get_argument_value(30)

    if SR.nearlyEqual(switch, 0.1, 0.03) then
        _data.selected = 0
    elseif SR.nearlyEqual(switch, 0.2, 0.03) then
        _data.selected = 1
    elseif SR.nearlyEqual(switch, 0.3, 0.03) then
        _data.selected = 2
    elseif SR.nearlyEqual(switch, 0.4, 0.03) then
        _data.selected = 3
    else
        _data.selected = -1
    end

    local _pilotPTT = SR.getButtonPosition(194)
    if _pilotPTT >= 0.1 then

        if _pilotPTT == 0.5 then
            -- intercom
            _data.selected = 0
        end

        _data.ptt = true
    end

    _data.control = 1; -- Full Radio

    -- HANDLE TRANSPONDER
    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}


    local iffPower =  SR.getSelectorPosition(59,0.1)

    local iffIdent =  SR.getButtonPosition(66) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- MODE set to MIC
        if iffIdent == -1 then

            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT due to MIC switch
            end
        end

    end

    local mode1On =  SR.getButtonPosition(61)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(68,0.33), 0.1)*10+SR.round(SR.getSelectorPosition(69,0.11), 0.1)


    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(63)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(70,0.11), 0.1) * 1000 + SR.round(SR.getSelectorPosition(71,0.11), 0.1) * 100 + SR.round(SR.getSelectorPosition(72,0.11), 0.1)* 10 + SR.round(SR.getSelectorPosition(73,0.11), 0.1)

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(67)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    --temporary hot mic
    _data.intercomHotMic = true

    return _data

end

function SR.exportRadioSA342(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1
    _data.radios[1].simul = true

    _data.radios[2].name = "TRAP 138A"
    local MHZ = 1000000
    local _hundreds = SR.round(SR.getKnobPosition(0, 133, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 100 * MHZ
    local _tens = SR.round(SR.getKnobPosition(0, 134, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 10 * MHZ
    local _ones = SR.round(SR.getKnobPosition(0, 136, { 0.0, 0.9 }, { 0, 9 }), 0.1) * MHZ
    local _tenth = SR.round(SR.getKnobPosition(0, 138, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 100000
    local _hundreth = SR.round(SR.getKnobPosition(0, 139, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 10000

    if SR.getSelectorPosition(128, 0.33) > 0.65 then -- Check VHF ON?
        _data.radios[2].freq = _hundreds + _tens + _ones + _tenth + _hundreth
    else
        _data.radios[2].freq = 1
    end
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 68, { 1.0, 0.0 }, true)
    _data.radios[2].rtMode = 1
    
    _data.radios[3].name = "UHF TRA 6031"

    -- deal with odd radio tune & rounding issue... BUG you cannot set frequency 243.000 ever again
    local freq = SR.getRadioFrequency(31, 500)
    freq = (math.floor(freq / 1000) * 1000)

    _data.radios[3].freq = freq

    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 69, { 0.0, 1.0 }, false)

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- 3 is Incockpit toggle + Gui Enc Key setting
    _data.radios[3].rtMode = 1

    _data.radios[4].name = "TRC 9600 PR4G"
    _data.radios[4].freq = SR.getRadioFrequency(28)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 70, { 0.0, 1.0 }, false)

    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 3 -- Variable Enc key but turned on by sim
    _data.radios[4].rtMode = 1

    --- is UHF ON?
    if SR.getSelectorPosition(383, 0.167) == 0 then
        _data.radios[3].freq = 1
    elseif SR.getSelectorPosition(383, 0.167) == 2 then
        --check UHF encryption
        _data.radios[3].enc = true
    end

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(383, 0.167)
    if uhfModeKnob == 5 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    --- is FM ON?
    if SR.getSelectorPosition(272, 0.25) == 0 then
        _data.radios[4].freq = 1
    elseif SR.getSelectorPosition(272, 0.25) == 2 then
        --check FM encryption
        _data.radios[4].enc = true
    end

    if SR.getButtonPosition(452) > 0.5 then
        _data.selected = 1
    elseif SR.getButtonPosition(454) > 0.5 then
        _data.selected = 2
    elseif SR.getButtonPosition(453) > 0.5 then
        _data.selected = 3
    end

    _data.control = 1; -- COCKPIT Controls
    _data.intercomHotMic = true

    return _data

end

function SR.exportRadioKA50(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    local _panel = GetDevice(0)

    _data.radios[2].name = "R-800L14 VHF/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(48)

    -- Get modulation mode
    local switch = _panel:get_argument_value(417)
    if SR.nearlyEqual(switch, 0.0, 0.03) then
        _data.radios[2].modulation = 1
    else
        _data.radios[2].modulation = 0
    end
    _data.radios[2].volume = SR.getRadioVolume(0, 353, { 0.0, 1.0 }, false) -- using ADF knob for now

    _data.radios[3].name = "R-828"
    _data.radios[3].freq = SR.getRadioFrequency(49, 50000)
    _data.radios[3].modulation = 1
    _data.radios[3].volume = SR.getRadioVolume(0, 372, { 0.0, 1.0 }, false)
    _data.radios[3].channel = SR.getSelectorPosition(371, 0.1) + 1

    --expansion radios
    _data.radios[4].name = "SPU-9 SW"
    _data.radios[4].freq = 5.0 * 1000000
    _data.radios[4].freqMin = 1.0 * 1000000
    _data.radios[4].freqMax = 10.0 * 1000000
    _data.radios[4].modulation = 0
    _data.radios[4].volume = 1.0
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1

    local switch = _panel:get_argument_value(428)

    if SR.nearlyEqual(switch, 0.0, 0.03) then
        _data.selected = 1
    elseif SR.nearlyEqual(switch, 0.1, 0.03) then
        _data.selected = 2
    elseif SR.nearlyEqual(switch, 0.2, 0.03) then
        _data.selected = 3
    else
        _data.selected = -1
    end

    _data.control = 1;

    return _data

end

function SR.exportRadioMI8(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    -- Doesnt work but might as well allow selection
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0

    _data.radios[2].name = "R-863"
    _data.radios[2].freq = SR.getRadioFrequency(38)

    local _modulation = GetDevice(0):get_argument_value(369)
    if _modulation > 0.5 then
        _data.radios[2].modulation = 1
    else
        _data.radios[2].modulation = 0
    end

    -- get channel selector
    local _selector = GetDevice(0):get_argument_value(132)

    if _selector > 0.5 then
        _data.radios[2].channel = SR.getSelectorPosition(370, 0.05) + 1 --add 1 as channel 0 is channel 1
    end

    _data.radios[2].volume = SR.getRadioVolume(0, 156, { 0.0, 1.0 }, false)

    _data.radios[3].name = "JADRO-1A"
    _data.radios[3].freq = SR.getRadioFrequency(37, 500)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 743, { 0.0, 1.0 }, false)

    _data.radios[4].name = "R-828"
    _data.radios[4].freq = SR.getRadioFrequency(39, 50000)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 737, { 0.0, 1.0 }, false)

    --guard mode for R-863 Radio
    local uhfModeKnob = SR.getSelectorPosition(153, 1)
    if uhfModeKnob == 1 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    -- Get selected radio from SPU-9
    local _switch = SR.getSelectorPosition(550, 0.1)

    if _switch == 0 then
        _data.selected = 1
    elseif _switch == 1 then
        _data.selected = 2
    elseif _switch == 2 then
        _data.selected = 3
    else
        _data.selected = -1
    end

    if SR.getButtonPosition(182) >= 0.5 or SR.getButtonPosition(225) >= 0.5 then
        _data.ptt = true
    end


    -- Radio / ICS Switch
    if SR.getButtonPosition(553) > 0.5 then
        _data.selected = 0
    end

    _data.control = 1; -- full radio

    return _data

end


function SR.exportRadioMI24P(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 0

    _data.radios[2].name = "R-863"
    _data.radios[2].freq = SR.getRadioFrequency(49)
    _data.radios[2].modulation = SR.getRadioModulation(49)
    _data.radios[2].volume = SR.getRadioVolume(0, 511, { 0.0, 1.0 }, false)
    _data.radios[2].volMode = 0

    local guard = SR.getSelectorPosition(507, 1)
    if guard == 1 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    _data.radios[3].name = "JADRO-1I"
    _data.radios[3].freq = SR.getRadioFrequency(50, 500)
    _data.radios[3].modulation = SR.getRadioModulation(50)
    _data.radios[3].volume = SR.getRadioVolume(0, 426, { 0.0, 1.0 }, false)
    _data.radios[3].volMode = 0

    _data.radios[4].name = "R-828"
    _data.radios[4].freq = SR.getRadioFrequency(51)
    _data.radios[4].modulation = 1 --SR.getRadioModulation(50)
    _data.radios[4].volume = SR.getRadioVolume(0, 339, { 0.0, 1.0 }, false)
    _data.radios[4].volMode = 0

    _data.radios[5].name = "R-852"
    _data.radios[5].freq = SR.getRadioFrequency(52)
    _data.radios[5].modulation = SR.getRadioModulation(52)
    _data.radios[5].volume = SR.getRadioVolume(0, 517, { 0.0, 1.0 }, false)
    _data.radios[5].volMode = 0

    _data.control = 0; -- HOTAS for now

    return _data

end

function SR.exportRadioL39(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = SR.getRadioVolume(0, 288, { 0.0, 0.8 }, false)

    _data.radios[2].name = "R-832M"
    _data.radios[2].freq = SR.getRadioFrequency(19)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 289, { 0.0, 0.8 }, false)

    -- Intercom button depressed
    if (SR.getButtonPosition(133) > 0.5 or SR.getButtonPosition(546) > 0.5) then
        _data.selected = 0
        _data.ptt = true
    elseif (SR.getButtonPosition(134) > 0.5 or SR.getButtonPosition(547) > 0.5) then
        _data.selected = 1
        _data.ptt = true
    else
        _data.selected = 1
        _data.ptt = false
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

    _data.control = 1; -- full radio - for expansion radios - DCS controls must be disabled

    _data.control = 1; -- full radio

    return _data
end

function SR.exportRadioEagleII(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1--SR.getRadioVolume(0, 288,{0.0,0.8},false)

    _data.radios[2].name = "KY-197A"
    _data.radios[2].freq = SR.getRadioFrequency(5)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 364, { 0.1, 1.0 }, false)

    if _data.radios[2].volume < 0 then
        _data.radios[2].volume = 0
    end


    -- Intercom button depressed
    -- if(SR.getButtonPosition(133) > 0.5 or SR.getButtonPosition(546) > 0.5) then
    --     _data.selected = 0
    --     _data.ptt = true
    -- elseif (SR.getButtonPosition(134) > 0.5 or SR.getButtonPosition(547) > 0.5) then
    --     _data.selected= 1
    --     _data.ptt = true
    -- else
    --     _data.selected= 1
    --      _data.ptt = false
    -- end

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

    _data.control = 0; -- HOTAS

    return _data
end

function SR.exportRadioYak52(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1

    _data.radios[2].name = "Baklan 5"
    _data.radios[2].freq = SR.getRadioFrequency(27)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 90, { 0.0, 1.0 }, false)

    -- Intercom button depressed
    if (SR.getButtonPosition(192) > 0.5 or SR.getButtonPosition(196) > 0.5) then
        _data.selected = 1
        _data.ptt = true
    elseif (SR.getButtonPosition(194) > 0.5 or SR.getButtonPosition(197) > 0.5) then
        _data.selected = 0
        _data.ptt = true
    else
        _data.selected = 1
        _data.ptt = false
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

    _data.control = 1; -- full radio - for expansion radios - DCS controls must be disabled

    return _data
end

--for A10C
function SR.exportRadioA10C(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    -- Check if player is in a new aircraft
    if _lastUnitId ~= _data.unitId then
        -- New aircraft; Reset volumes to 100%
        local _device = GetDevice(0)

        if _device then
            _device:set_argument_value(133, 1.0) -- VHF AM
            _device:set_argument_value(171, 1.0) -- UHF
            _device:set_argument_value(147, 1.0) -- VHF FM
        end
    end


    -- VHF AM
    -- Set radio data
    _data.radios[2].name = "AN/ARC-186(V) AM"
    _data.radios[2].freq = SR.getRadioFrequency(55)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 133, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 225, { 0.0, 1.0 }, false) * SR.getButtonPosition(226)


    -- UHF
    -- Set radio data
    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(54)
    
    local modulation = SR.getSelectorPosition(162, 0.1)

    --is HQ selected (A on the Radio)
    if modulation == 2 then
        _data.radios[3].modulation = 4
    else
        _data.radios[3].modulation = 0
    end


    _data.radios[3].volume = SR.getRadioVolume(0, 171, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 227, { 0.0, 1.0 }, false) * SR.getButtonPosition(228)
    _data.radios[3].encMode = 2 -- Mode 2 is set by aircraft

    -- Check UHF frequency mode (0 = MNL, 1 = PRESET, 2 = GRD)
    local _selector = SR.getSelectorPosition(167, 0.1)
    if _selector == 1 then
        -- Using UHF preset channels
        local _channel = SR.getSelectorPosition(161, 0.05) + 1 --add 1 as channel 0 is channel 1
        _data.radios[3].channel = _channel
    end

    -- Check UHF function mode (0 = OFF, 1 = MAIN, 2 = BOTH, 3 = ADF)
    local uhfModeKnob = SR.getSelectorPosition(168, 0.1)
    if uhfModeKnob == 2 and _data.radios[3].freq > 1000 then
        -- Function dial set to BOTH
        -- Listen to Guard as well as designated frequency
        _data.radios[3].secFreq = 243.0 * 1000000
    else
        -- Function dial set to OFF, MAIN, or ADF
        -- Not listening to Guard secondarily
        _data.radios[3].secFreq = 0
    end


    -- VHF FM
    -- Set radio data
    _data.radios[4].name = "AN/ARC-186(V)FM"
    _data.radios[4].freq = SR.getRadioFrequency(56)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 147, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 223, { 0.0, 1.0 }, false) * SR.getButtonPosition(224)
    _data.radios[4].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft


    -- KY-58 Radio Encryption
    -- Check if encryption is being used
    local _ky58Power = SR.getButtonPosition(784)
    if _ky58Power > 0.5 and SR.getButtonPosition(783) == 0 then
        -- mode switch set to OP and powered on
        -- Power on!

        local _radio = nil
        if SR.round(SR.getButtonPosition(781), 0.1) == 0.2 then
            --crad/2 vhf - FM
            _radio = _data.radios[4]
        elseif SR.getButtonPosition(781) == 0 then
            --crad/1 uhf
            _radio = _data.radios[3]
        end

        -- Get encryption key
        local _channel = SR.getSelectorPosition(782, 0.1) + 1

        if _radio ~= nil and _channel ~= nil then
            -- Set encryption key for selected radio
            _radio.encKey = _channel
            _radio.enc = true
        end
    end


    -- Mic Switch Radio Select and Transmit - by Dyram
    -- Check Mic Switch position (UP: 751 1.0, DOWN: 751 -1.0, FWD: 752 1.0, AFT: 752 -1.0)
    if SR.getButtonPosition(752) == 1 then
        -- Mic Switch FWD pressed
        -- Check Intercom panel Rotary Selector Dial (0: INT, 1: FM, 2: VHF, 3: HF, 4: "")
        if SR.getSelectorPosition(239, 0.1) == 2 then
            -- Intercom panel set to VHF
            _data.selected = 1 -- radios[2] VHF AM
            _data.ptt = true
        elseif SR.getSelectorPosition(239, 0.1) == 0 then
            -- Intercom panel set to INT
            -- Intercom not functional, but select it anyway to be proper
            _data.selected = 0 -- radios[1] Intercom
        else
            _data.selected = -1
        end
    elseif SR.getButtonPosition(751) == -1 then
        -- Mic Switch DOWN pressed
        _data.selected = 2 -- radios[3] UHF
        _data.ptt = true
    elseif SR.getButtonPosition(752) == -1 then
        -- Mic Switch AFT pressed
        _data.selected = 3 -- radios[4] VHF FM
        _data.ptt = true
    else
        -- Mic Switch released
        _data.selected = -1
        _data.ptt = false
    end

    _data.control = 1 -- COCKPIT 

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(200,0.1)

    local iffIdent =  SR.getButtonPosition(207) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1)*100+SR.round(SR.getButtonPosition(210), 0.1)*10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000 + SR.round(SR.getButtonPosition(212), 0.1) * 1000 + SR.round(SR.getButtonPosition(213), 0.1)* 100 + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")
    return _data
end

local _fa18 = {}
_fa18.radio1 = {}
_fa18.radio2 = {}
_fa18.radio3 = {}
_fa18.radio4 = {}
_fa18.radio1.guard = 0
_fa18.radio2.guard = 0
_fa18.radio3.channel = 127 --127 is disabled for MIDS
_fa18.radio4.channel = 127
 -- initial IFF status set to -1 to indicate its not initialized, status then set depending on cold/hot start
_fa18.iff = {status=-1,mode1=-1,mode3=-1,mode4=true,control=0,expansion=false}

--[[
From NATOPS - https://info.publicintelligence.net/F18-ABCD-000.pdf (VII-23-2)

ARC-210(RT-1556 and DCS)

Frequency Band(MHz) Modulation  Guard Channel (MHz)
    30 to 87.995        FM
    *108 to 135.995     AM          121.5
    136 to 155.995      AM/FM
    156 to 173.995      FM
    225 to 399.975      AM/FM       243.0 (AM)

*Cannot transmit on 108 thru 117.995 MHz
]]--

function SR.exportRadioFA18C(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    local _ufc = SR.getListIndicatorValue(6)

    --{
    --   "UFC_Comm1Display": " 1",
    --   "UFC_Comm2Display": " 8",
    --   "UFC_MainDummy": "",
    --   "UFC_OptionCueing1": ":",
    --   "UFC_OptionCueing2": ":",
    --   "UFC_OptionCueing3": "",
    --   "UFC_OptionCueing4": ":",
    --   "UFC_OptionCueing5": "",
    --   "UFC_OptionDisplay1": "GRCV",
    --   "UFC_OptionDisplay2": "SQCH",
    --   "UFC_OptionDisplay3": "CPHR",
    --   "UFC_OptionDisplay4": "AM  ",
    --   "UFC_OptionDisplay5": "MENU",
    --   "UFC_ScratchPadNumberDisplay": "257.000",
    --   "UFC_ScratchPadString1Display": " 8",
    --   "UFC_ScratchPadString2Display": "_",
    --   "UFC_mask": ""
    -- }
    --_data.radios[3].secFreq = 243.0 * 1000000
    -- reset state on aircraft switch
    if _lastUnitId ~= _data.unitId then
        _fa18.radio1.guard = 0
        _fa18.radio2.guard = 0
        _fa18.radio3.channel = 127 --127 is disabled for MIDS
        _fa18.radio4.channel = 127
        _fa18.iff = {status=-1,mode1=-1,mode3=-1,mode4=true,control=0,expansion=false}
    end

    local getGuardFreq = function (freq,currentGuard,modulation)


        if freq > 1000000 then

            -- check if UFC is currently displaying the GRCV for this radio
            --and change state if so

            if _ufc and _ufc.UFC_OptionDisplay1 == "GRCV" then

                if _ufc.UFC_ScratchPadNumberDisplay then
                    local _ufcFreq = tonumber(_ufc.UFC_ScratchPadNumberDisplay)

                    -- if its the correct radio
                    if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq,1000) then
                        if _ufc.UFC_OptionCueing1 == ":" then

                            -- GUARD changes based on the tuned frequency
                            if freq > 108*1000000
                                    and freq < 135.995*1000000
                                    and modulation == 0 then
                                return 121.5 * 1000000
                            end
                            if freq > 108*1000000
                                    and freq < 399.975*1000000
                                    and modulation == 0 then
                                return 243 * 1000000
                            end

                            return 0
                        else
                            return 0
                        end
                    end
                end
            end

            if currentGuard > 1000 then

                if freq > 108*1000000
                        and freq < 135.995*1000000
                        and modulation == 0 then

                    return 121.5 * 1000000
                end
                if freq > 108*1000000
                        and freq < 399.975*1000000
                        and modulation == 0 then

                    return 243 * 1000000
                end
            end

            return currentGuard

        else
            -- reset state
            return 0
        end

    end

    -- VHF AM
    -- Set radio data
    _data.radios[2].name = "AN/ARC-210 - 1"
    _data.radios[2].freq = SR.getRadioFrequency(38)
    _data.radios[2].modulation = SR.getRadioModulation(38)
    _data.radios[2].volume = SR.getRadioVolume(0, 108, { 0.0, 1.0 }, false)
    -- _data.radios[2].encMode = 2 -- Mode 2 is set by aircraft
    --_data.radios[2].secFreq = 243.0 * 1000000


    local radio1Guard = getGuardFreq(_data.radios[2].freq, _fa18.radio1.guard, _data.radios[2].modulation)

    _fa18.radio1.guard = radio1Guard


    _data.radios[2].secFreq = _fa18.radio1.guard

    -- UHF
    -- Set radio data
    _data.radios[3].name = "AN/ARC-210 - 2"
    _data.radios[3].freq = SR.getRadioFrequency(39)
    _data.radios[3].modulation = SR.getRadioModulation(39)
    _data.radios[3].volume = SR.getRadioVolume(0, 123, { 0.0, 1.0 }, false)
    _data.radios[3].encMode = 2 -- Mode 2 is set by aircraft


    local radio2Guard = getGuardFreq(_data.radios[3].freq, _fa18.radio2.guard,_data.radios[3].modulation)

    _fa18.radio2.guard = radio2Guard
    _data.radios[3].secFreq = _fa18.radio2.guard

    -- KY-58 Radio Encryption
    local _ky58Power = SR.round(SR.getButtonPosition(447), 0.1)
    if _ky58Power == 0.1 and SR.round(SR.getButtonPosition(444), 0.1) == 0.1 then
        -- mode switch set to C and powered on
        -- Power on!

        -- Get encryption key
        local _channel = SR.getSelectorPosition(446, 0.1) + 1
        if _channel > 6 then
            _channel = 6 -- has two other options - lock to 6
        end

        -- _data.radios[2].encKey = _channel
        -- _data.radios[2].enc = true

        _data.radios[3].encKey = _channel
        _data.radios[3].enc = true

    end

    local getMidsChannel = function (currentChannel, mids)

        --DL page
        if _ufc.UFC_OptionDisplay4 == "VOCA" then

            if _ufc.UFC_ScratchPadString1Display == "O" and _ufc.UFC_ScratchPadString2Display == "N" then

                if _ufc.UFC_OptionCueing4 ==":" and mids == 1 then
                        --first mids

                        local chan = tonumber(_ufc.UFC_ScratchPadNumberDisplay)

                        if chan == nil then
                            return 0
                        end
                        return chan
                end
                if _ufc.UFC_OptionCueing5 ==":" and mids == 2 then
                    --first mids

                    local chan = tonumber(_ufc.UFC_ScratchPadNumberDisplay)

                    if chan == nil then
                        return 0
                    end
                    return chan
                end
            else
                return 0
            end
        end

        return currentChannel
    end


    local midsAChannel = getMidsChannel(_fa18.radio3.channel,1)

    _fa18.radio3.channel = midsAChannel

    _data.radios[4].name = "MIDS A"
    _data.radios[4].modulation = 6
    _data.radios[4].volume = SR.getRadioVolume(0, 362, { 0.0, 1.0 }, false)
    _data.radios[4].encMode = 2 -- Mode 2 is set by aircraft

    if midsAChannel < 127 and _fa18.radio3.channel > 0 then
        _data.radios[4].freq = SR.MIDS_FREQ +  (SR.MIDS_FREQ_SEPARATION * midsAChannel)
        _data.radios[4].channel = midsAChannel
    else
        _data.radios[4].freq = 1
        _data.radios[4].channel = -1
    end


    local midsBChannel = getMidsChannel(_fa18.radio4.channel,2)

    -- Set MIDS data
    _fa18.radio4.channel = midsBChannel

    _data.radios[5].name = "MIDS B"
    _data.radios[5].modulation = 6
    _data.radios[5].volume = SR.getRadioVolume(0, 361, { 0.0, 1.0 }, false)
    _data.radios[5].encMode = 2 -- Mode 2 is set by aircraft

    if midsBChannel < 127 and _fa18.radio4.channel > 0 then
        _data.radios[5].freq = SR.MIDS_FREQ +  (SR.MIDS_FREQ_SEPARATION * midsBChannel)
        _data.radios[5].channel = midsBChannel

    else
        _data.radios[5].freq = 1
        _data.radios[5].channel = -1
    end

    local validateIffCode = function (codeString, mode)
        -- returns false if code is not valid, true if it is
        if mode == 1 then
            -- mode 1 code is 2-digit and is valid if the first digit is 0-7 and the second digit is 0-3
            if #codeString < 2 then return false end
            for i = 1, #codeString do
                local c = codeString:sub(i,i)
                if i == 1 and (c == "8" or c == "9") then return false end
                if i == 2 and (c == "4" or c == "5" or c == "6" or c == "7" or c == "8" or c == "9") then
                    return false
                end
            end
        elseif mode == 3 then
            -- mode 3 code is 4-digit and is valid if all digits are 0-7
            if #codeString < 4 then return false end
            for i = 1, #codeString do
                local c = codeString:sub(i,i)
                if c == "8" or c == "9" then return false end
            end
        end
        return true
    end

    local getTransponderStatus = function (currentStatus)
        -- returns current status if status can't be read from the UFC, else returns 1 for xpdr ON or 0 for xpdr OFF
        if _ufc.UFC_OptionDisplay2 == "2   " then
            if _ufc.UFC_ScratchPadString1Display == "X" or _ufc.UFC_ScratchPadString1Display == "A" then return 1
            elseif _ufc.UFC_ScratchPadString1Display == "" then return 0
            end
        end
        return currentStatus
    end

    local getIffCode = function (currentCode, iffMode)
        -- for a given mode returns current code if status can't be read from the UFC, else returns the selected code
        -- provided it passes validation
        if _ufc.UFC_OptionDisplay2 == "2   " and _ufc.UFC_ScratchPadString1Display == "X" then
            -- UFC IFF transponder (XP) menu (don't confuse with IFF interrogator (AI) menu)

            -- Which mode's code is being edited
            local editingMode = string.sub(_ufc.UFC_ScratchPadNumberDisplay, 0, 2)

            if iffMode == 1 then
                 if _ufc.UFC_OptionCueing1 == ":" then
                     if editingMode == "1-" then
                         local code = (string.sub(_ufc.UFC_OptionDisplay1, -2))
                         if validateIffCode(code, 1) then return code end
                     end
                 else return -1
                 end

            elseif iffMode == 3 then
                 if _ufc.UFC_OptionCueing3 == ":" then
                     if editingMode == "3-" then
                         local code = (string.sub(_ufc.UFC_ScratchPadNumberDisplay, -4))
                         if validateIffCode(code, 3) then return code end
                     end
                 else return -1
                 end

            elseif iffMode == 4 then
                 if _ufc.UFC_OptionCueing4 == ":" then
                     return true
                 else return false
                 end
            end
        end
        return currentCode
    end

    -- set initial IFF status based on cold/hot start since it can't be read directly off the panel
    local batterySwitch = SR.getButtonPosition(404)
    if _fa18.iff.status == -1 then
        if batterySwitch == 0 then
            -- cold start, everything off
            _fa18.iff = {status=0,mode1=-1,mode3=-1,mode4=false,control=0,expansion=false}
        elseif batterySwitch == 0 then
            -- hot start, M4 on
            _fa18.iff = {status=1,mode1=-1,mode3=-1,mode4=true,control=0,expansion=false}
        end
    end

    local iffStatus = getTransponderStatus(_fa18.iff.status)
    local iffMode1 = getIffCode(_fa18.iff.mode1, 1)
    local iffMode3 = getIffCode(_fa18.iff.mode3, 3)
    local iffMode4 = getIffCode(_fa18.iff.mode4, 4)
    local iffIdent = SR.getButtonPosition(99)

    -- Mode 1/3 IDENT, requires mode 1 or mode 3 to be on and I/P pushbutton press
    if iffStatus == 1 and iffIdent == 1 and (iffMode1 ~= -1 or iffMode3 ~= -1) then
        iffStatus = 2
    elseif iffStatus == 2 and iffIdent == 0 then
        -- remove IDENT status when pushbutton released
        iffStatus = 1
    end

    _data.iff.status = iffStatus
    _data.iff.mode1 = iffMode1
    _data.iff.mode3 = iffMode3
    _data.iff.mode4 = iffMode4
    _data.iff.control = _fa18.iff.control
    _data.iff.expansion = _fa18.iff.expansion

    -- set current IFF settings
    _fa18.iff = _data.iff

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")
    return _data
end

local _f16 = {}
_f16.radio1 = {}
_f16.radio1.guard = 0

function SR.exportRadioF16C(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    -- UHF
    _data.radios[2].name = "AN/ARC-164"
    _data.radios[2].freq = SR.getRadioFrequency(36)
    _data.radios[2].modulation = SR.getRadioModulation(36)
    _data.radios[2].volume = SR.getRadioVolume(0, 430, { 0.0, 1.0 }, false)
    _data.radios[2].encMode = 2

    -- C&I Backup/UFC by Raffson, aka Stoner
    local _cni = SR.getButtonPosition(542)
    if _cni == 0 then
        local _buhf_func = SR.getSelectorPosition(417, 0.1)
        if _buhf_func == 2 then
            -- Function set to BOTH --> also listen to guard
            _data.radios[2].secFreq = 243.0 * 1000000
        else
            _data.radios[2].secFreq = 0
        end

        -- Check UHF frequency mode (0 = MNL, 1 = PRESET, 2 = GRD)
        local _selector = SR.getSelectorPosition(416, 0.1)
        if _selector == 1 then
            -- Using UHF preset channels
            local _channel = SR.getSelectorPosition(410, 0.05) + 1 --add 1 as channel 0 is channel 1
            _data.radios[2].channel = _channel
        end
    else
        -- Parse the UFC - LOOK FOR BOTH (OR MAIN)
        local ded = SR.getListIndicatorValue(6)
        --PANEL 6{"Active Frequency or Channel":"305.00","Asterisks on Scratchpad_lhs":"*","Asterisks on Scratchpad_rhs":"*","Bandwidth":"NB","Bandwidth_placeholder":"","COM 1 Mode":"UHF","Preset Frequency":"305.00","Preset Frequency_placeholder":"","Preset Label":"PRE     a","Preset Number":" 1","Preset Number_placeholder":"","Receiver Mode":"BOTH","Scratchpad":"305.00","Scratchpad_placeholder":"","TOD Label":"TOD"}
        
        if ded and ded["Receiver Mode"] ~= nil and  ded["COM 1 Mode"] == "UHF" then
            if ded["Receiver Mode"] == "BOTH" then
                _f16.radio1.guard= 243.0 * 1000000
            else
                _f16.radio1.guard= 0
            end
        else
            if _data.radios[2].freq < 1000 then
                _f16.radio1.guard= 0
            end
        end

        _data.radios[2].secFreq = _f16.radio1.guard
            
     end

    -- VHF
    _data.radios[3].name = "AN/ARC-222"
    _data.radios[3].freq = SR.getRadioFrequency(38)
    _data.radios[3].modulation = SR.getRadioModulation(38)
    _data.radios[3].volume = SR.getRadioVolume(0, 431, { 0.0, 1.0 }, false)
    _data.radios[3].encMode = 2
    _data.radios[3].guardFreqMode = 1
    _data.radios[3].secFreq = 121.5 * 1000000

    -- KY-58 Radio Encryption
    local _ky58Power = SR.round(SR.getButtonPosition(707), 0.1)

    if _ky58Power == 0.5 and SR.round(SR.getButtonPosition(705), 0.1) == 0.1 then
        -- mode switch set to C and powered on
        -- Power on and correct mode selected
        -- Get encryption key
        local _channel = SR.getSelectorPosition(706, 0.1)

        local _cipherSwitch = SR.round(SR.getButtonPosition(701), 1)
        local _radio = nil
        if _cipherSwitch > 0.5 then
            -- CRAD1 (UHF)
            _radio = _data.radios[2]
        elseif _cipherSwitch < -0.5 then
            -- CRAD2 (VHF)
            _radio = _data.radios[3]
        end
        if _radio ~= nil and _channel > 0 and _channel < 7 then
            _radio.encKey = _channel
            _radio.enc = true
            _radio.volume = SR.getRadioVolume(0, 708, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 432, { 0.0, 1.0 }, false)--User KY-58 volume if chiper is used
        end
    end

    local _cipherOnly =  SR.round(SR.getButtonPosition(443),1) < -0.5 --If HOT MIC CIPHER Switch, HOT MIC / OFF / CIPHER set to CIPHER, allow only cipher
    if _cipherOnly and _data.radios[3].enc ~=true then
        _data.radios[3].freq = 0
    end
    if _cipherOnly and _data.radios[2].enc ~=true then
        _data.radios[2].freq = 0
    end

    _data.control = 0; -- SRS Hotas Controls

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(540,0.1)

    local iffIdent =  SR.getButtonPosition(125) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

    end

    local modeSelector =  SR.getButtonPosition(553)

    if modeSelector == -1 then

        --shares a dial with the mode 3, limit number to max 3
        local _secondDigit = SR.round(SR.getButtonPosition(548), 0.1)*10

        if _secondDigit > 3 then
            _secondDigit = 3
        end

        _data.iff.mode1 = SR.round(SR.getButtonPosition(546), 0.1)*100 + _secondDigit
    else
        _data.iff.mode1 = -1
    end

    if modeSelector ~= 0 then
        _data.iff.mode3 = SR.round(SR.getButtonPosition(546), 0.1) * 10000 + SR.round(SR.getButtonPosition(548), 0.1) * 1000 + SR.round(SR.getButtonPosition(550), 0.1)* 100 + SR.round(SR.getButtonPosition(552), 0.1) * 10
    else
        _data.iff.mode3 = -1
    end

    if iffPower == 4 and modeSelector ~= 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(541)

    local mode4Code = SR.getButtonPosition(543)

    if mode4On == 0 and mode4Code ~= -1 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")

    return _data
end

function SR.exportRadioF86Sabre(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "AN/ARC-27"
    _data.radios[2].freq = SR.getRadioFrequency(26)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 806, { 0.1, 0.9 }, false)

    -- get channel selector
    local _channel = SR.getSelectorPosition(807, 0.01)

    if _channel >= 1 then
        _data.radios[2].channel = _channel
    end

    _data.selected = 1

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(805, 0.1)
    if uhfModeKnob == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    -- Check PTT
    if (SR.getButtonPosition(213)) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
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

    _data.control = 0; -- Hotas Controls

    return _data;
end

function SR.exportRadioMIG15(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "RSI-6K"
    _data.radios[2].freq = SR.getRadioFrequency(30)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 126, { 0.1, 0.9 }, false)

    _data.selected = 1

    -- Check PTT
    if (SR.getButtonPosition(202)) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
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

    _data.control = 0; -- Hotas Controls radio

    return _data;
end

function SR.exportRadioMIG19(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "RSIU-4V"
    _data.radios[2].freq = SR.getRadioFrequency(17)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 327, { 0.0, 1.0 }, false)

    _data.selected = 1

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

    _data.control = 0; -- Hotas Controls radio

    return _data;
end

function SR.exportRadioMIG21(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "R-832"
    _data.radios[2].freq = SR.getRadioFrequency(22)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 210, { 0.0, 1.0 }, false)

    _data.radios[2].channel = SR.getSelectorPosition(211, 0.05)

    _data.selected = 1

    if (SR.getButtonPosition(315)) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
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

    _data.control = 0; -- hotas radio

    return _data;
end

function SR.exportRadioF5E(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "AN/ARC-164"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].volume = SR.getRadioVolume(0, 309, { 0.0, 1.0 }, false)

    local modulation = SR.getSelectorPosition(327, 0.1)

    --is HQ selected (A on the Radio)
    if modulation == 0 then
        _data.radios[2].modulation = 4
    else
        _data.radios[2].modulation = 0
    end

    -- get channel selector
    local _selector = SR.getSelectorPosition(307, 0.1)

    if _selector == 1 then
        _data.radios[2].channel = SR.getSelectorPosition(300, 0.05) + 1 --add 1 as channel 0 is channel 1
    end

    _data.selected = 1

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(311, 0.1)

    if uhfModeKnob == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    -- Check PTT - By Tarres!
    --NWS works as PTT when wheels up
    if (SR.getButtonPosition(135) > 0.5 or (SR.getButtonPosition(131) > 0.5 and SR.getButtonPosition(83) > 0.5)) then
        _data.ptt = true
    else
        _data.ptt = false
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

    _data.control = 0; -- hotas radio

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(200,0.1)

    local iffIdent =  SR.getButtonPosition(207) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1)*100+SR.round(SR.getButtonPosition(210), 0.1)*10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000 + SR.round(SR.getButtonPosition(212), 0.1) * 1000 + SR.round(SR.getButtonPosition(213), 0.1)* 100 + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    return _data;
end

function SR.exportRadioP51(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "SCR522A"
    _data.radios[2].freq = SR.getRadioFrequency(24)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 116, { 0.0, 1.0 }, false)

    _data.selected = 1

    if (SR.getButtonPosition(44)) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
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

    _data.control = 0; -- hotas radio

    return _data;
end


function SR.exportRadioP47(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "Only one radio by default" }

    _data.radios[2].name = "SCR522"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 77, { 0.0, 1.0 }, false)

    _data.selected = 1

    --Cant find the button in the cockpit?
    -- if (SR.getButtonPosition(44)) > 0.5 then
    --     _data.ptt = true
    -- else
    --     _data.ptt = false
    -- end

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

    _data.control = 0; -- hotas radio

    return _data;
end

function SR.exportRadioFW190(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FuG 16ZY"
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = 0
    _data.radios[2].volMode = 1
    _data.radios[2].volume = 1.0  --SR.getRadioVolume(0, 83,{0.0,1.0},true) Volume knob is not behaving..

    _data.selected = 1


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

    _data.control = 0; -- hotas radio

    return _data;
end

function SR.exportRadioBF109(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    
    _data.radios[2].name = "FuG 16ZY"
    _data.radios[2].freq = SR.getRadioFrequency(14)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 130, { 0.0, 1.0 }, false)

    _data.selected = 1

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

    _data.control = 0; -- hotas radio

    return _data;
end

function SR.exportRadioSpitfireLFMkIX (_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "A.R.I. 1063" --minimal bug in the ME GUI and in the LUA. SRC5222 is the P-51 radio.
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = 0
    _data.radios[2].volMode = 1
    _data.radios[2].volume = 1.0 --no volume control

    _data.selected = 1

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

    _data.control = 0; -- no ptt, same as the FW and 109. No connector.

    return _data;
end
function SR.exportRadioC101EB(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "Pull the HOT MIC breaker up to enable HOT MIC" }

    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = SR.getRadioVolume(0, 403, { 0.0, 1.0 }, false)

    _data.radios[2].name = "AN/ARC-164 UHF"
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 234, { 0.0, 1.0 }, false)

    local _selector = SR.getSelectorPosition(232, 0.25)

    if _selector ~= 0 then
        _data.radios[2].freq = SR.getRadioFrequency(11)
    else
        _data.radios[2].freq = 1
    end

    -- UHF Guard
    if _selector == 2 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    _data.radios[3].name = "AN/ARC-134"
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 412, { 0.0, 1.0 }, false)

    _data.radios[3].freq = SR.getRadioFrequency(10)

    local _seat = GetDevice(0):get_current_seat()

    local _selector

    if _seat == 0 then
    	_selector = SR.getSelectorPosition(404, 0.5)
    else
    	_selector = SR.getSelectorPosition(947, 0.5)
    end

    if _selector == 1 then
        _data.selected = 1
    elseif _selector == 2 then
        _data.selected = 2
    else
        _data.selected = 0
    end

    --TODO figure our which cockpit you're in? So we can have controls working in the rear?

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(347,0.25)

    -- SR.log("IFF iffPower"..iffPower.."\n\n")

    local iffIdent =  SR.getButtonPosition(361) -- -1 is off 0 or more is on

    if iffPower <= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 1

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(349)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(355), 0.1)*100+SR.round(SR.getButtonPosition(356), 0.1)*10

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(351)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(357), 0.1) * 10000 + SR.round(SR.getButtonPosition(358), 0.1) * 1000 + SR.round(SR.getButtonPosition(359), 0.1)* 100 + SR.round(SR.getButtonPosition(360), 0.1) * 10

    if mode3On == 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(354)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end
    _data.control = 1; -- full radio

    local frontHotMic =  SR.getButtonPosition(287)
    local rearHotMic =   SR.getButtonPosition(891)
    -- only if The hot mic talk button (labeled TALK in cockpit) is up
    if frontHotMic == 1 or rearHotMic == 1 then
       _data.intercomHotMic = true
    end

    return _data;
end

function SR.exportRadioC101CC(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "The hot mic talk button (labeled TALK in cockpit) must be pulled out" }

    -- TODO - figure out channels.... it saves state??
    -- figure out volume
    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = SR.getRadioVolume(0, 403, { 0.0, 1.0 }, false)

    _data.radios[2].name = "V/TVU-740"
    _data.radios[2].freq = SR.getRadioFrequency(11)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = 1.0--SR.getRadioVolume(0, 234,{0.0,1.0},false)
    _data.radios[2].volMode = 1

    local _channel = SR.getButtonPosition(231)

    -- SR.log("Channel SELECTOR: ".. SR.getButtonPosition(231).."\n")


    local uhfModeKnob = SR.getSelectorPosition(232, 0.1)
    if uhfModeKnob == 2 and _data.radios[2].freq > 1000 then
        -- Function dial set to BOTH
        -- Listen to Guard as well as designated frequency
        _data.radios[2].secFreq = 243.0 * 1000000
    else
        -- Function dial set to OFF, MAIN, or ADF
        -- Not listening to Guard secondarily
        _data.radios[2].secFreq = 0
    end

    _data.radios[3].name = "VHF-20B"
    _data.radios[3].modulation = 0
    _data.radios[3].volume = 1.0 --SR.getRadioVolume(0, 412,{0.0,1.0},false)
    _data.radios[3].volMode = 1

    --local _vhfPower = SR.getSelectorPosition(413,1.0)
    --
    --if _vhfPower == 1 then
    _data.radios[3].freq = SR.getRadioFrequency(10)
    --else
    --    _data.radios[3].freq = 1
    --end
    --
    local _seat = GetDevice(0):get_current_seat()
    local _selector

    if _seat == 0 then
    	_selector = SR.getSelectorPosition(404, 0.05)
    else
    	_selector = SR.getSelectorPosition(947, 0.05)
    end

    if _selector == 0 then
        _data.selected = 0
    elseif _selector == 2 then
        _data.selected = 2
    elseif _selector == 12 then
        _data.selected = 1
    else
        _data.selected = -1
    end

    --TODO figure our which cockpit you're in? So we can have controls working in the rear?

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(347,0.25)

    --SR.log("IFF iffPower"..iffPower.."\n\n")

    local iffIdent =  SR.getButtonPosition(361) -- -1 is off 0 or more is on

    if iffPower <= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 1

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(349)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(355), 0.1)*100+SR.round(SR.getButtonPosition(356), 0.1)*10

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(351)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(357), 0.1) * 10000 + SR.round(SR.getButtonPosition(358), 0.1) * 1000 + SR.round(SR.getButtonPosition(359), 0.1)* 100 + SR.round(SR.getButtonPosition(360), 0.1) * 10

    if mode3On == 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(354)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    _data.control = 1;


    local frontHotMic =  SR.getButtonPosition(287)
    local rearHotMic =   SR.getButtonPosition(891)
    -- only if The hot mic talk button (labeled TALK in cockpit) is up
    if frontHotMic == 1 or rearHotMic == 1 then
       _data.intercomHotMic = true
    end

    return _data;
end

function SR.exportRadioHawk(_data)

    local MHZ = 1000000

    _data.radios[2].name = "AN/ARC-164 UHF"

    local _selector = SR.getSelectorPosition(221, 0.25)

    if _selector == 1 or _selector == 2 then

        local _hundreds = SR.getSelectorPosition(226, 0.25) * 100 * MHZ
        local _tens = SR.round(SR.getKnobPosition(0, 227, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 10 * MHZ
        local _ones = SR.round(SR.getKnobPosition(0, 228, { 0.0, 0.9 }, { 0, 9 }), 0.1) * MHZ
        local _tenth = SR.round(SR.getKnobPosition(0, 229, { 0.0, 0.9 }, { 0, 9 }), 0.1) * 100000
        local _hundreth = SR.round(SR.getKnobPosition(0, 230, { 0.0, 0.3 }, { 0, 3 }), 0.1) * 10000

        _data.radios[2].freq = _hundreds + _tens + _ones + _tenth + _hundreth
    else
        _data.radios[2].freq = 1
    end
    _data.radios[2].modulation = 0
    _data.radios[2].volume = 1

    _data.radios[3].name = "ARI 23259/1"
    _data.radios[3].freq = SR.getRadioFrequency(7)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = 1

    --guard mode for UHF Radio
    local _uhfKnob = SR.getSelectorPosition(221, 0.25)
    if _uhfKnob == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    --- is VHF ON?
    if SR.getSelectorPosition(391, 0.2) == 0 then
        _data.radios[3].freq = 1
    end
    --guard mode for VHF Radio
    local _vhfKnob = SR.getSelectorPosition(391, 0.2)
    if _vhfKnob == 2 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 121.5 * 1000000
    end

    -- Radio Select Switch
    if (SR.getButtonPosition(265)) > 0.5 then
        _data.selected = 2
    else
        _data.selected = 1
    end

    _data.control = 1; -- full radio

    return _data;
end

local _mirageEncStatus = false
local _previousEncState = 0
function SR.exportRadioM2000C(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "TRT ERA 7000 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(19)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 707, { 0.0, 1.0 }, false)

    -- get channel selector
    local _selector = SR.getSelectorPosition(448, 0.50)

    if _selector == 1 then
        _data.radios[2].channel = SR.getSelectorPosition(445, 0.05)  --add 1 as channel 0 is channel 1
    end

    _data.radios[3].name = "TRT ERA 7200 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(20)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 706, { 0.0, 1.0 }, false)

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- 3 is Incockpit toggle + Gui Enc Key setting

    --  local _switch = SR.getButtonPosition(700) -- remmed, the connectors are being coded, maybe soon will be a full radio.

    --    if _switch == 1 then
    --      _data.selected = 0
    --  else
    --     _data.selected = 1
    -- end

    --guard mode for V/UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(446, 0.25) -- TODO!
    if uhfModeKnob == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 243.0 * 1000000
    end

    -- reset state on aircraft switch
    if _lastUnitId ~= _data.unitId then
        _mirageEncStatus = false
        _previousEncState = 0
    end

    -- handle the button no longer toggling...
    if SR.getButtonPosition(432) > 0.5 and _previousEncState < 0.5 then
        --431

        if _mirageEncStatus then
            _mirageEncStatus = false
        else
            _mirageEncStatus = true
        end
    end

    _data.radios[3].enc = _mirageEncStatus

    _previousEncState = SR.getButtonPosition(432)

    _data.control = 0; -- partial radio, allows hotkeys

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}


    local iffIdent =  SR.getButtonPosition(383) -- -1 is off 0 or more is on

    -- No power switch - always on
    _data.iff.status = 1 -- NORMAL

    if iffIdent == 1 then
        _data.iff.status = 2 -- IDENT (BLINKY THING)
    end

    local mode1On =  SR.getButtonPosition(384)

    local mode1Digit1 = SR.round(SR.getButtonPosition(377), 0.1)*100
    local mode1Digit2 = SR.round(SR.getButtonPosition(378), 0.1)*10

    if mode1Digit1 > 70 then
        mode1Digit1 = 70
    end

    if mode1Digit2 > 3 then
        mode1Digit2 = 3
    end

    _data.iff.mode1 = mode1Digit1+mode1Digit2

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(386)

    local mode3Digit1 = SR.round(SR.getButtonPosition(379), 0.1)*10000
    local mode3Digit2 = SR.round(SR.getButtonPosition(380), 0.1)*1000
    local mode3Digit3 = SR.round(SR.getButtonPosition(381), 0.1)*100
    local mode3Digit4 = SR.round(SR.getButtonPosition(382), 0.1)*10

    if mode3Digit1 > 7000 then
        mode3Digit1 = 7000
    end

    if mode3Digit2 > 700 then
        mode3Digit2 = 700
    end

    if mode3Digit3 > 70 then
        mode3Digit3 = 70
    end

    if mode3Digit4 > 7 then
        mode3Digit4 = 7
    end

    _data.iff.mode3 = mode3Digit1+mode3Digit2+mode3Digit3+mode3Digit4

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.round(SR.getButtonPosition(598),0.1)*10

    if mode4On == 2 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    return _data
end

local newJF17Interface = nil

function SR.exportRadioJF17(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "COMM1 VHF Radio"
    _data.radios[2].freq = SR.getRadioFrequency(25)
    _data.radios[2].modulation = SR.getRadioModulation(25)
    _data.radios[2].volume = SR.getRadioVolume(0, 934, { 0.0, 1.0 }, false)
    _data.radios[2].secFreq = GetDevice(25):get_guard_plus_freq()

    _data.radios[3].name = "COMM2 UHF Radio"
    _data.radios[3].freq = SR.getRadioFrequency(26)
    _data.radios[3].modulation = SR.getRadioModulation(26)
    _data.radios[3].volume = SR.getRadioVolume(0, 938, { 0.0, 1.0 }, false)
    _data.radios[3].secFreq = GetDevice(26):get_guard_plus_freq()

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



    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

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

    return _data
end

local _av8 = {}
_av8.radio1 = {}
_av8.radio2 = {}
_av8.radio1.guard = 0
_av8.radio1.encKey = -1
_av8.radio1.enc = false
_av8.radio2.guard = 0
_av8.radio2.encKey = -1
_av8.radio2.enc = false

function SR.exportRadioAV8BNA(_data)
    
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    local _ufc = SR.getListIndicatorValue(6)

    --{
    --    "ODU_DISPLAY":"",
    --    "ODU_Option_1_Text":TR-G",
    --    "ODU_Option_2_Text":" ",
    --    "ODU_Option_3_Slc":":",
    --    "ODU_Option_3_Text":"SQL",
    --    "ODU_Option_4_Text":"PLN",
    --    "ODU_Option_5_Text":"CD 0"
    -- }

    --SR.log("UFC:\n"..SR.JSON:encode(_ufc).."\n\n")
    local _ufcScratch = SR.getListIndicatorValue(5)

    --{
    --    "UFC_DISPLAY":"",
    --    "ufc_chnl_1_m":"M",
    --    "ufc_chnl_2_m":"M",
    --    "ufc_right_position":"127.500"
    -- }

    --SR.log("UFC Scratch:\n"..SR.JSON:encode(SR.getListIndicatorValue(5)).."\n\n")

    if _lastUnitId ~= _data.unitId then
        _av8.radio1.guard = 0
        _av8.radio2.guard = 0
    end

    local getGuardFreq = function (freq,currentGuard)


        if freq > 1000000 then

            -- check if LEFT UFC is currently displaying the TR-G for this radio
            --and change state if so

            if _ufcScratch and _ufc and _ufcScratch.ufc_right_position then
                local _ufcFreq = tonumber(_ufcScratch.ufc_right_position)

                if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq,1000) then
                    if _ufc.ODU_Option_1_Text == "TR-G" then
                        return 243.0 * 1000000
                    else
                        return 0
                    end
                end
            end


            return currentGuard

        else
            -- reset state
            return 0
        end

    end

    local getEncryption = function ( freq, currentEnc,currentEncKey )
    if freq > 1000000 then

            -- check if LEFT UFC is currently displaying the encryption for this radio
 

            if _ufcScratch and _ufcScratch and _ufcScratch.ufc_right_position then
                local _ufcFreq = tonumber(_ufcScratch.ufc_right_position)

                if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq,1000) then
                    if _ufc.ODU_Option_4_Text == "CIPH" then

                        -- validate number
                        -- ODU_Option_5_Text
                        if string.find(_ufc.ODU_Option_5_Text, "CD",1,true) then

                          local cduStr = string.gsub(_ufc.ODU_Option_5_Text, "CD ", ""):gsub("^%s*(.-)%s*$", "%1")

                            --remove CD and trim
                            local encNum = tonumber(cduStr)

                            if encNum and encNum > 0 then 
                                return true,encNum
                            else
                                return false,-1
                            end
                        else
                            return false,-1
                        end
                    else
                        return false,-1
                    end
                end
            end


            return currentEnc,currentEncKey

        else
            -- reset state
            return false,-1
        end
end



    _data.radios[2].name = "ARC-210 COM 1"
    _data.radios[2].freq = SR.getRadioFrequency(2)
    _data.radios[2].modulation = SR.getRadioModulation(2)
    _data.radios[2].volume = SR.getRadioVolume(0, 298, { 0.0, 1.0 }, false)
    _data.radios[2].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft

    local radio1Guard = getGuardFreq(_data.radios[2].freq, _av8.radio1.guard)

    _av8.radio1.guard = radio1Guard
    _data.radios[2].secFreq = _av8.radio1.guard

    local radio1Enc, radio1EncKey = getEncryption(_data.radios[2].freq, _av8.radio1.enc, _av8.radio1.encKey)

    _av8.radio1.enc = radio1Enc
    _av8.radio1.encKey = radio1EncKey

    if _av8.radio1.enc then
        _data.radios[2].enc = _av8.radio1.enc 
        _data.radios[2].encKey = _av8.radio1.encKey 
    end

    
    -- get channel selector
    --  local _selector  = SR.getSelectorPosition(448,0.50)

    --if _selector == 1 then
    --_data.radios[2].channel =  SR.getSelectorPosition(178,0.01)  --add 1 as channel 0 is channel 1
    --end

    _data.radios[3].name = "ARC-210 COM 2"
    _data.radios[3].freq = SR.getRadioFrequency(3)
    _data.radios[3].modulation = SR.getRadioModulation(3)
    _data.radios[3].volume = SR.getRadioVolume(0, 299, { 0.0, 1.0 }, false)
    _data.radios[3].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft

    local radio2Guard = getGuardFreq(_data.radios[3].freq, _av8.radio2.guard)

    _av8.radio2.guard = radio2Guard
    _data.radios[3].secFreq = _av8.radio2.guard

    local radio2Enc, radio2EncKey = getEncryption(_data.radios[3].freq, _av8.radio2.enc, _av8.radio2.encKey)

    _av8.radio2.enc = radio2Enc
    _av8.radio2.encKey = radio2EncKey

    if _av8.radio2.enc then
        _data.radios[3].enc = _av8.radio2.enc 
        _data.radios[3].encKey = _av8.radio2.encKey 
    end

    --https://en.wikipedia.org/wiki/AN/ARC-210

    -- EXTRA Radio - temporary extra radio
    --https://en.wikipedia.org/wiki/AN/ARC-210
    --_data.radios[4].name = "ARC-210 COM 3"
    --_data.radios[4].freq = 251.0*1000000 --225-399.975 MHZ
    --_data.radios[4].modulation = 0
    --_data.radios[4].secFreq = 243.0*1000000
    --_data.radios[4].volume = 1.0
    --_data.radios[4].freqMin = 108*1000000
    --_data.radios[4].freqMax = 512*1000000
    --_data.radios[4].expansion = false
    --_data.radios[4].volMode = 1
    --_data.radios[4].freqMode = 1
    --_data.radios[4].encKey = 1
    --_data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting


    _data.selected = 1
    _data.control = 0; -- partial radio, allows hotkeys

    return _data
end

--for F-14
function SR.exportRadioF14(_data)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local ics_devid = 2
    local arc159_devid = 3
    local arc182_devid = 4

    local ICS_device = GetDevice(ics_devid)
    local ARC159_device = GetDevice(arc159_devid)
    local ARC182_device = GetDevice(arc182_devid)

    local intercom_transmit = ICS_device:intercom_transmit()
    local ARC159_ptt = ARC159_device:is_ptt_pressed()
    local ARC182_ptt = ARC182_device:is_ptt_pressed()

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = ICS_device:get_volume()

    _data.radios[2].name = "AN/ARC-159(V)"
    _data.radios[2].freq = ARC159_device:is_on() and SR.round(ARC159_device:get_frequency(), 5000) or 1
    _data.radios[2].modulation = ARC159_device:get_modulation()
    _data.radios[2].volume = ARC159_device:get_volume()
    if ARC159_device:is_guard_enabled() then
        _data.radios[2].secFreq = 243.0 * 1000000
    else
        _data.radios[2].secFreq = 0
    end
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].encKey = ICS_device:get_ky28_key()
    _data.radios[2].enc = ICS_device:is_arc159_encrypted()
    _data.radios[2].encMode = 2

    _data.radios[3].name = "AN/ARC-182(V)"
    _data.radios[3].freq = ARC182_device:is_on() and SR.round(ARC182_device:get_frequency(), 5000) or 1
    _data.radios[3].modulation = ARC182_device:get_modulation()
    _data.radios[3].volume = ARC182_device:get_volume()
    if ARC182_device:is_guard_enabled() then
        _data.radios[3].secFreq = SR.round(ARC182_device:get_guard_freq(), 5000)
    else
        _data.radios[3].secFreq = 0
    end
    _data.radios[3].freqMin = 30 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].encKey = ICS_device:get_ky28_key()
    _data.radios[3].enc = ICS_device:is_arc182_encrypted()
    _data.radios[3].encMode = 2

    if (ARC182_ptt) then
        _data.selected = 2 -- radios[3] ARC-182
        _data.ptt = true
    elseif (ARC159_ptt) then
        _data.selected = 1 -- radios[2] ARC-159
        _data.ptt = true
    elseif (intercom_transmit) then
        _data.selected = 0 -- radios[1] intercom
        _data.ptt = true
    else
        _data.selected = -1
        _data.ptt = false
    end

    -- handle simultaneous transmission
    if _data.selected ~= 0 and _data.ptt then
        local xmtrSelector = SR.getButtonPosition(381) --402

        if xmtrSelector == 0 then
            _data.radios[2].simul =true
            _data.radios[3].simul =true
        end

    end


    -- _data.intercomHotMic = true  --402 for the hotmic switch
    _data.control = 1 -- full radio

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(184,0.25)

    local iffIdent =  SR.getButtonPosition(167)

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL


        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        if iffIdent == -1 then
            if ARC159_ptt then -- ONLY on UHF radio PTT press
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(162)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(201,0.11111), 0.1)*10+SR.round(SR.getSelectorPosition(200,0.11111), 0.1)


    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(164)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(199,0.11111), 0.1) * 1000 + SR.round(SR.getSelectorPosition(198,0.11111), 0.1) * 100 + SR.round(SR.getSelectorPosition(2261,0.11111), 0.1)* 10 + SR.round(SR.getSelectorPosition(2262,0.11111), 0.1)

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(181)

    if mode4On == 0 then
        _data.iff.mode4 = false
    else
        _data.iff.mode4 = true
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")

    return _data
end

function SR.exportRadioAJS37(_data)

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FR 22"
    _data.radios[2].freq = SR.getRadioFrequency(31)
    _data.radios[2].modulation = SR.getRadioModulation(31)

    --   local _modulation =SR.getButtonPosition(3008)

    --   if _modulation > 0.5 then
    --       _data.radios[3].modulation = 1
    --   else
    --       _data.radios[3].modulation = 0
    --   end

    _data.radios[2].volume = 1.0
    _data.radios[2].volMode = 1

    _data.radios[3].name = "FR 24"
    _data.radios[3].freq = SR.getRadioFrequency(30)
    _data.radios[3].modulation = SR.getRadioModulation(30)
    _data.radios[3].volume = 1.0-- SR.getRadioVolume(0, 3112,{0.00001,1.0},false) volume not working yet
    _data.radios[3].volMode = 1

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-164 UHF"
    _data.radios[4].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 225 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].expansion = true
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0;
    _data.selected = 1

    return _data
end

function SR.getRadioVolume(_deviceId, _arg, _minMax, _invert)

    local _device = GetDevice(_deviceId)

    if not _minMax then
        _minMax = { 0.0, 1.0 }
    end

    if _device then
        local _val = tonumber(_device:get_argument_value(_arg))
        local _reRanged = SR.rerange(_val, _minMax, { 0.0, 1.0 })  --re range to give 0.0 - 1.0

        if _invert then
            return SR.round(math.abs(1.0 - _reRanged), 0.005)
        else
            return SR.round(_reRanged, 0.005);
        end
    end
    return 1.0
end

function SR.getKnobPosition(_deviceId, _arg, _minMax, _mapMinMax)

    local _device = GetDevice(_deviceId)

    if _device then
        local _val = tonumber(_device:get_argument_value(_arg))
        local _reRanged = SR.rerange(_val, _minMax, _mapMinMax)

        return _reRanged
    end
    return -1
end

function SR.getSelectorPosition(_args, _step)
    local _value = GetDevice(0):get_argument_value(_args)
    local _num = math.abs(tonumber(string.format("%.0f", (_value) / _step)))

    return _num

end

function SR.getButtonPosition(_args)
    local _value = GetDevice(0):get_argument_value(_args)

    return _value

end

function SR.getRadioFrequency(_deviceId, _roundTo)
    local _device = GetDevice(_deviceId)

    if not _roundTo then
        _roundTo = 5000
    end

    if _device then
        if _device:is_on() then
            -- round as the numbers arent exact
            return SR.round(_device:get_frequency(), _roundTo)
        end
    end
    return 1
end

function SR.getRadioModulation(_deviceId)
    local _device = GetDevice(_deviceId)

    local _modulation = 0

    if _device then

        pcall(function()
            _modulation = _device:get_modulation()
        end)

    end
    return _modulation
end

function SR.rerange(_val, _minMax, _limitMinMax)
    return ((_limitMinMax[2] - _limitMinMax[1]) * (_val - _minMax[1]) / (_minMax[2] - _minMax[1])) + _limitMinMax[1];

end

function SR.round(number, step)
    if number == 0 then
        return 0
    else
        return math.floor((number + step / 2) / step) * step
    end
end

function SR.nearlyEqual(a, b, diff)
    return math.abs(a - b) < diff
end

-- SOURCE: DCS-BIOS! Thank you! https://dcs-bios.readthedocs.io/
-- The function return a table with values of given indicator
-- The value is retrievable via a named index. e.g. TmpReturn.txt_digits
function SR.getListIndicatorValue(IndicatorID)
    local ListIindicator = list_indication(IndicatorID)
    local TmpReturn = {}

    if ListIindicator == "" then
        return nil
    end

    local ListindicatorMatch = ListIindicator:gmatch("-----------------------------------------\n([^\n]+)\n([^\n]*)\n")
    while true do
        local Key, Value = ListindicatorMatch()
        if not Key then
            break
        end
        TmpReturn[Key] = Value
    end

    return TmpReturn
end


function SR.basicSerialize(var)
    if var == nil then
        return "\"\""
    else
        if ((type(var) == 'number') or
                (type(var) == 'boolean') or
                (type(var) == 'function') or
                (type(var) == 'table') or
                (type(var) == 'userdata') ) then
            return tostring(var)
        elseif type(var) == 'string' then
            var = string.format('%q', var)
            return var
        end
    end
end


function SR.tableShow(tbl, loc, indent, tableshow_tbls) --based on serialize_slmod, this is a _G serialization
    tableshow_tbls = tableshow_tbls or {} --create table of tables
    loc = loc or ""
    indent = indent or ""
    if type(tbl) == 'table' then --function only works for tables!
        tableshow_tbls[tbl] = loc

        local tbl_str = {}

        tbl_str[#tbl_str + 1] = indent .. '{\n'

        for ind,val in pairs(tbl) do -- serialize its fields
            if type(ind) == "number" then
                tbl_str[#tbl_str + 1] = indent
                tbl_str[#tbl_str + 1] = loc .. '['
                tbl_str[#tbl_str + 1] = tostring(ind)
                tbl_str[#tbl_str + 1] = '] = '
            else
                tbl_str[#tbl_str + 1] = indent
                tbl_str[#tbl_str + 1] = loc .. '['
                tbl_str[#tbl_str + 1] = SR.basicSerialize(ind)
                tbl_str[#tbl_str + 1] = '] = '
            end

            if ((type(val) == 'number') or (type(val) == 'boolean')) then
                tbl_str[#tbl_str + 1] = tostring(val)
                tbl_str[#tbl_str + 1] = ',\n'
            elseif type(val) == 'string' then
                tbl_str[#tbl_str + 1] = SR.basicSerialize(val)
                tbl_str[#tbl_str + 1] = ',\n'
            elseif type(val) == 'nil' then -- won't ever happen, right?
                tbl_str[#tbl_str + 1] = 'nil,\n'
            elseif type(val) == 'table' then
                if tableshow_tbls[val] then
                    tbl_str[#tbl_str + 1] = tostring(val) .. ' already defined: ' .. tableshow_tbls[val] .. ',\n'
                else
                    tableshow_tbls[val] = loc ..    '[' .. SR.basicSerialize(ind) .. ']'
                    tbl_str[#tbl_str + 1] = tostring(val) .. ' '
                    tbl_str[#tbl_str + 1] = SR.tableShow(val,  loc .. '[' .. SR.basicSerialize(ind).. ']', indent .. '        ', tableshow_tbls)
                    tbl_str[#tbl_str + 1] = ',\n'
                end
            elseif type(val) == 'function' then
                if debug and debug.getinfo then
                    local fcnname = tostring(val)
                    local info = debug.getinfo(val, "S")
                    if info.what == "C" then
                        tbl_str[#tbl_str + 1] = string.format('%q', fcnname .. ', C function') .. ',\n'
                    else
                        if (string.sub(info.source, 1, 2) == [[./]]) then
                            tbl_str[#tbl_str + 1] = string.format('%q', fcnname .. ', defined in (' .. info.linedefined .. '-' .. info.lastlinedefined .. ')' .. info.source) ..',\n'
                        else
                            tbl_str[#tbl_str + 1] = string.format('%q', fcnname .. ', defined in (' .. info.linedefined .. '-' .. info.lastlinedefined .. ')') ..',\n'
                        end
                    end

                else
                    tbl_str[#tbl_str + 1] = 'a function,\n'
                end
            else
                tbl_str[#tbl_str + 1] = 'unable to serialize value type ' .. SR.basicSerialize(type(val)) .. ' at index ' .. tostring(ind)
            end
        end

        tbl_str[#tbl_str + 1] = indent .. '}'
        return table.concat(tbl_str)
    end
end

SR.log("Loaded SimpleRadio Standalone Export version: 1.9.7.0")