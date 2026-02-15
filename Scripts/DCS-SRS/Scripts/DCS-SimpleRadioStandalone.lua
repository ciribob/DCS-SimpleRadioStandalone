-- Version 2.3.4.0

-- Special thanks to Cap. Zeen, Tarres and Splash for all the help
-- with getting the radio information :)
-- Run the installer to correctly install this file
local SR = {}

-- Known radio presets (think make and model).
SR.RadioModels = {
    Intercom = "intercom",

    -- WW2
    AN_ARC5 = "arc5",
    FUG_16_ZY = "fug16zy",
    R1155 = "r1155",
    SCR522A = "scr522a",
    T1154 = "t1154",

    -- Western
    AN_ARC27 = "arc27",
    AN_ARC51 = "arc51",
    AN_ARC51BX = "arc51",
    AN_ARC131 = "arc131",
    AN_ARC134 = "arc134",
    AN_ARC164 = "arc164",
    AN_ARC182 = "arc186",
    AN_ARC186 = "arc186",
    AN_ARC201D = "arc201d",
    AN_ARC210 = "arc210",
    AN_ARC220 = "arc220",
    AN_ARC222 = "arc222",
    LINK16 = "link16",
    

    -- Eastern
    Baklan_5 = "baklan5",
    JADRO_1A = "jadro1a",
    R_800 = "r800",
    R_828 = "r828",
    R_832M = "r832m",
    R_852 = "r852",
    R_862 = "r862",
    R_863 = "r863",
    R_864 = "r864",
    RSI_6K = "rsi6k",
}

SR.SEAT_INFO_PORT = 9087
SR.LOS_RECEIVE_PORT = 9086
SR.LOS_SEND_TO_PORT = 9085
SR.RADIO_SEND_TO_PORT = 9084


SR.LOS_HEIGHT_OFFSET = 20.0 -- sets the line of sight offset to simulate radio waves bending
SR.LOS_HEIGHT_OFFSET_MAX = 200.0 -- max amount of "bend"
SR.LOS_HEIGHT_OFFSET_STEP = 20.0 -- Interval to "bend" in

SR.unicast = true --DONT CHANGE THIS

SR.lastKnownPos = { x = 0, y = 0, z = 0 }
SR.lastKnownSeat = 0
SR.lastKnownSlot = ''

SR.LastKnownUnitId = "" -- used for a10c volume
SR.LastKnownUnitType = ""    -- used for F/A-18C ENT button

SR.MIDS_FREQ = 1030.0 * 1000000 -- Start at UHF 300
SR.MIDS_FREQ_SEPARATION = 1.0 * 100000 -- 0.1 MHZ between MIDS channels

function SR.log(str)
    log.write('SRS-export', log.INFO, str)
end

function SR.error(str)
    log.write('SRS-export', log.ERROR, str)
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
SR.UDPSeatReceiveSocket = socket.udp()

--bind for listening for LOS info
SR.UDPLosReceiveSocket:setsockname("*", SR.LOS_RECEIVE_PORT)
SR.UDPLosReceiveSocket:settimeout(0) --receive timer was 0001

SR.UDPSeatReceiveSocket:setsockname("*", SR.SEAT_INFO_PORT)
SR.UDPSeatReceiveSocket:settimeout(0) 

local terrain = require('terrain')

if terrain ~= nil then
    SR.log("Loaded Terrain - SimpleRadio Standalone!")
end

-- Prev Export functions.
local _prevLuaExportActivityNextEvent = LuaExportActivityNextEvent
local _prevLuaExportBeforeNextFrame = LuaExportBeforeNextFrame

local _tNextSRS = 0

SR.exporters = {}   -- exporter table. Initialized at the end

SR.fc3 = {}
SR.fc3["A-10A"] = true
SR.fc3["F-15C"] = true
SR.fc3["MiG-29A"] = true
SR.fc3["MiG-29S"] = true
SR.fc3["MiG-29G"] = true
SR.fc3["Su-27"] = true
SR.fc3["J-11A"] = true
SR.fc3["Su-33"] = true
SR.fc3["Su-25"] = true
SR.fc3["Su-25T"] = true

--[[ Reading special options.
   option: dot separated 'path' to your option under the plugins field,
   ie 'DCS-SRS.srsAutoLaunchEnabled', or 'SA342.HOT_MIC'
--]]
SR.specialOptions = {}
function SR.getSpecialOption(option)
    if not SR.specialOptions[option] then
        local options = require('optionsEditor')
        -- If the option doesn't exist, a nil value is returned.
        -- Memoize into a subtable to avoid entering that code again,
        -- since options.getOption ends up doing a disk access.
        SR.specialOptions[option] = { value = options.getOption('plugins.'..option) }
    end
    
    return SR.specialOptions[option].value
end

-- Function to load mods' SRS plugin script
function SR.LoadModsPlugins()
    -- Load SRS Maintained Modules
    local SRSModulesPath = lfs.writedir() .. [[Mods\Services\DCS-SRS\Scripts\DCS-SRS-Modules]]
    for moduleFile in lfs.dir(SRSModulesPath) do
        SR.LoadModule(SRSModulesPath .. [[\]] .. moduleFile)
    end

    -- Check the 3 main Mods sub-folders
    local aircraftModsPath = lfs.writedir() .. [[Mods\Aircraft]]
    SR.ModsPuginsRecursiveSearch(aircraftModsPath)

    local TechModsPath = lfs.writedir() .. [[Mods\Tech]]
    SR.ModsPuginsRecursiveSearch(TechModsPath)

    -- local ServicesModsPath = lfs.writedir() .. [[Mods\Services]]
    -- SR.ModsPuginsRecursiveSearch(ServicesModsPath)
end

-- Performs a search of subfolders for SRS/autoload.lua
-- compainion function to SR.LoadModsPlugins()
function SR.ModsPuginsRecursiveSearch(modsPath)
    local mode, errmsg
    mode, errmsg = lfs.attributes (modsPath, "mode")
   
    -- Check that Mod folder actually exists, if not then do nothing
    if mode == nil or mode ~= "directory" then
        SR.error("SR.RecursiveSearch(): modsPath is not a directory or is null: '" .. modsPath)
        return
    end

    SR.log("Searching for mods in '" .. modsPath)
    
    -- Process each available Mod
    for modFolder in lfs.dir(modsPath) do
        modAutoloadPath = modsPath .. [[\]] .. modFolder .. [[\SRS\autoload.lua]]

        -- If the Mod declares an SRS autoload file we process it
        SR.LoadModule(modAutoloadPath)
    end
end

function SR.LoadModule(modulePath)
    local mode, errmsg
    mode, errmsg = lfs.attributes(modulePath, "mode")

    if mode ~= nil and mode == "file" then
        -- Try to load the Mod's script through a protected environment to avoid to invalidate SRS entirely if the script contains any error
        local status, error = pcall(function()
            loadfile(modulePath)().register(SR)
        end)

        if error then
            SR.error("Failed loading SRS Mod plugin due to an error in '" .. modulePath .. "'")
        else
            SR.log("Loaded SRS Mod plugin '" .. modulePath .. "'")
        end
    end
end

function SR.shouldUseUnitDetails(_slot)

    if  _slot == "Tactical-Commander" or _slot == "Game-Master" or _slot == "JTAC-Operator" or _slot == "Observer"  then
        return false
    end
    
    return true
end

function SR.exporter()

    local _slot = ''

    if SR.lastKnownSlot == nil or SR.lastKnownSlot == '' then
        _slot = 'Spectator'
    else
        if string.find(SR.lastKnownSlot, 'artillery_commander') then
            _slot = "Tactical-Commander"
        elseif string.find(SR.lastKnownSlot, 'instructor') then
            _slot = "Game-Master"
        elseif string.find(SR.lastKnownSlot, 'forward_observer') then
            _slot = "JTAC-Operator" -- "JTAC"
        elseif string.find(SR.lastKnownSlot, 'observer') then
            _slot = "Observer"
        else
            _slot = SR.lastKnownSlot
        end
    end
    
    local _update
    local _data = LoGetSelfData()

    -- REMOVE
   -- SR.log(SR.debugDump(_data).."\n\n")

    if _data ~= nil and not SR.fc3[_data.Name] then
        -- check for death / eject -- call below returns a number when ejected - ignore FC3
        local _device = GetDevice(0)

        if _device == nil or type(_device) == 'number' then
            _data = nil -- wipe out data - aircraft is gone really
        end
    end

    if _data ~= nil and SR.shouldUseUnitDetails(_slot) then

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
                { name = "", freq = 100, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2, model = SR.RadioModels.Intercom },
                { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 }, -- enc means encrypted
                { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
                { name = "", freq = 0, modulation = 3, volume = 1.0, secFreq = 0, freqMin = 1, freqMax = 1, encKey = 0, enc = false, encMode = 0, freqMode = 0, guardFreqMode = 0, volMode = 0, expansion = false, rtMode = 2 },
            },
            control = 0, -- HOTAS
        }
        _update.ambient = {vol = 0.0, abType = '' }
        _update.name = _data.UnitName
        _update.unit = _data.Name
        _update.unitId = LoGetPlayerPlaneId()

        local _latLng,_point = SR.exportPlayerLocation(_data)

        _update.latLng = _latLng
        SR.lastKnownPos = _point

        -- IFF_STATUS:  OFF = 0,  NORMAL = 1 , or IDENT = 2 (IDENT means Blink on LotATC)
        -- M1:-1 = off, any other number on
        -- M2: -1 = OFF, any other number on
        -- M3: -1 = OFF, any other number on
        -- M4: 1 = ON or 0 = OFF
        -- EXPANSION: only enabled if IFF Expansion is enabled
        -- CONTROL: 1 - OVERLAY / SRS, 0 - COCKPIT / Realistic, 2 = DISABLED / NOT FITTED AT ALL
        -- MIC - -1 for OFF or ID of the radio to trigger IDENT Mode if the PTT is used
        -- IFF STATUS{"control":1,"expansion":false,"mode1":51,"mode3":7700,"mode4":true,"status":2,mic=1}

        _update.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=1,expansion=false,mic=-1}

        --   SR.log(_update.unit.."\n\n")

        local aircraftExporter = SR.exporters[_update.unit]

        if aircraftExporter then

          -- show_param_handles_list()
          --  list_cockpit_params()
          --  SR.log(SR.debugDump(getmetatable(GetDevice(1))).."\n\n")

            _update = aircraftExporter(_update, SR)
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

            _update.radios[5].name = "FC3 HF"
            _update.radios[5].freq = 3.0 * 1000000
            _update.radios[5].modulation = 0
            _update.radios[5].volume = 1.0
            _update.radios[5].freqMin = 1 * 1000000
            _update.radios[5].freqMax = 15 * 1000000
            _update.radios[5].volMode = 1
            _update.radios[5].freqMode = 1
            _update.radios[5].encKey = 1
            _update.radios[5].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
            _update.radios[5].rtMode = 1

            _update.control = 0;
            _update.selected = 1
            _update.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false,mic=-1}

            _update.ambient = {vol = 0.2, abType = 'jet' }
        end

        SR.LastKnownUnitId = _update.unitId
        SR.LastKnownUnitType = _data.Name
    else
        -- There may be a unit but we're purposely ignoring it if you're in a special slot
        
        --Ground Commander or spectator
        _update = {
            name = "Unknown",
            ambient = {vol = 0.0, abType = ''},
            unit = _slot,
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
            iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false,mic=-1}
        }

        -- Allows for custom radio's using the DCS-Plugin scheme.
        local aircraftExporter = SR.exporters[_update.unit]
        if aircraftExporter then
            _update = aircraftExporter(_update, SR)
        end

        -- Use vehicle position if we have one so when you join a vehicle you get LOS etc
        if _data ~= nil then
            local _latLng,_point = SR.exportPlayerLocation(_data)

            _update.latLng = _latLng
            SR.lastKnownPos = _point
        end

        SR.LastKnownUnitId = ""
        SR.LastKnownUnitType = ""
    end

    _update.seat = SR.lastKnownSeat

    if SR.unicast then
        socket.try(SR.UDPSendSocket:sendto(SR.JSON:encode(_update) .. " \n", "127.0.0.1", SR.RADIO_SEND_TO_PORT))
    else
        socket.try(SR.UDPSendSocket:sendto(SR.JSON:encode(_update) .. " \n", "127.255.255.255", SR.RADIO_SEND_TO_PORT))
    end
end


function SR.readLOSSocket()
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
end

function SR.readSeatSocket()
    -- Receive buffer is 8192 in LUA Socket
    local _received = SR.UDPSeatReceiveSocket:receive()

    if _received then
        local _decoded = SR.JSON:decode(_received)

        if _decoded then
            SR.lastKnownSeat = _decoded.seat
            SR.lastKnownSlot = _decoded.slot
            --SR.log("lastKnownSeat "..SR.lastKnownSeat)
        end

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

function SR.getNonStandardSpinner(_deviceId, _range, _step, _round)
    local _value = GetDevice(0):get_argument_value(_deviceId)
    -- round to x decimal places
    _value = SR.advRound(_value,_round)

    -- round to nearest step
    -- then round again to X decimal places
    _value = SR.advRound(SR.round(_value, _step),_round)

    --round to the step of the values
    local _res = _range[_value]

    if not _res then
        return 0
    end

    return _res

end

function SR.getAmbientVolumeEngine()

    local _res = 0
    
    pcall(function()
    
        local engine = LoGetEngineInfo()

        --{"EngineStart":{"left":0,"right":0},"FuelConsumption":{"left":1797.9623832703,"right":1795.5901498795},"HydraulicPressure":{"left":0,"right":0},"RPM":{"left":97.268943786621,"right":97.269966125488},"Temperature":{"left":746.81764087677,"right":745.09023532867},"fuel_external":0,"fuel_internal":0.99688786268234}
        --SR.log(JSON:encode(engine))
        if engine.RPM and engine.RPM.left > 1 then
            _res = engine.RPM.left 
        end

        if engine.RPM and engine.RPM.right > 1 then
            _res = engine.RPM.right
        end
    end )

    return SR.round(_res,1)
end


function SR.getRadioFrequency(_deviceId, _roundTo, _ignoreIsOn)
    local _device = GetDevice(_deviceId)

    if not _roundTo then
        _roundTo = 5000
    end

    if _device then
        if _device:is_on() or _ignoreIsOn then
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


function SR.advRound(number, decimals, method)
    if string.find(number, "%p" ) ~= nil then
        decimals = decimals or 0
        local lFactor = 10 ^ decimals
        if (method == "ceil" or method == "floor") then
            -- ceil: Returns the smallest integer larger than or equal to number
            -- floor: Returns the smallest integer smaller than or equal to number
            return math[method](number * lFactor) / lFactor
        else
            return tonumber(("%."..decimals.."f"):format(number))
        end
    else
        return number
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

function SR.debugDump(o)
    if o == nil then
        return "~nil~"
    elseif type(o) == 'table' then
        local s = '{ '
        for k,v in pairs(o) do
                if type(k) ~= 'number' then k = '"'..k..'"' end
                s = s .. '['..k..'] = ' .. SR.debugDump(v) .. ','
        end
        return s .. '} '
    else
        return tostring(o)
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

--- DCS EXPORT FUNCTIONS
LuaExportActivityNextEvent = function(tCurrent)
    -- we only want to send once every 0.2 seconds
    -- but helios (and other exports) require data to come much faster
    if _tNextSRS - tCurrent < 0.01 then   -- has to be written this way as the function is being called with a loss of precision at times
        _tNextSRS = tCurrent + 0.2

        local _status, _result = pcall(SR.exporter)

        if not _status then
            SR.error(SR.debugDump(_result))
        end
    end

    local tNext = _tNextSRS

    -- call previous
    if _prevLuaExportActivityNextEvent then
        local _status, _result = pcall(_prevLuaExportActivityNextEvent, tCurrent)
        if _status then
            -- Use lower of our tNext (0.2s) or the previous export's
            if _result and _result < tNext and _result > tCurrent then
                tNext = _result
            end
        else
            SR.error('Calling other LuaExportActivityNextEvent from another script: ' .. SR.debugDump(_result))
        end
    end

    if terrain == nil then
        SR.error("Terrain Export is not working")
        --SR.log("EXPORT CHECK "..tostring(terrain.isVisible(1,100,1,1,100,1)))
        --SR.log("EXPORT CHECK "..tostring(terrain.isVisible(1,1,1,1,-100,-100)))
    end

     --SR.log(SR.tableShow(_G).."\n\n")

    return tNext
end


LuaExportBeforeNextFrame = function()

    -- read from socket
    local _status, _result = pcall(SR.readLOSSocket)

    if not _status then
        SR.error('LuaExportBeforeNextFrame readLOSSocket SRS: ' .. SR.debugDump(_result))
    end

    _status, _result = pcall(SR.readSeatSocket)

    if not _status then
        SR.error('LuaExportBeforeNextFrame readSeatSocket SRS: ' .. SR.debugDump(_result))
    end

    -- call original
    if _prevLuaExportBeforeNextFrame then
        _status, _result = pcall(_prevLuaExportBeforeNextFrame)
        if not _status then
            SR.error('Calling other LuaExportBeforeNextFrame from another script: ' .. SR.debugDump(_result))
        end
    end
end

-- Load mods' SRS plugins
SR.LoadModsPlugins()

SR.log("Loaded SimpleRadio Standalone Export version: 2.3.4.0")
