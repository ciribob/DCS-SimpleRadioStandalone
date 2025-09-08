-- Version 2.3.0.3

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
SR.lastKnownSlot = ""

SR.MIDS_FREQ = 1030.0 * 1000000 -- Start at UHF 300
SR.MIDS_FREQ_SEPARATION = 1.0 * 100000 -- 0.1 MHZ between MIDS channels

function SR.log(str)
    log.write("SRS-export", log.INFO, str)
end

function SR.error(str)
    log.write("SRS-export", log.ERROR, str)
end

package.path = package.path .. ";.\\LuaSocket\\?.lua;"
package.cpath = package.cpath .. ";.\\LuaSocket\\?.dll;"

---- DCS Search Paths - So we can load Terrain!
local guiBindPath = "./dxgui/bind/?.lua;"
    .. "./dxgui/loader/?.lua;"
    .. "./dxgui/skins/skinME/?.lua;"
    .. "./dxgui/skins/common/?.lua;"

package.path = package.path
    .. ";"
    .. guiBindPath
    .. "./MissionEditor/?.lua;"
    .. "./MissionEditor/themes/main/?.lua;"
    .. "./MissionEditor/modules/?.lua;"
    .. "./Scripts/?.lua;"
    .. "./LuaSocket/?.lua;"
    .. "./Scripts/UI/?.lua;"
    .. "./Scripts/UI/Multiplayer/?.lua;"
    .. "./Scripts/DemoScenes/?.lua;"

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

local terrain = require("terrain")

if terrain ~= nil then
    SR.log("Loaded Terrain - SimpleRadio Standalone!")
end

-- Prev Export functions.
local _prevLuaExportActivityNextEvent = LuaExportActivityNextEvent
local _prevLuaExportBeforeNextFrame = LuaExportBeforeNextFrame

local _lastUnitId = "" -- used for a10c volume
local _lastUnitType = "" -- used for F/A-18C ENT button
local _tNextSRS = 0

SR.exporters = {} -- exporter table. Initialized at the end

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
        local options = require("optionsEditor")
        -- If the option doesn't exist, a nil value is returned.
        -- Memoize into a subtable to avoid entering that code again,
        -- since options.getOption ends up doing a disk access.
        SR.specialOptions[option] = { value = options.getOption("plugins." .. option) }
    end

    return SR.specialOptions[option].value
end

-- Function to load mods' SRS plugin script
function SR.LoadModsPlugins()
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
    mode, errmsg = lfs.attributes(modsPath, "mode")

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
        mode, errmsg = lfs.attributes(modAutoloadPath, "mode")
        if mode ~= nil and mode == "file" then
            -- Try to load the Mod's script through a protected environment to avoid to invalidate SRS entirely if the script contains any error
            local status, error = pcall(function()
                loadfile(modAutoloadPath)().register(SR)
            end)

            if error then
                SR.error("Failed loading SRS Mod plugin due to an error in '" .. modAutoloadPath .. "'")
            else
                SR.log("Loaded SRS Mod plugin '" .. modAutoloadPath .. "'")
            end
        end
    end
end

function SR.shouldUseUnitDetails(_slot)
    if _slot == "Tactical-Commander" or _slot == "Game-Master" or _slot == "JTAC-Operator" or _slot == "Observer" then
        return false
    end

    return true
end

function SR.exporter()
    local _slot = ""

    if SR.lastKnownSlot == nil or SR.lastKnownSlot == "" then
        _slot = "Spectator"
    else
        if string.find(SR.lastKnownSlot, "artillery_commander") then
            _slot = "Tactical-Commander"
        elseif string.find(SR.lastKnownSlot, "instructor") then
            _slot = "Game-Master"
        elseif string.find(SR.lastKnownSlot, "forward_observer") then
            _slot = "JTAC-Operator" -- "JTAC"
        elseif string.find(SR.lastKnownSlot, "observer") then
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

        if type(_device) == "number" then
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
                {
                    name = "",
                    freq = 100,
                    modulation = 3,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 1,
                    freqMax = 1,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    guardFreqMode = 0,
                    volMode = 0,
                    expansion = false,
                    rtMode = 2,
                    model = SR.RadioModels.Intercom,
                },
                {
                    name = "",
                    freq = 0,
                    modulation = 3,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 1,
                    freqMax = 1,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    guardFreqMode = 0,
                    volMode = 0,
                    expansion = false,
                    rtMode = 2,
                }, -- enc means encrypted
                {
                    name = "",
                    freq = 0,
                    modulation = 3,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 1,
                    freqMax = 1,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    guardFreqMode = 0,
                    volMode = 0,
                    expansion = false,
                    rtMode = 2,
                },
                {
                    name = "",
                    freq = 0,
                    modulation = 3,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 1,
                    freqMax = 1,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    guardFreqMode = 0,
                    volMode = 0,
                    expansion = false,
                    rtMode = 2,
                },
                {
                    name = "",
                    freq = 0,
                    modulation = 3,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 1,
                    freqMax = 1,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    guardFreqMode = 0,
                    volMode = 0,
                    expansion = false,
                    rtMode = 2,
                },
                {
                    name = "",
                    freq = 0,
                    modulation = 3,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 1,
                    freqMax = 1,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    guardFreqMode = 0,
                    volMode = 0,
                    expansion = false,
                    rtMode = 2,
                },
            },
            control = 0, -- HOTAS
        }
        _update.ambient = { vol = 0.0, abType = "" }
        _update.name = _data.UnitName
        _update.unit = _data.Name
        _update.unitId = LoGetPlayerPlaneId()

        local _latLng, _point = SR.exportPlayerLocation(_data)

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

        _update.iff =
            { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 1, expansion = false, mic = -1 }

        --   SR.log(_update.unit.."\n\n")

        local aircraftExporter = SR.exporters[_update.unit]

        if aircraftExporter then
            -- show_param_handles_list()
            --  list_cockpit_params()
            --  SR.log(SR.debugDump(getmetatable(GetDevice(1))).."\n\n")

            _update = aircraftExporter(_update)
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

            _update.control = 0
            _update.selected = 1
            _update.iff =
                { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false, mic = -1 }

            _update.ambient = { vol = 0.2, abType = "jet" }
        end

        _lastUnitId = _update.unitId
        _lastUnitType = _data.Name
    else
        -- There may be a unit but we're purposely ignoring it if you're in a special slot

        --Ground Commander or spectator
        _update = {
            name = "Unknown",
            ambient = { vol = 0.0, abType = "" },
            unit = _slot,
            selected = 1,
            ptt = false,
            capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" },
            simultaneousTransmissionControl = 1,
            latLng = { lat = 0, lng = 0, alt = 0 },
            unitId = 100000001, -- pass through starting unit id here
            radios = {
                --- Radio 0 is always intercom now -- disabled if AWACS panel isnt open
                {
                    name = "SATCOM",
                    freq = 100,
                    modulation = 2,
                    volume = 1.0,
                    secFreq = 0,
                    freqMin = 100,
                    freqMax = 100,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 0,
                    volMode = 1,
                    expansion = false,
                    rtMode = 2,
                },
                {
                    name = "UHF Guard",
                    freq = 251.0 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 243.0 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 1,
                    enc = false,
                    encMode = 1,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "UHF Guard",
                    freq = 251.0 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 243.0 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 1,
                    enc = false,
                    encMode = 1,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "VHF FM",
                    freq = 30.0 * 1000000,
                    modulation = 1,
                    volume = 1.0,
                    secFreq = 1,
                    freqMin = 1 * 1000000,
                    freqMax = 76 * 1000000,
                    encKey = 1,
                    enc = false,
                    encMode = 1,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "UHF Guard",
                    freq = 251.0 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 243.0 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 1,
                    enc = false,
                    encMode = 1,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "UHF Guard",
                    freq = 251.0 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 243.0 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 1,
                    enc = false,
                    encMode = 1,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "VHF Guard",
                    freq = 124.8 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 121.5 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "VHF Guard",
                    freq = 124.8 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 121.5 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "VHF FM",
                    freq = 30.0 * 1000000,
                    modulation = 1,
                    volume = 1.0,
                    secFreq = 1,
                    freqMin = 1 * 1000000,
                    freqMax = 76 * 1000000,
                    encKey = 1,
                    enc = false,
                    encMode = 1,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "VHF Guard",
                    freq = 124.8 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 121.5 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
                {
                    name = "VHF Guard",
                    freq = 124.8 * 1000000,
                    modulation = 0,
                    volume = 1.0,
                    secFreq = 121.5 * 1000000,
                    freqMin = 1 * 1000000,
                    freqMax = 400 * 1000000,
                    encKey = 0,
                    enc = false,
                    encMode = 0,
                    freqMode = 1,
                    volMode = 1,
                    expansion = false,
                    rtMode = 1,
                },
            },
            radioType = 3,
            iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false, mic = -1 },
        }

        -- Allows for custom radio's using the DCS-Plugin scheme.
        local aircraftExporter = SR.exporters[_update.unit]
        if aircraftExporter then
            _update = aircraftExporter(_update)
        end

        -- Use vehicle position if we have one so when you join a vehicle you get LOS etc
        if _data ~= nil then
            local _latLng, _point = SR.exportPlayerLocation(_data)

            _update.latLng = _latLng
            SR.lastKnownPos = _point
        end

        _lastUnitId = ""
        _lastUnitType = ""
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
                socket.try(
                    SR.UDPSendSocket:sendto(SR.JSON:encode(_losList) .. " \n", "127.255.255.255", SR.LOS_SEND_TO_PORT)
                )
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
        local _point = LoGeoCoordinatesToLoCoordinates(_client.lng, _client.lat)
        -- Encoded Point: {"x":3758906.25,"y":0,"z":-1845112.125}

        local _los = 1.0 -- 1.0 is NO line of sight as in full signal loss - 0.0 is full signal, NO Loss

        local _hasLos = terrain.isVisible(
            SR.lastKnownPos.x,
            SR.lastKnownPos.y + SR.LOS_HEIGHT_OFFSET,
            SR.lastKnownPos.z,
            _point.x,
            _client.alt + SR.LOS_HEIGHT_OFFSET,
            _point.z
        )

        if _hasLos then
            table.insert(_result, { id = _client.id, los = 0.0 })
        else
            -- find the lowest offset that would provide line of sight
            for _losOffset = SR.LOS_HEIGHT_OFFSET + SR.LOS_HEIGHT_OFFSET_STEP, SR.LOS_HEIGHT_OFFSET_MAX - SR.LOS_HEIGHT_OFFSET_STEP, SR.LOS_HEIGHT_OFFSET_STEP do
                _hasLos = terrain.isVisible(
                    SR.lastKnownPos.x,
                    SR.lastKnownPos.y + _losOffset,
                    SR.lastKnownPos.z,
                    _point.x,
                    _client.alt + SR.LOS_HEIGHT_OFFSET,
                    _point.z
                )

                if _hasLos then
                    -- compute attenuation as a percentage of LOS_HEIGHT_OFFSET_MAX
                    -- e.g.:
                    --    LOS_HEIGHT_OFFSET_MAX = 500   -- max offset
                    --    _losOffset = 200              -- offset actually used
                    --    -> attenuation would be 200 / 500 = 0.4
                    table.insert(_result, { id = _client.id, los = (_losOffset / SR.LOS_HEIGHT_OFFSET_MAX) })
                    break
                end
            end

            -- if there is still no LOS
            if not _hasLos then
                -- then check max offset gives LOS
                _hasLos = terrain.isVisible(
                    SR.lastKnownPos.x,
                    SR.lastKnownPos.y + SR.LOS_HEIGHT_OFFSET_MAX,
                    SR.lastKnownPos.z,
                    _point.x,
                    _client.alt + SR.LOS_HEIGHT_OFFSET,
                    _point.z
                )

                if _hasLos then
                    -- but make sure that we do not get 1.0 attenuation when using LOS_HEIGHT_OFFSET_MAX
                    -- (LOS_HEIGHT_OFFSET_MAX / LOS_HEIGHT_OFFSET_MAX would give attenuation of 1.0)
                    -- I'm using 0.99 as a placeholder, not sure what would work here
                    table.insert(_result, { id = _client.id, los = 0.99 })
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
        local latLng = LoLoCoordinatesToGeoCoordinates(_data.Position.x, _data.Position.z)
        --LatLng: {"latitude":25.594814853729,"longitude":55.938746498011}

        return { lat = latLng.latitude, lng = latLng.longitude, alt = _data.Position.y }, _data.Position
    else
        return { lat = 0, lng = 0, alt = 0 }, { x = 0, y = 0, z = 0 }
    end
end

function SR.exportCameraLocation()
    local _cameraPosition = LoGetCameraPosition()

    if _cameraPosition ~= nil and _cameraPosition.p ~= nil then
        local latLng = LoLoCoordinatesToGeoCoordinates(_cameraPosition.p.x, _cameraPosition.p.z)

        return { lat = latLng.latitude, lng = latLng.longitude, alt = _cameraPosition.p.y }, _cameraPosition.p
    end

    return { lat = 0, lng = 0, alt = 0 }, { x = 0, y = 0, z = 0 }
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
    _data.radios[2].model = SR.RadioModels.AN_ARC186

    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = 251.0 * 1000000 --225-399.975 MHZ
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = 243.0 * 1000000
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 225 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.radios[4].name = "AN/ARC-186(V) FM"
    _data.radios[4].freq = 30.0 * 1000000 --VHF/FM opera entre 30.000 y 76.000 MHz.
    _data.radios[4].modulation = 1
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30 * 1000000
    _data.radios[4].freqMax = 76 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        --  local _door = SR.getButtonPosition(181)

        --  if _door > 0.15 then
        _data.ambient = { vol = 0.3, abType = "a10" }
    -- else
    --     _data.ambient = {vol = 0.2,  abType = 'a10' }
    --  end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "a10" }
    end

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
    _data.radios[2].model = SR.RadioModels.R_862

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        --  local _door = SR.getButtonPosition(181)

        --   if _door > 0.15 then
        _data.ambient = { vol = 0.3, abType = "mig29" }
    --   else
    --      _data.ambient = {vol = 0.2,  abType = 'mig29' }
    --    end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mig29" }
    end

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
    _data.radios[2].model = SR.RadioModels.R_862

    _data.radios[3].name = "R-828"
    _data.radios[3].freq = 30.0 * 1000000 --20 - 60 MHz.
    _data.radios[3].modulation = 1
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 20 * 1000000
    _data.radios[3].freqMax = 59.975 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].model = SR.RadioModels.R_828

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        --   local _door = SR.getButtonPosition(181)

        --    if _door > 0.15 then
        _data.ambient = { vol = 0.3, abType = "su25" }
    --   else
    --      _data.ambient = {vol = 0.2,  abType = 'su25' }
    --    end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "su25" }
    end

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
    _data.radios[2].model = SR.RadioModels.R_800

    _data.radios[3].name = "R-864"
    _data.radios[3].freq = 3.5 * 1000000 --HF frequencies in the 3-10Mhz, like the Jadro
    _data.radios[3].modulation = 0
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 3 * 1000000
    _data.radios[3].freqMax = 10 * 1000000
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].model = SR.RadioModels.R_864

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        --  local _door = SR.getButtonPosition(181)

        --  if _door > 0.2 then
        _data.ambient = { vol = 0.3, abType = "su27" }
    --  else
    --      _data.ambient = {vol = 0.2,  abType = 'su27' }
    --   end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "su27" }
    end

    return _data
end

local _ah64Mode1Persist = -1 -- Need this persistence for only MODE1 because it's pulled from the XPNDR page; default it to off
function SR.exportRadioAH64D(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "Recommended: Always Allow SRS Hotkeys - OFF. Bind Intercom Select & PTT, Radio PTT and DCS RTS up down",
    }
    _data.control = 1

    local _iffSettings = {
        status = 0,
        mode1 = _ah64Mode1Persist,
        mode2 = -1,
        mode3 = -1,
        mode4 = false,
        control = 0,
        expansion = false,
    }

    -- Check if player is in a new aircraft
    if _lastUnitId ~= _data.unitId then
        -- New aircraft; SENS volume is at 0
        pcall(function()
            -- source https://github.com/DCSFlightpanels/dcs-bios/blob/master/Scripts/DCS-BIOS/lib/AH-64D.lua
            GetDevice(63):performClickableAction(3011, 1) -- Pilot Master
            GetDevice(63):performClickableAction(3012, 1) -- Pilot SENS

            GetDevice(62):performClickableAction(3011, 1) -- CoPilot Master
            GetDevice(62):performClickableAction(3012, 1) -- CoPilot SENS
        end)
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-186 VHF"
    _data.radios[2].freq = SR.getRadioFrequency(58)
    _data.radios[2].modulation = SR.getRadioModulation(58)
    _data.radios[2].volMode = 0
    _data.radios[2].model = SR.RadioModels.AN_ARC186

    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(57)
    _data.radios[3].modulation = SR.getRadioModulation(57)
    _data.radios[3].volMode = 0
    _data.radios[3].encMode = 2
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    _data.radios[4].name = "AN/ARC201D FM1"
    _data.radios[4].freq = SR.getRadioFrequency(59)
    _data.radios[4].modulation = SR.getRadioModulation(59)
    _data.radios[4].volMode = 0
    _data.radios[4].encMode = 2
    _data.radios[4].model = SR.RadioModels.AN_ARC201D

    _data.radios[5].name = "AN/ARC-201D FM2"
    _data.radios[5].freq = SR.getRadioFrequency(60)
    _data.radios[5].modulation = SR.getRadioModulation(60)
    _data.radios[5].volMode = 0
    _data.radios[5].encMode = 2
    _data.radios[5].model = SR.RadioModels.AN_ARC201D

    _data.radios[6].name = "AN/ARC-220 HF"
    _data.radios[6].freq = SR.getRadioFrequency(61)
    _data.radios[6].modulation = 0
    _data.radios[6].volMode = 0
    _data.radios[6].encMode = 2 -- As of DCS ver 2.9.4.53627 the HF preset functionality is bugged, but I'll leave this here in hopes ED fixes the bug
    _data.radios[6].model = SR.RadioModels.AN_ARC220

    local _seat = get_param_handle("SEAT"):get() -- PLT/CPG ?
    local _eufdDevice = nil
    local _mpdLeft = nil
    local _mpdRight = nil
    local _iffIdentBtn = nil
    local _iffEmergency = nil

    if _seat == 0 then
        _eufdDevice = SR.getListIndicatorValue(18)
        _mpdLeft = SR.getListIndicatorValue(7)
        _mpdRight = SR.getListIndicatorValue(9)
        _iffIdentBtn = SR.getButtonPosition(347) -- PLT comm panel ident button
        _iffEmergency = GetDevice(0):get_argument_value(404) -- PLT Emergency Panel XPNDR Indicator

        local _masterVolume = SR.getRadioVolume(0, 344, { 0.0, 1.0 }, false)

        --intercom
        _data.radios[1].volume = SR.getRadioVolume(0, 345, { 0.0, 1.0 }, false) * _masterVolume

        -- VHF
        if SR.getButtonPosition(449) == 0 then
            _data.radios[2].volume = SR.getRadioVolume(0, 334, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[2].volume = 0
        end

        -- UHF
        if SR.getButtonPosition(450) == 0 then
            _data.radios[3].volume = SR.getRadioVolume(0, 335, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[3].volume = 0
        end

        -- FM1
        if SR.getButtonPosition(451) == 0 then
            _data.radios[4].volume = SR.getRadioVolume(0, 336, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[4].volume = 0
        end

        -- FM2
        if SR.getButtonPosition(452) == 0 then
            _data.radios[5].volume = SR.getRadioVolume(0, 337, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[5].volume = 0
        end

        -- HF
        if SR.getButtonPosition(453) == 0 then
            _data.radios[6].volume = SR.getRadioVolume(0, 338, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[6].volume = 0
        end

        if SR.getButtonPosition(346) ~= 1 then
            _data.intercomHotMic = true
        end
    else
        _eufdDevice = SR.getListIndicatorValue(19)
        _mpdLeft = SR.getListIndicatorValue(11)
        _mpdRight = SR.getListIndicatorValue(13)
        _iffIdentBtn = SR.getButtonPosition(388) -- CPG comm panel ident button
        _iffEmergency = GetDevice(0):get_argument_value(428) -- CPG Emergency Panel XPNDR Indicator

        local _masterVolume = SR.getRadioVolume(0, 385, { 0.0, 1.0 }, false)

        --intercom
        _data.radios[1].volume = SR.getRadioVolume(0, 386, { 0.0, 1.0 }, false) * _masterVolume

        -- VHF
        if SR.getButtonPosition(459) == 0 then
            _data.radios[2].volume = SR.getRadioVolume(0, 375, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[2].volume = 0
        end

        -- UHF
        if SR.getButtonPosition(460) == 0 then
            _data.radios[3].volume = SR.getRadioVolume(0, 376, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[3].volume = 0
        end

        -- FM1
        if SR.getButtonPosition(461) == 0 then
            _data.radios[4].volume = SR.getRadioVolume(0, 377, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[4].volume = 0
        end

        -- FM2
        if SR.getButtonPosition(462) == 0 then
            _data.radios[5].volume = SR.getRadioVolume(0, 378, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[5].volume = 0
        end

        -- HF
        if SR.getButtonPosition(463) == 0 then
            _data.radios[6].volume = SR.getRadioVolume(0, 379, { 0.0, 1.0 }, false) * _masterVolume
        else
            _data.radios[6].volume = 0
        end

        if SR.getButtonPosition(387) ~= 1 then
            _data.intercomHotMic = true
        end
    end

    if _eufdDevice then
        -- figure out selected
        if _eufdDevice["Rts_VHF_"] == "<" then
            _data.selected = 1
        elseif _eufdDevice["Rts_UHF_"] == "<" then
            _data.selected = 2
        elseif _eufdDevice["Rts_FM1_"] == "<" then
            _data.selected = 3
        elseif _eufdDevice["Rts_FM2_"] == "<" then
            _data.selected = 4
        elseif _eufdDevice["Rts_HF_"] == "<" then
            _data.selected = 5
        end

        if _eufdDevice["Guard"] == "G" then
            _data.radios[3].secFreq = 243e6
        end

        -- TODO??: Regarding IFF, I had not checked prior to @falcoger pointing it out... the Apache XPNDR
        --      page only runs a simple logic check (#digits) to accept input for modes. I considered
        --      scripting the logic on SRS' end to perform the check for validity, but without sufficient
        --      means to provide user feedback regarding the validity, I opted to just let the user input
        --      invalid codes since it seems (?) LotAtc doesn't perform a proper logic check either, just a
        --      #digit check. Hopefully ED will someday emplace the logic check within the Apache.

        if _eufdDevice["Transponder_MC"] == "NORM" then -- IFF NORM
            _iffSettings.status = (_iffIdentBtn > 0) and 2 or 1 -- IDENT and Power

            _iffSettings.mode3 = tonumber(_eufdDevice["Transponder_MODE_3A"]) or -1

            _iffSettings.mode4 = _eufdDevice["XPNDR_MODE_4"] ~= nil
        else -- Transponder_MC == "STBY" or there's no power
            _iffSettings.status = 0
        end

        if _iffEmergency == 1 then
            _iffSettings.mode3 = 7700
            _iffSettings.status = 1 -- XPNDR btn would actually turn on the XPNDR if it were in STBY (in real life)
        end

        _data.radios[3].enc = _eufdDevice["Cipher_UHF"] ~= nil
        _data.radios[3].encKey = tonumber(string.match(_eufdDevice["Cipher_UHF"] or "C1", "^C(%d+)"))

        _data.radios[4].enc = _eufdDevice["Cipher_FM1"] ~= nil
        _data.radios[4].encKey = tonumber(string.match(_eufdDevice["Cipher_FM1"] or "C1", "^C(%d+)"))

        _data.radios[5].enc = _eufdDevice["Cipher_FM2"] ~= nil
        _data.radios[5].encKey = tonumber(string.match(_eufdDevice["Cipher_FM2"] or "C1", "^C(%d+)"))

        _data.radios[6].enc = _eufdDevice["Cipher_HF"] ~= nil
        _data.radios[6].encKey = tonumber(string.match(_eufdDevice["Cipher_HF"] or "C1", "^C(%d+)"))
    end

    if _mpdLeft or _mpdRight then
        if _mpdLeft["Mode_S_Codes_Window_text_1"] then -- We're on the XPNDR page on the LEFT MPD
            _ah64Mode1Persist = _mpdLeft["PB24_9"] == "}1" and -1 or tonumber(string.format("%02d", _mpdLeft["PB7_23"]))
        end

        if _mpdRight["Mode_S_Codes_Window_text_1"] then -- We're on the XPNDR page on the RIGHT MPD
            _ah64Mode1Persist = _mpdRight["PB24_9"] == "}1" and -1
                or tonumber(string.format("%02d", _mpdRight["PB7_23"]))
        end
    end

    --CYCLIC_RTS_SW_LEFT 573 CPG 531 PLT
    local _pttButtonId = 573
    if _seat == 0 then
        _pttButtonId = 531
    end

    local _pilotPTT = SR.getButtonPosition(_pttButtonId)
    if _pilotPTT >= 0.5 then
        _data.intercomHotMic = false
        -- intercom
        _data.selected = 0
        _data.ptt = true
    elseif _pilotPTT <= -0.5 then
        _data.ptt = true
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(795)
        local _doorRight = SR.getButtonPosition(798)

        if _doorLeft > 0.3 or _doorRight > 0.3 then
            _data.ambient = { vol = 0.35, abType = "ah64" }
        else
            _data.ambient = { vol = 0.2, abType = "ah64" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ah64" }
    end

    for k, v in pairs(_iffSettings) do
        _data.iff[k] = v
    end -- IFF table overwrite

    return _data
end

function SR.exportRadioUH60L(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local isDCPower = SR.getButtonPosition(17) > 0 -- just using battery switch position for now, could tie into DC ESS BUS later?
    local intercomVolume = 0
    if isDCPower then
        -- ics master volume
        intercomVolume = GetDevice(0):get_argument_value(401)
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = intercomVolume
    _data.radios[1].volMode = 0
    _data.radios[1].freqMode = 0
    _data.radios[1].rtMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    -- Pilots' AN/ARC-201 FM
    local fm1Device = GetDevice(6)
    local fm1Power = GetDevice(0):get_argument_value(601) > 0.01
    local fm1Volume = 0
    local fm1Freq = 0
    local fm1Modulation = 1

    if fm1Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm1Volume = GetDevice(0):get_argument_value(604)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(403)
        fm1Freq = fm1Device:get_frequency()
        ARC201FM1Freq = get_param_handle("ARC201FM1param"):get()
        fm1Modulation = get_param_handle("ARC201_FM1_MODULATION"):get()
    end

    if not (fm1Power and isDCPower) then
        ARC201FM1Freq = 0
    end

    _data.radios[2].name = "AN/ARC-201 (1)"
    _data.radios[2].freq = ARC201FM1Freq --fm1Freq
    _data.radios[2].modulation = fm1Modulation
    _data.radios[2].volume = fm1Volume
    _data.radios[2].freqMin = 29.990e6
    _data.radios[2].freqMax = 87.985e6
    _data.radios[2].volMode = 0
    _data.radios[2].freqMode = 0
    _data.radios[2].rtMode = 0
    _data.radios[2].model = SR.RadioModels.AN_ARC201D

    -- AN/ARC-164 UHF
    local arc164Device = GetDevice(5)
    local arc164Power = GetDevice(0):get_argument_value(50) > 0
    local arc164Volume = 0
    local arc164Freq = 0
    local arc164SecFreq = 0

    if arc164Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        arc164Volume = GetDevice(0):get_argument_value(51)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(404)
        arc164Freq = arc164Device:get_frequency()
        arc164SecFreq = 243e6
    end

    _data.radios[3].name = "AN/ARC-164(V)"
    _data.radios[3].freq = arc164Freq
    _data.radios[3].modulation = 0
    _data.radios[3].secFreq = arc164SecFreq
    _data.radios[3].volume = arc164Volume
    _data.radios[3].freqMin = 225e6
    _data.radios[3].freqMax = 399.975e6
    _data.radios[3].volMode = 0
    _data.radios[3].freqMode = 0
    _data.radios[3].rtMode = 0
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    -- AN/ARC-186 VHF
    local arc186Device = GetDevice(8)
    local arc186Power = GetDevice(0):get_argument_value(419) > 0
    local arc186Volume = 0
    local arc186Freq = 0
    local arc186SecFreq = 0

    if arc186Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        arc186Volume = GetDevice(0):get_argument_value(410)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(405)
        arc186Freq = get_param_handle("ARC186param"):get() --arc186Device:get_frequency()
        arc186SecFreq = 121.5e6
    end

    if not (arc186Power and isDCPower) then
        arc186Freq = 0
        arc186SecFreq = 0
    end

    _data.radios[4].name = "AN/ARC-186(V)"
    _data.radios[4].freq = arc186Freq
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = arc186SecFreq
    _data.radios[4].volume = arc186Volume
    _data.radios[4].freqMin = 30e6
    _data.radios[4].freqMax = 151.975e6
    _data.radios[4].volMode = 0
    _data.radios[4].freqMode = 0
    _data.radios[4].rtMode = 0
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    -- Copilot's AN/ARC-201 FM
    local fm2Device = GetDevice(10)
    local fm2Power = GetDevice(0):get_argument_value(701) > 0.01
    local fm2Volume = 0
    local fm2Freq = 0
    local fm2Modulation = 1

    if fm2Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm2Volume = GetDevice(0):get_argument_value(704)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(406)
        fm2Freq = fm2Device:get_frequency()
        ARC201FM2Freq = get_param_handle("ARC201FM2param"):get()
        fm2Modulation = get_param_handle("ARC201_FM2_MODULATION"):get()
    end

    if not (fm2Power and isDCPower) then
        ARC201FM2Freq = 0
    end

    _data.radios[5].name = "AN/ARC-201 (2)"
    _data.radios[5].freq = ARC201FM2Freq --fm2Freq
    _data.radios[5].modulation = fm2Modulation
    _data.radios[5].volume = fm2Volume
    _data.radios[5].freqMin = 29.990e6
    _data.radios[5].freqMax = 87.985e6
    _data.radios[5].volMode = 0
    _data.radios[5].freqMode = 0
    _data.radios[5].rtMode = 0
    _data.radios[5].model = SR.RadioModels.AN_ARC201D

    -- AN/ARC-220 HF radio - not implemented in module, freqs must be changed through SRS UI
    _data.radios[6].name = "AN/ARC-220"
    _data.radios[6].freq = 2e6
    _data.radios[6].modulation = 0
    _data.radios[6].volume = GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(407)
    _data.radios[6].freqMin = 2e6
    _data.radios[6].freqMax = 29.9999e6
    _data.radios[6].volMode = 1
    _data.radios[6].freqMode = 1
    _data.radios[6].encKey = 1
    _data.radios[6].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting --ANDR0ID Added
    _data.radios[6].rtMode = 1
    _data.radios[6].model = SR.RadioModels.AN_ARC220

    -- Only select radio if power to ICS panel
    local radioXMTSelectorValue = _data.selected or 0
    if isDCPower then
        radioXMTSelectorValue = SR.round(GetDevice(0):get_argument_value(400) * 5, 1)
        -- SR.log(radioXMTSelectorValue)
    end

    _data.selected = radioXMTSelectorValue
    _data.intercomHotMic = GetDevice(0):get_argument_value(402) > 0
    _data.ptt = GetDevice(0):get_argument_value(82) > 0
    _data.control = 1 -- full radio HOTAS control

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "uh60" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "uh60" }
    end

    return _data
end

function SR.exportRadioSH60B(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    local isDCPower = SR.getButtonPosition(17) > 0 -- just using battery switch position for now, could tie into DC ESS BUS later?
    local intercomVolume = 0
    if isDCPower then
        -- ics master volume
        intercomVolume = GetDevice(0):get_argument_value(401)
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = intercomVolume
    _data.radios[1].volMode = 0
    _data.radios[1].freqMode = 0
    _data.radios[1].rtMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    -- Copilot's AN/ARC-182 FM (COM1)
    local fm2Device = GetDevice(8)
    local fm2Power = GetDevice(0):get_argument_value(3113) > 0 --NEEDS UPDATE
    local fm2Volume = 0
    local fm2Freq = 0
    local fm2Mod = 0

    if fm2Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm2Volume = GetDevice(0):get_argument_value(3167)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(403)
        fm2Freq = fm2Device:get_frequency()
        ARC182FM2Freq = get_param_handle("ARC182_1_param"):get()
        fm2Mod = GetDevice(0):get_argument_value(3119)
    end

    if not (fm2Power and isDCPower) then
        ARC182FM2Freq = 0
    end

    if fm2Mod == 1 then --Nessecary since cockpit switches are inverse from SRS settings
        fm2ModCorrected = 0
    else
        fm2ModCorrected = 1
    end

    _data.radios[2].name = "AN/ARC-182 (1)"
    _data.radios[2].freq = ARC182FM2Freq --fm2Freq
    _data.radios[2].modulation = fm2ModCorrected
    _data.radios[2].volume = fm2Volume
    _data.radios[2].freqMin = 30e6
    _data.radios[2].freqMax = 399.975e6
    _data.radios[2].volMode = 0
    _data.radios[2].freqMode = 0
    _data.radios[2].rtMode = 0
    _data.radios[2].model = SR.RadioModels.AN_ARC182

    -- Pilots' AN/ARC-182 FM (COM2)
    local fm1Device = GetDevice(6)
    local fm1Power = GetDevice(0):get_argument_value(3113) > 0 --NEEDS UPDATE
    local fm1Volume = 0
    local fm1Freq = 0
    local fm1Mod = 0

    if fm1Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        fm1Volume = GetDevice(0):get_argument_value(3168)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(404)
        fm1Freq = fm1Device:get_frequency()
        ARC182FM1Freq = get_param_handle("ARC182_2_param"):get()
        fm1Mod = GetDevice(0):get_argument_value(3120)
    end

    if not (fm1Power and isDCPower) then
        ARC182FM1Freq = 0
    end

    if fm1Mod == 1 then
        fm1ModCorrected = 0
    else
        fm1ModCorrected = 1
    end

    _data.radios[3].name = "AN/ARC-182 (2)"
    _data.radios[3].freq = ARC182FM1Freq --fm1Freq
    _data.radios[3].modulation = fm1ModCorrected
    _data.radios[3].volume = fm1Volume
    _data.radios[3].freqMin = 30e6
    _data.radios[3].freqMax = 399.975e6
    _data.radios[3].volMode = 0
    _data.radios[3].freqMode = 0
    _data.radios[3].rtMode = 0
    _data.radios[3].model = SR.RadioModels.AN_ARC182

    --D/L not implemented in module, using a "dummy radio" for now
    _data.radios[4].name = "DATA LINK (D/L)"
    _data.radios[4].freq = 0
    _data.radios[4].modulation = 0
    _data.radios[4].volume = GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(409)
    _data.radios[4].freqMin = 15e9
    _data.radios[4].freqMax = 15e9
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 0
    _data.radios[4].rtMode = 1
    _data.radios[4].model = SR.RadioModels.LINK16

    -- AN/ARC-174A HF radio - not implemented in module, freqs must be changed through SRS UI
    _data.radios[5].name = "AN/ARC-174(A)"
    _data.radios[5].freq = 2e6
    _data.radios[5].modulation = 0
    _data.radios[5].volume = GetDevice(0):get_argument_value(401) * GetDevice(0):get_argument_value(407)
    _data.radios[5].freqMin = 2e6
    _data.radios[5].freqMax = 29.9999e6
    _data.radios[5].volMode = 1
    _data.radios[5].freqMode = 1
    _data.radios[5].encKey = 1
    _data.radios[5].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting --ANDR0ID Added
    _data.radios[5].rtMode = 1

    -- Only select radio if power to ICS panel
    local radioXMTSelectorValue = _data.selected or 0
    if isDCPower then
        radioXMTSelectorValue = SR.round(GetDevice(0):get_argument_value(400) * 5, 1)
        -- SR.log(radioXMTSelectorValue)
    end

    -- UHF/VHF BACKUP
    local arc164Device = GetDevice(5)
    local arc164Power = GetDevice(0):get_argument_value(3091) > 0
    local arc164Volume = 0
    local arc164Freq = 0
    local arc164Mod = 0
    --local arc164SecFreq = 0

    if arc164Power and isDCPower then
        -- radio volume * ics master volume * ics switch
        arc164Volume = GetDevice(0):get_argument_value(3089)
            * GetDevice(0):get_argument_value(401)
            * GetDevice(0):get_argument_value(405)
        arc164Freq = get_param_handle("VUHFB_FREQ"):get()
        arc164Mod = GetDevice(0):get_argument_value(3094)
        --arc164SecFreq = 243e6
    end

    if arc164Mod == 1 then
        arc164ModCorrected = 0
    else
        arc164ModCorrected = 1
    end

    _data.radios[6].name = "UHF/VHF BACKUP"
    _data.radios[6].freq = arc164Freq * 1000
    _data.radios[6].modulation = arc164ModCorrected
    --_data.radios[6].secFreq = arc164SecFreq
    _data.radios[6].volume = arc164Volume
    _data.radios[6].freqMin = 30e6
    _data.radios[6].freqMax = 399.975e6
    _data.radios[6].volMode = 0
    _data.radios[6].freqMode = 0
    _data.radios[6].rtMode = 0
    _data.radios[6].model = SR.RadioModels.AN_ARC164

    _data.selected = radioXMTSelectorValue
    _data.intercomHotMic = GetDevice(0):get_argument_value(402) > 0
    _data.ptt = GetDevice(0):get_argument_value(82) > 0
    _data.control = 1 -- full radio HOTAS control

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "uh60" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "uh60" }
    end

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
    arc51.modulation = 0 -- AM only
    arc51.freqMin = 220.000e6
    arc51.freqMax = 399.950e6
    arc51.model = SR.RadioModels.AN_ARC51BX

    -- TODO Check if there are other volume knobs in series
    arc51.volume = SR.getRadioVolume(0, 365, { 0.2, 0.8 }, false)
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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(26)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "A4" }
        else
            _data.ambient = { vol = 0.2, abType = "A4" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "A4" }
    end

    return _data
end

function SR.exportRadioSK60(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-164 UHF"
    _data.radios[2].freq = SR.getRadioFrequency(6)
    _data.radios[2].modulation = 1
    _data.radios[2].volume = 1.0
    _data.radios[2].volMode = 1
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-186(V)FM"
    _data.radios[4].freq = 30.0 * 1000000
    _data.radios[4].modulation = 1
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30 * 1000000
    _data.radios[4].freqMax = 76 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door < 0.9 then
            _data.ambient = { vol = 0.3, abType = "sk60" }
        else
            _data.ambient = { vol = 0.2, abType = "sk60" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "sk60" }
    end

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
    _data.radios[1].model = SR.RadioModels.Intercom

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
    arc182.model = SR.RadioModels.AN_ARC182

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
    arc182_2.model = SR.RadioModels.AN_ARC182

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

    _data.control = 1 -- full radio HOTAS control

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "jet" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "jet" }
    end

    return _data
end

function SR.exportRadioPUCARA(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = GetDevice(0):get_argument_value(764)
    _data.radios[1].model = SR.RadioModels.Intercom

    local comm1Switch = GetDevice(0):get_argument_value(762)
    local comm2Switch = GetDevice(0):get_argument_value(763)
    local comm1PTT = GetDevice(0):get_argument_value(765)
    local comm2PTT = GetDevice(0):get_argument_value(7655)
    local modeSelector1 = GetDevice(0):get_argument_value(1080) -- 0:off, 0.25:T/R, 0.5:T/R+G
    local amfm = GetDevice(0):get_argument_value(770)

    _data.radios[2].name = "SUNAIR ASB-850 COM1"
    _data.radios[2].modulation = amfm
    _data.radios[2].volume = SR.getRadioVolume(0, 1079, { 0.0, 1.0 }, false)

    if comm1Switch == 0 then
        _data.radios[2].freq = 246.000e6
        _data.radios[2].secFreq = 0
    elseif comm1Switch == 1 then
        local one = 100.000e6 * SR.getSelectorPosition(1090, 1 / 4)
        local two = 10.000e6 * SR.getSelectorPosition(1082, 1 / 10)
        local three = 1.000e6 * SR.getSelectorPosition(1084, 1 / 10)
        local four = 0.1000e6 * SR.getSelectorPosition(1085, 1 / 10)
        local five = 0.010e6 * SR.getSelectorPosition(1087, 1 / 10)
        local six = 0.0010e6 * SR.getSelectorPosition(1086, 1 / 10)
        mainFreq = one + two + three + four + five - six
        _data.radios[2].freq = mainFreq
        _data.radios[2].secFreq = 0
    end

    _data.radios[3].name = "RTA-42A BENDIX COM2"
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 1100, { 0.0, 1.0 }, false)

    if comm2Switch == 0 then
        _data.radios[3].freq = 140.000e6
        _data.radios[3].secFreq = 0
    elseif comm2Switch == 1 then
        local onea = 100.000e6 * SR.getSelectorPosition(1104, 1 / 4)
        local twoa = 10.000e6 * SR.getSelectorPosition(1103, 1 / 10)

        mainFreqa = onea + twoa
        _data.radios[3].freq = mainFreqa
        _data.radios[3].secFreq = 0
    end

    _data.control = 1 -- Hotas Controls radio

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "jet" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "jet" }
    end

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
        if _ufcp.com1_freq then
            com1_freq = (_ufcp.com1_freq * 1000000)
        end
        if _ufcp.com1_mod then
            com1_mod = _ufcp.com1_mod * 1
        end
        if _ufcp.com1_sql then
            com1_sql = _ufcp.com1_sql
        end
        if _ufcp.com1_pwr then
            com1_pwr = _ufcp.com1_pwr
        end
        if _ufcp.com1_mode then
            com1_mode = _ufcp.com1_mode * 1
        end

        if _ufcp.com2_freq then
            com2_freq = (_ufcp.com2_freq * 1000000)
        end
        if _ufcp.com2_mod then
            com2_mod = _ufcp.com2_mod * 1
        end
        if _ufcp.com2_sql then
            com2_sql = _ufcp.com2_sql
        end
        if _ufcp.com2_pwr then
            com2_pwr = _ufcp.com2_pwr
        end
        if _ufcp.com2_mode then
            com2_mode = _ufcp.com2_mode * 1
        end
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

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(26)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "a29" }
        else
            _data.ambient = { vol = 0.2, abType = "a29" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "a29" }
    end

    return _data
end

function SR.exportRadioVSNF104C(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    _data.control = 0 -- full radio

    -- UHF radio
    local arc66 = {}
    arc66.DeviceId = 1
    arc66.Frequency = 0
    arc66.Volume = GetDevice(0):get_argument_value(506) --TEST-- --0		-- no volume control in cockpit definitions
    arc66.Mode = { OFF = 0, TR = 1, TRG = 2 }
    arc66.ModeSw = (GetDevice(0):get_argument_value(505)) * 2
    arc66.ChannelSw = GetDevice(0):get_argument_value(302)
    arc66.PttSw = 0 -- GetDevice(arc66.DeviceId):is_ptt_pressed()
    arc66.Power = GetDevice(arc66.DeviceId):is_on()
    arc66.Manual = GetDevice(0):get_argument_value(504)
    --new
    arc66.Channel = math.floor(arc66.ChannelSw * 100.0)

    -- UHF guard channel
    local guard = {}
    guard.DeviceId = 2
    guard.Frequency = 0

    -- intercom
    local ics = {}
    ics.DeviceId = 3
    ics.Frequency = 0

    local iff = {}
    iff.DeviceId = 24 -- defined in devices, but not used by this script
    iff.MstrMode = { OFF = 0, STBY = 1, LOW = 2, NORM = 3, EMER = 4 }
    iff.MstrModeSw = (GetDevice(0):get_argument_value(243)) * 4
    iff.Ident = { MIC = 0, OUT = 1, IP = 2 }
    iff.IdentSw = (GetDevice(0):get_argument_value(240)) * 2
    iff.Mode1Code = 0 -- not in cockpit definitions yet
    iff.Mode = { OUT = 0, ON = 1 }
    iff.Mode2Sw = GetDevice(0):get_argument_value(242)
    iff.Mode3Sw = GetDevice(0):get_argument_value(241)
    iff.Mode3Code = 0 -- not in cockpit definitions yet

    if arc66.PttSw == 1 then
        _data.ptt = true
    end

    if arc66.Power and (arc66.ModeSw ~= arc66.Mode.OFF) then
        arc66.Frequency = math.floor((GetDevice(arc66.DeviceId):get_frequency() + 5000 / 2) / 5000) * 5000
        ics.Frequency = 100.0
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
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = ics.Frequency
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = arc66.Volume

    -- ARC-66 UHF radio
    _data.radios[2].name = "AN/ARC-66"
    _data.radios[2].freq = arc66.Frequency
    _data.radios[2].secFreq = guard.Frequency
    _data.radios[2].modulation = 0 -- AM only
    _data.radios[2].freqMin = 225.000e6
    _data.radios[2].freqMax = 399.900e6
    _data.radios[2].volume = arc66.Volume

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
    _data.iff = { status = 0, mode1 = 0, mode2 = 0, mode3 = 0, mode4 = false, control = 0, expansion = false }

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

    return _data
end

function SR.exportRadioVSNF4(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "AN/ARC-164 UHF"
    _data.radios[2].freq = SR.getRadioFrequency(2)
    _data.radios[2].modulation = 0
    _data.radios[2].secFreq = 0
    _data.radios[2].volume = 1.0
    _data.radios[2].volMode = 1
    _data.radios[2].freqMode = 0
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

    _data.radios[2].encKey = 1
    _data.radios[2].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "jet" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "jet" }
    end

    return _data
end

function SR.exportRadioHercules(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = true, desc = "" }
    _data.iff =
        { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 1, expansion = false, mic = -1 }

    -- Intercom
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1 -- Overlay control
    _data.radios[1].model = SR.RadioModels.Intercom

    -- AN/ARC-164(V) Radio
    -- Use the Pilot's volume for any station other
    -- than the copilot.
    local volumeKnob = 1430 -- PILOT_ICS_Volume_Rot
    if SR.lastKnownSeat == 1 then
        volumeKnob = 1432 -- COPILOT_ICS_Volume_Rot
    end
    local arc164 = GetDevice(18)
    _data.radios[2].name = "AN/ARC-164 UHF"
    if arc164:is_on() then
        _data.radios[2].freq = arc164:get_frequency()
        _data.radios[2].secFreq = 243e6
    else
        _data.radios[2].freq = 0
        _data.radios[2].secFreq = 0
    end
    _data.radios[2].modulation = arc164:get_modulation()

    _data.radios[2].volume = SR.getRadioVolume(0, volumeKnob, { -1.0, 1.0 })
    _data.radios[2].freqMin = 225e6
    _data.radios[2].freqMax = 399.975e6
    _data.radios[2].volMode = 0
    _data.radios[2].freqMode = 0
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    -- Expansions - Server Side Controlled
    -- VHF AM - 116-151.975MHz
    _data.radios[3].name = "AN/ARC-186(V) AM"
    _data.radios[3].freq = 124.8e6
    _data.radios[3].modulation = 0 -- AM
    _data.radios[3].secFreq = 121.5e6
    _data.radios[3].volume = 1.0
    _data.radios[3].freqMin = 116e6
    _data.radios[3].freqMax = 151.975e6
    _data.radios[3].volMode = 1
    _data.radios[3].freqMode = 1
    _data.radios[3].expansion = false
    _data.radios[3].model = SR.RadioModels.AN_ARC186

    -- VHF FM - 30-87.975MHz
    _data.radios[4].name = "AN/ARC-186(V) FM"
    _data.radios[4].freq = 30e6
    _data.radios[4].modulation = 1 -- FM
    _data.radios[4].secFreq = 0
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 30e6
    _data.radios[4].freqMax = 87.975e6
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = false
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "hercules" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "hercules" }
    end

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
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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
    _data.radios[3].model = SR.RadioModels.AN_ARC164

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
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        --   local _door = SR.getButtonPosition(181)

        --      if _door > 0.2 then
        _data.ambient = { vol = 0.3, abType = "f15" }
        --  else
        --    _data.ambient = {vol = 0.2,  abType = 'f15' }
        --  end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f15" }
    end

    return _data
end

function SR.exportRadioF15ESE(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = true, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-164 UHF-1"
    _data.radios[2].freq = SR.getRadioFrequency(7)
    _data.radios[2].modulation = SR.getRadioModulation(7)
    _data.radios[2].model = SR.RadioModels.AN_ARC164

    _data.radios[3].name = "AN/ARC-164 UHF-2"
    _data.radios[3].freq = SR.getRadioFrequency(8)
    _data.radios[3].modulation = SR.getRadioModulation(8)
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    -- TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    -- {"UFC_CC_01":"","UFC_CC_02":"","UFC_CC_03":"","UFC_CC_04":"","UFC_DISPLAY":"","UFC_SC_01":"LAW 250'","UFC_SC_02":"TCN OFF","UFC_SC_03":"IFF 4","UFC_SC_04":"TF OFF","UFC_SC_05":"*U243000G","UFC_SC_05A":".","UFC_SC_06":" G","UFC_SC_07":"GV ","UFC_SC_08":"U133000*","UFC_SC_08A":".","UFC_SC_09":"N-F OFF","UFC_SC_10":"A4/E4","UFC_SC_11":"A/P OFF","UFC_SC_12":"STR B"}

    local setGuard = function(freq)
        -- GUARD changes based on the tuned frequency
        if freq > 108 * 1000000 and freq < 135.995 * 1000000 then
            return 121.5 * 1000000
        end
        if freq > 108 * 1000000 and freq < 399.975 * 1000000 then
            return 243 * 1000000
        end

        return -1
    end

    local _ufc = SR.getListIndicatorValue(9)

    if _ufc and _ufc.UFC_SC_05 and string.find(_ufc.UFC_SC_05, "G", 1, true) and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = setGuard(_data.radios[2].freq)
    end

    if _ufc and _ufc.UFC_SC_08 and string.find(_ufc.UFC_SC_08, "G", 1, true) and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = setGuard(_data.radios[3].freq)
    end

    if _seat == 0 then
        _data.radios[1].volume = SR.getRadioVolume(0, 504, { 0.0, 1.0 }, false)
        _data.radios[2].volume = SR.getRadioVolume(0, 282, { 0.0, 1.0 }, false)
        _data.radios[3].volume = SR.getRadioVolume(0, 283, { 0.0, 1.0 }, false)

        if SR.getButtonPosition(509) == 0.5 then
            _data.intercomHotMic = true
        end
    else
        _data.radios[1].volume = SR.getRadioVolume(0, 1422, { 0.0, 1.0 }, false)
        _data.radios[2].volume = SR.getRadioVolume(0, 1307, { 0.0, 1.0 }, false)
        _data.radios[3].volume = SR.getRadioVolume(0, 1308, { 0.0, 1.0 }, false)

        if SR.getButtonPosition(1427) == 0.5 then
            _data.intercomHotMic = true
        end
    end

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "f15" }
        else
            _data.ambient = { vol = 0.2, abType = "f15" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f15" }
    end

    -- HANDLE TRANSPONDER
    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local _iffDevice = GetDevice(68)

    if _iffDevice:hasPower() then
        _data.iff.status = 1 -- NORMAL

        if _iffDevice:isIdentActive() then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    else
        _data.iff.status = -1
    end

    if _iffDevice:isModeActive(4) then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if _iffDevice:isModeActive(3) then
        _data.iff.mode3 = tonumber(_iffDevice:getModeCode(3))
    else
        _data.iff.mode3 = -1
    end

    if _iffDevice:isModeActive(2) then
        _data.iff.mode2 = tonumber(_iffDevice:getModeCode(2))
    else
        _data.iff.mode2 = -1
    end

    if _iffDevice:isModeActive(1) then
        _data.iff.mode1 = tonumber(_iffDevice:getModeCode(1))
    else
        _data.iff.mode1 = -1
    end

    -- local temp = {}
    -- temp.mode4 = string.format("%04d",_iffDevice:getModeCode(4)) -- mode 4
    -- temp.mode1 = string.format("%02d",_iffDevice:getModeCode(1))
    -- temp.mode3 = string.format("%04d",_iffDevice:getModeCode(3))
    -- temp.mode2 = string.format("%04d",_iffDevice:getModeCode(2))
    -- temp.mode4Active = _iffDevice:isModeActive(4)
    -- temp.mode1Active = _iffDevice:isModeActive(1)
    -- temp.mode3Active = _iffDevice:isModeActive(3)
    -- temp.mode2Active = _iffDevice:isModeActive(2)
    -- temp.ident = _iffDevice:isIdentActive()
    -- temp.power = _iffDevice:hasPower()

    return _data
end

function SR.exportRadioUH1H(_data)
    local intercomOn = SR.getButtonPosition(27)
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = SR.getRadioVolume(0, 29, { 0.3, 1.0 }, true)
    _data.radios[1].model = SR.RadioModels.Intercom

    if intercomOn > 0.5 then
        --- control hot mic instead of turning it on and off
        _data.intercomHotMic = true
    end

    local fmOn = SR.getButtonPosition(23)
    _data.radios[2].name = "AN/ARC-131"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].modulation = 1
    _data.radios[2].volume = SR.getRadioVolume(0, 37, { 0.3, 1.0 }, true)
    _data.radios[2].model = SR.RadioModels.AN_ARC131

    if fmOn < 0.5 then
        _data.radios[2].freq = 1
    end

    local uhfOn = SR.getButtonPosition(24)
    _data.radios[3].name = "AN/ARC-51BX - UHF"
    _data.radios[3].freq = SR.getRadioFrequency(22)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 21, { 0.0, 1.0 }, true)
    _data.radios[3].model = SR.RadioModels.AN_ARC51BX

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

    local vhfOn = SR.getButtonPosition(25)
    _data.radios[4].name = "AN/ARC-134"
    _data.radios[4].freq = SR.getRadioFrequency(20)
    _data.radios[4].modulation = 0
    _data.radios[4].volume = SR.getRadioVolume(0, 9, { 0.0, 0.60 }, false)
    _data.radios[4].model = SR.RadioModels.AN_ARC134

    if vhfOn < 0.5 then
        _data.radios[4].freq = 1
    end

    --_device:get_argument_value(_arg)

    -- TODO check it works
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    if _seat == 0 then
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

        _data.control = 1 -- Full Radio

        _data.capabilities = {
            dcsPtt = true,
            dcsIFF = true,
            dcsRadioSwitch = true,
            intercomHotMic = true,
            desc = "Hot mic on INT switch",
        }
    else
        _data.control = 0 -- no copilot or gunner radio controls - allow them to switch

        _data.radios[1].volMode = 1
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1

        _data.capabilities = {
            dcsPtt = false,
            dcsIFF = true,
            dcsRadioSwitch = false,
            intercomHotMic = true,
            desc = "Hot mic on INT switch",
        }
    end

    -- HANDLE TRANSPONDER
    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(59, 0.1)

    local iffIdent = SR.getButtonPosition(66) -- -1 is off 0 or more is on

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

    local mode1On = SR.getButtonPosition(61)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(68, 0.33), 0.1) * 10
        + SR.round(SR.getSelectorPosition(69, 0.11), 0.1)

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(63)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(70, 0.11), 0.1) * 1000
        + SR.round(SR.getSelectorPosition(71, 0.11), 0.1) * 100
        + SR.round(SR.getSelectorPosition(72, 0.11), 0.1) * 10
        + SR.round(SR.getSelectorPosition(73, 0.11), 0.1)

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(67)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    local _doorLeft = SR.getButtonPosition(420)

    -- engine on
    if SR.getAmbientVolumeEngine() > 10 then
        if _doorLeft >= 0 and _doorLeft < 0.5 then
            -- engine on and door closed
            _data.ambient = { vol = 0.2, abType = "uh1" }
        else
            -- engine on and door open
            _data.ambient = { vol = 0.35, abType = "uh1" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "uh1" }
    end

    -- SR.log("ambient STATUS"..SR.JSON:encode(_data.ambient).."\n\n")

    return _data
end

local _ch47 = {}
_ch47.radio1 = { enc = false }
_ch47.radio2 = { guard = 0, enc = false }
_ch47.radio3 = { guard = 0, enc = false }

function SR.exportRadioCH47F(_data)
    -- RESET
    if _lastUnitId ~= _data.unitId then
        _ch47.radio1 = { enc = false }
        _ch47.radio2 = { guard = 0, enc = false }
        _ch47.radio3 = { guard = 0, enc = false }
    end

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-201 FM1" -- ARC 201
    _data.radios[2].freq = SR.getRadioFrequency(51)
    _data.radios[2].modulation = SR.getRadioModulation(51)
    _data.radios[2].model = SR.RadioModels.AN_ARC201D

    _data.radios[2].encKey = 1
    _data.radios[2].encMode = 3 -- Cockpit Toggle + Gui Enc key setting

    _data.radios[3].name = "AN/ARC-164 UHF" -- ARC_164
    _data.radios[3].freq = SR.getRadioFrequency(49)
    _data.radios[3].modulation = SR.getRadioModulation(49)
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- Cockpit Toggle + Gui Enc key setting

    _data.radios[4].name = "AN/ARC-186 VHF" -- ARC_186
    _data.radios[4].freq = SR.getRadioFrequency(50)
    _data.radios[4].modulation = SR.getRadioModulation(50)
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 3 -- Cockpit Toggle + Gui Enc key setting

    -- Handle GUARD freq selection for the VHF Backup head.
    local arc186FrequencySelectionDial = SR.getSelectorPosition(1221, 0.1)
    if arc186FrequencySelectionDial == 0 then
        _data.radios[4].freq = 40.5e6
        _data.radios[4].modulation = 1
    elseif arc186FrequencySelectionDial == 1 then
        _data.radios[4].freq = 121.5e6
        _data.radios[4].modulation = 0
    end

    _data.radios[5].name = "AN/ARC-220 HF" -- ARC_220
    _data.radios[5].freq = SR.getRadioFrequency(52)
    _data.radios[5].modulation = SR.getRadioModulation(52)
    _data.radios[5].model = SR.RadioModels.AN_ARC220

    _data.radios[5].encMode = 0

    -- TODO (still in overlay)
    _data.radios[6].name = "AN/ARC-201 FM2"
    -- _data.radios[6].freq = SR.getRadioFrequency(32)
    _data.radios[6].freq = 32000000
    _data.radios[6].modulation = 1
    _data.radios[6].model = SR.RadioModels.AN_ARC201D

    _data.radios[6].freqMin = 20.0 * 1000000
    _data.radios[6].freqMax = 60.0 * 1000000
    _data.radios[6].freqMode = 1

    _data.radios[6].encKey = 1
    _data.radios[6].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting

    local _seat = SR.lastKnownSeat

    local _pilotCopilotRadios = function(_offset, _pttArg)
        _data.radios[1].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false) -- 23
        _data.radios[2].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 1) -- +1
        _data.radios[3].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 2, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 3) -- +3
        _data.radios[4].volume = SR.getRadioVolume(0, 1219, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 4, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 5)
        _data.radios[5].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 6, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 7)
        _data.radios[6].volume = SR.getRadioVolume(0, _offset + 23, { 0, 1.0 }, false)
            * SR.getRadioVolume(0, _offset + 8, { 0, 1.0 }, false)
            * SR.getButtonPosition(_offset + 9)

        local _selector = SR.getSelectorPosition(_offset + 22, 0.05)

        if _selector <= 6 then
            _data.selected = _selector
        elseif _offset ~= 657 and _selector == 9 then -- BU
            -- Look up the BKUP RAD SEL switch to know which radio we have.
            local bkupRadSel = SR.getButtonPosition(1466)
            if bkupRadSel < 0.5 then
                -- Switch facing down: Pilot gets V3, Copilot U2.
                _data.selected = _offset == 591 and 3 or 2
            else
                -- Other way around.
                _data.selected = _offset == 591 and 2 or 3
            end
        else -- 8 = RMT, TODO
            _data.selected = -1
        end

        if _pttArg > 0 then
            local _ptt = SR.getButtonPosition(_pttArg)

            if _ptt >= 0.1 then
                if _ptt == 0.5 then
                    -- intercom
                    _data.selected = 0
                end

                _data.ptt = true
            end
        end
    end

    if _seat == 0 then -- 591
        local _offset = 591

        _pilotCopilotRadios(591, 1271)

        _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
        _data.control = 1 -- Full Radio
    elseif _seat == 1 then --624
        _pilotCopilotRadios(624, 1283)

        _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
        _data.control = 1 -- Full Radio
    elseif _seat == 2 then --657
        _pilotCopilotRadios(657, -1)

        _data.capabilities =
            { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
    else
        _data.radios[1].volume = 1.0
        _data.radios[2].volume = 1.0
        _data.radios[3].volume = 1.0
        _data.radios[4].volume = 1.0
        _data.radios[5].volume = 1.0
        _data.radios[6].volume = 1.0

        _data.radios[1].volMode = 1
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1
        _data.radios[5].volMode = 1
        _data.radios[6].volMode = 1

        _data.capabilities =
            { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    end

    -- EMER Guard switch.
    -- If enabled, forces F1, U2, and V3 to GUARDs.
    local manNormGuard = SR.getSelectorPosition(583, 0.1)
    if manNormGuard > 1 then
        _data.radios[2].freq = 40.5e6 -- F1
        _data.radios[3].freq = 243e6 -- U2
        _data.radios[4].freq = 121.5e6 -- V3
    end

    -- EMER IFF.
    -- When enabled, toggles all transponders ON.
    -- Since we currently can't change M1 and M2 codes in cockpit,
    -- Set 3A to 7700, and enable S.
    --[[ FIXME: Having issues handing over the controls back to the overlay.
    local holdOffEmer = SR.getSelectorPosition(585, 0.1)
    if holdOffEmer > 1 then
        _data.iff = {
        status = 1,
        mode3 = 7700,
        mode4 = true,
        control = 0
        }
    else
        -- Release control back to overlay.
        _data.iff.control = 1
    end
    ]]

    -- engine on
    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "ch47" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ch47" }
    end

    local _ufc = nil
    if _seat == 0 then
        -- RIGHT SEAT (pilot)
        _ufc = SR.getListIndicatorValue(1)
    elseif _seat == 1 then
        -- LEFT SEAT (Copilot)
        _ufc = SR.getListIndicatorValue(0)
    end

    if _ufc ~= nil then
        if _ufc["pg_title_F1_FM_FH_COMM"] then
            --   "pg_title_F1_FM_FH_COMM": "F1 CONTROL",
            -- IF CIPHER
            --   "F1_FM_FH_COMSEC_MODE_CIPHER": "CIPHER",

            if _ufc["F1_FM_FH_COMSEC_MODE_CIPHER"] then
                _ch47.radio1.enc = true
            else
                _ch47.radio1.enc = false
            end
        elseif _ufc["pg_title_U2_COMM"] then
            --   "pg_title_U2_COMM": "U2 CONTROL",
            --     "U2_VHF_AM_MODE_TR_plus_G": "TR+G",
            --   "U2_VHF_AM_COMSEC_MODE_CIPHER": "CIPHER",

            if _ufc["U2_VHF_AM_COMSEC_MODE_CIPHER"] then
                _ch47.radio2.enc = true
            else
                _ch47.radio2.enc = false
            end

            if _ufc["U2_VHF_AM_MODE_TR_plus_G"] then
                _ch47.radio2.guard = 243.0 * 1000000
            else
                _ch47.radio2.guard = 0
            end
        elseif _ufc["pg_title_V3_COMM"] then
            --   "pg_title_V3_COMM": "V3 CONTROL",
            --   "V3_VHF_AM_FM_COMSEC_MODE_CIPHER": "CIPHER",
            --   "V3_VHF_AM_FM_MODE_TR_plus_G": "TR+G",

            if _ufc["V3_VHF_AM_FM_COMSEC_MODE_CIPHER"] then
                _ch47.radio3.enc = true
            else
                _ch47.radio3.enc = false
            end

            if _ufc["V3_VHF_AM_FM_MODE_TR_plus_G"] then
                _ch47.radio3.guard = 121.5 * 1000000
            else
                _ch47.radio3.guard = 0
            end
        elseif _ufc["pg_title_COMM"] then
            --   "F1_COMSEC_MODE_CIPHER": "C",
            --   "U2_COMSEC_MODE_CIPHER": "C",
            --   "V3_COMSEC_MODE_CIPHER": "C",

            if _ufc["F1_COMSEC_MODE_CIPHER"] then
                _ch47.radio1.enc = true
            else
                _ch47.radio1.enc = false
            end

            if _ufc["U2_COMSEC_MODE_CIPHER"] then
                _ch47.radio2.enc = true
            else
                _ch47.radio2.enc = false
            end

            if _ufc["V3_COMSEC_MODE_CIPHER"] then
                _ch47.radio3.enc = true
            else
                _ch47.radio3.enc = false
            end
        end
    end

    _data.radios[2].enc = _ch47.radio1.enc
    _data.radios[3].enc = _ch47.radio2.enc
    _data.radios[3].secFreq = _ch47.radio2.guard
    _data.radios[4].enc = _ch47.radio3.enc
    _data.radios[4].secFreq = _ch47.radio3.guard

    return _data
end

function SR.exportRadioSA342(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    -- Check for version
    local _newVersion = false
    local _uhfId = 31
    local _fmId = 28

    pcall(function()
        local temp = SR.getRadioFrequency(30, 500)

        if temp ~= nil then
            _newVersion = true
            _fmId = 27
            _uhfId = 30
        end
    end)

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1
    _data.radios[1].model = SR.RadioModels.Intercom

    -- TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    local vhfVolume = 68 -- IC1_VHF
    local uhfVolume = 69 -- IC1_UHF
    local fm1Volume = 70 -- IC1_FM1

    local vhfPush = 452 -- IC1_VHF_Push
    local fm1Push = 453 -- IC1_FM1_Push
    local uhfPush = 454 -- IC1_UHF_Push

    if _seat == 1 then
        -- Copilot.
        vhfVolume = 79 -- IC2_VHF
        uhfVolume = 80 -- IC2_UHF
        fm1Volume = 81 -- IC2_FM1

        vhfPush = 455 -- IC2_VHF_Push
        fm1Push = 456 -- IC2_FM1_Push
        uhfPush = 457 -- IC2_UHF_Push
    end

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
    _data.radios[2].volume = SR.getRadioVolume(0, vhfVolume, { 1.0, 0.0 }, true)
    _data.radios[2].rtMode = 1

    _data.radios[3].name = "TRA 6031 UHF"

    -- deal with odd radio tune & rounding issue... BUG you cannot set frequency 243.000 ever again
    local freq = SR.getRadioFrequency(_uhfId, 500)
    freq = (math.floor(freq / 1000) * 1000)

    _data.radios[3].freq = freq

    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, uhfVolume, { 0.0, 1.0 }, false)

    _data.radios[3].encKey = 1
    _data.radios[3].encMode = 3 -- 3 is Incockpit toggle + Gui Enc Key setting
    _data.radios[3].rtMode = 1

    _data.radios[4].name = "TRC 9600 PR4G"
    _data.radios[4].freq = SR.getRadioFrequency(_fmId)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, fm1Volume, { 0.0, 1.0 }, false)

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

    if _seat < 2 then
        -- Pilot or Copilot have cockpit controls

        if SR.getButtonPosition(vhfPush) > 0.5 then
            _data.selected = 1
        elseif SR.getButtonPosition(uhfPush) > 0.5 then
            _data.selected = 2
        elseif SR.getButtonPosition(fm1Push) > 0.5 then
            _data.selected = 3
        end

        _data.control = 1 -- COCKPIT Controls
    else
        -- Neither Pilot nor copilot - everything overlay.
        _data.capabilities.dcsRadioSwitch = false
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1

        _data.control = 0 -- OVERLAY Controls
    end

    -- The option reads 'disable HOT_MIC', true means off.
    _data.intercomHotMic = not SR.getSpecialOption("SA342.HOT_MIC")

    -- HANDLE TRANSPONDER
    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getButtonPosition(246)

    local iffIdent = SR.getButtonPosition(240) -- -1 is off 0 or more is on

    if iffPower > 0 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    end

    local mode1On = SR.getButtonPosition(248)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(234, 0.1), 0.1) * 10
        + SR.round(SR.getSelectorPosition(235, 0.1), 0.1)

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(250)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(236, 0.1), 0.1) * 1000
        + SR.round(SR.getSelectorPosition(237, 0.1), 0.1) * 100
        + SR.round(SR.getSelectorPosition(238, 0.1), 0.1) * 10
        + SR.round(SR.getSelectorPosition(239, 0.1), 0.1)

    if mode3On == 0 then
        _data.iff.mode3 = -1
    end

    local mode4On = SR.getButtonPosition(251)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "sa342" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "sa342" }
    end

    return _data
end

local _oh58RetranPersist = nil -- For persistence of retrans variable
function SR.exportRadioOH58D(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "VOX control for intercom volume",
    }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-201D FM1"
    _data.radios[2].freq = SR.getRadioFrequency(29)
    _data.radios[2].modulation = SR.getRadioModulation(29)
    _data.radios[2].volMode = 0
    _data.radios[2].encMode = 2
    _data.radios[2].model = SR.RadioModels.AN_ARC201D

    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(30)
    _data.radios[3].modulation = SR.getRadioModulation(30)
    _data.radios[3].volMode = 0
    _data.radios[3].encMode = 2
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    _data.radios[4].name = "AN/ARC-186 VHF"
    _data.radios[4].freq = SR.getRadioFrequency(31)
    _data.radios[4].modulation = SR.getRadioModulation(31)
    _data.radios[4].volMode = 0
    _data.radios[4].encMode = 2
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.radios[5].name = "AN/ARC-201D FM2"
    _data.radios[5].freq = SR.getRadioFrequency(32)
    _data.radios[5].modulation = SR.getRadioModulation(32)
    _data.radios[5].volMode = 0
    _data.radios[5].encMode = 2
    _data.radios[5].model = SR.RadioModels.AN_ARC201D

    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()
    local _hotMic = 0
    local _selector = 0
    local _cyclicICSPtt = false
    local _cyclicPtt = false
    local _footPtt = false

    local _radioDisplay = SR.getListIndicatorValue(8)
    local _mpdRight = SR.getListIndicatorValue(3)
    local _mpdLeft = SR.getListIndicatorValue(4)
    local _activeRadioParamPrefix = nil

    local _getActiveRadio = function() -- Probably a better way to do this, but it works...
        for i = 1, 5 do
            if tonumber(get_param_handle(_activeRadioParamPrefix .. i):get()) == 1 then
                if i >= 5 then
                    return i - 1
                else
                    return i
                end
            end
        end
    end

    if _seat == 0 then
        _data.radios[1].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 187, { 0.0, 0.8 }, false)
        _data.radios[2].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 175, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(174)
        _data.radios[3].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 177, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(176)
        _data.radios[4].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 179, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(178)
        _data.radios[5].volume = SR.getRadioVolume(0, 173, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 183, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(182)
        -- 186 hotmic
        -- 188 radio selector

        _hotMic = SR.getSelectorPosition(186, 0.1)
        _selector = SR.getSelectorPosition(188, 0.1)

        -- right cyclic ICS (intercom) 1st detent trigger PTT: 400
        -- right cyclic radio 2nd detent trigger PTT: 401
        -- right foot pedal PTT: 404

        _cyclicICSPtt = SR.getButtonPosition(400)
        _cyclicPtt = SR.getButtonPosition(401)
        _footPtt = SR.getButtonPosition(404)

        _activeRadioParamPrefix = "PilotSelect_vis"
    else
        _data.radios[1].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 830, { 0.0, 0.8 }, false)
        _data.radios[2].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 814, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(813)
        _data.radios[3].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 817, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(816)
        _data.radios[4].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 819, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(818)
        _data.radios[5].volume = SR.getRadioVolume(0, 812, { 0.0, 0.8 }, false)
            * SR.getRadioVolume(0, 823, { 0.0, 0.8 }, false)
            * SR.getButtonPosition(822)

        --- 828 Hotmic wheel
        --- 831 radio selector

        _hotMic = SR.getSelectorPosition(828, 0.1)
        _selector = SR.getSelectorPosition(831, 0.1)

        -- left cyclic ICS (intercom) 1st detent trigger PTT: 402
        -- left cyclic radio 2nd detent trigger PTT: 403
        -- left foot pedal PTT: 405

        _cyclicICSPtt = SR.getButtonPosition(402)
        _cyclicPtt = SR.getButtonPosition(403)
        _footPtt = SR.getButtonPosition(405)

        _activeRadioParamPrefix = "CopilotSelect_vis"
    end

    if _hotMic == 0 or _hotMic == 1 then
        _data.intercomHotMic = true
    end

    -- ACTIVE RADIO START
    _selector = _selector > 7 and 7 or _selector -- Sometimes _selector == 10 on start; clamp to 7
    local _mapSelector = {
        [0] = -1, -- PVT
        [1] = 0, -- Intercom
        [2] = 1, -- FM1
        [3] = 2, -- UHF
        [4] = 3, -- VHF
        [5] = -1, -- Radio not implemented (HF/SATCOM)
        [6] = 4, -- FM2
        [7] = _getActiveRadio(), -- RMT
    }
    _data.selected = _mapSelector[_selector]
    -- ACTIVE RADIO END

    -- ENCRYPTION START
    for i = 1, 5 do
        if _radioDisplay == nil then
            break
        end -- Probably no battery power so break
        local _radioTranslate = i < 5 and i + 1 or i
        local _radioChannel = _radioDisplay["CHNL" .. i]
        local _channelToEncKey = function()
            if _radioChannel == "M" or _radioChannel == "C" then
                return 1
            else
                return tonumber(_radioChannel)
            end
        end

        _data.radios[_radioTranslate].enc = tonumber(get_param_handle("Cipher_vis" .. i):get()) == 1
        _data.radios[_radioTranslate].encKey = _channelToEncKey()

        if _radioChannel ~= "M" and _radioChannel ~= "C" then
            _data.radios[_radioTranslate].channel = _data.radios[_radioTranslate].encKey
        end
    end
    -- ENCRYPTION END

    -- FM RETRAN START
    if _mpdLeft ~= nil then
        if _mpdRight["R4_TEXT"] then
            if _mpdRight["R4_TEXT"] == "FM" and _mpdRight["R4_BORDERCONTAINER"] then
                _oh58RetranPersist = true
            elseif _mpdRight["R4_TEXT"] == "FM" and _mpdRight["R4_BORDERCONTAINER"] == nil then
                _oh58RetranPersist = false
            end
        end

        if _mpdLeft["R4_TEXT"] then
            if _mpdLeft["R4_TEXT"] == "FM" and _mpdLeft["R4_BORDERCONTAINER"] then
                _oh58RetranPersist = true
            elseif _mpdLeft["R4_TEXT"] == "FM" and _mpdLeft["R4_BORDERCONTAINER"] == nil then
                _oh58RetranPersist = false
            end
        end

        if _oh58RetranPersist then
            _data.radios[2].rtMode = 0
            _data.radios[5].rtMode = 0
            _data.radios[2].retransmit = true
            _data.radios[5].retransmit = true
            _data.radios[2].rxOnly = true
            _data.radios[5].rxOnly = true
        end
    end
    -- FM RETRAN END

    if _cyclicICSPtt > 0.5 then
        _data.ptt = true
        _data.selected = 0
    end

    if _cyclicPtt > 0.5 then
        _data.ptt = true
    end

    if _footPtt > 0.5 then
        _data.ptt = true
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.3, abType = "oh58d" }
    --
    --       local _door = SR.getButtonPosition(800)
    --
    --        if _door > 0.2 then
    --            _data.ambient = {vol = 0.35,  abType = 'oh58d' }
    --        else
    --            _data.ambient = {vol = 0.2,  abType = 'oh58d' }
    --        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "oh58d" }
    end

    _data.control = 1

    return _data
end

function SR.exportRadioOH6A(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = true, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volMode = 0

    _data.radios[2].name = "AN/ARC-54 VHF FM"
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = SR.getRadioModulation(15)
    _data.radios[2].volMode = 0

    _data.radios[3].name = "AN/ARC-51 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(14)
    _data.radios[3].modulation = SR.getRadioModulation(14)
    _data.radios[3].volMode = 0
    _data.radios[3].model = SR.RadioModels.AN_ARC51

    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()
    local _hotMic = 0
    local _selector = 0

    if _seat == 0 then
        if SR.getButtonPosition(344) > 0.5 then
            _data.radios[1].volume = SR.getRadioVolume(0, 346, { -1.0, 1.0 }, false)
        else
            _data.radios[1].volume = 0
        end

        if SR.getButtonPosition(340) > 0.5 then
            _data.radios[2].volume = SR.getRadioVolume(0, 346, { -1.0, 1.0 }, false)
                * SR.getRadioVolume(0, 51, { 0.0, 1.0 }, false)
        else
            _data.radios[2].volume = 0
        end

        if SR.getButtonPosition(341) > 0.5 then
            _data.radios[3].volume = SR.getRadioVolume(0, 346, { -1.0, 1.0 }, false)
                * SR.getRadioVolume(0, 57, { 0.0, 1.0 }, false)
        else
            _data.radios[3].volume = 0
        end

        _selector = SR.getSelectorPosition(347, 0.165)
    else
        if SR.getButtonPosition(352) > 0.5 then
            _data.radios[1].volume = SR.getRadioVolume(0, 354, { -1.0, 1.0 }, false)
        else
            _data.radios[1].volume = 0
        end

        if SR.getButtonPosition(348) > 0.5 then
            _data.radios[2].volume = SR.getRadioVolume(0, 354, { -1.0, 1.0 }, false)
                * SR.getRadioVolume(0, 51, { 0.0, 1.0 }, false)
        else
            _data.radios[2].volume = 0
        end

        if SR.getButtonPosition(349) > 0.5 then
            _data.radios[3].volume = SR.getRadioVolume(0, 354, { -1.0, 1.0 }, false)
                * SR.getRadioVolume(0, 57, { 0.0, 1.0 }, false)
        else
            _data.radios[3].volume = 0
        end

        -- _hotMic = SR.getSelectorPosition(186, 0.1)
        _selector = SR.getSelectorPosition(355, 0.165)
    end

    if _selector == 2 then
        _data.selected = 0
    elseif _selector == 3 then
        _data.selected = 1
    elseif _selector == 4 then
        _data.selected = 2
    else
        _data.selected = -1
    end

    --guard mode for UHF Radio
    local uhfModeKnob = SR.getSelectorPosition(56, 0.33)
    if uhfModeKnob == 2 and _data.radios[3].freq > 1000 then
        _data.radios[3].secFreq = 243.0 * 1000000
    end

    --guard mode for UHF Radio
    local retran = SR.getSelectorPosition(52, 0.33)

    if retran == 2 and _data.radios[2].freq > 1000 then
        _data.radios[2].rtMode = 0
        _data.radios[2].retransmit = true

        _data.radios[3].rtMode = 0
        _data.radios[3].retransmit = true
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then
            _data.ambient = { vol = 0.35, abType = "oh6a" }
        else
            _data.ambient = { vol = 0.2, abType = "oh6a" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "oh6a" }
    end

    _data.control = 1

    return _data
end

function SR.exportRadioKA50(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    local _panel = GetDevice(0)

    _data.radios[2].name = "R-800L14 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(48)
    _data.radios[2].model = SR.RadioModels.R_800

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
    _data.radios[3].model = SR.RadioModels.R_828

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

    _data.control = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "ka50" }
        else
            _data.ambient = { vol = 0.2, abType = "ka50" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ka50" }
    end

    return _data
end

function SR.exportRadioMI8(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    -- Doesnt work but might as well allow selection
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "R-863"
    _data.radios[2].freq = SR.getRadioFrequency(38)
    _data.radios[2].model = SR.RadioModels.R_863

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
    _data.radios[3].model = SR.RadioModels.JADRO_1A

    _data.radios[4].name = "R-828"
    _data.radios[4].freq = SR.getRadioFrequency(39, 50000)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 737, { 0.0, 1.0 }, false)
    _data.radios[4].model = SR.RadioModels.R_828

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

    _data.control = 1 -- full radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(216)
        local _doorRight = SR.getButtonPosition(215)

        if _doorLeft > 0.2 or _doorRight > 0.2 then
            _data.ambient = { vol = 0.35, abType = "mi8" }
        else
            _data.ambient = { vol = 0.2, abType = "mi8" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mi8" }
    end

    return _data
end

function SR.exportRadioMI24P(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "Use Radio/ICS Switch to control Intercom Hot Mic",
    }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "R-863"
    _data.radios[2].freq = SR.getRadioFrequency(49)
    _data.radios[2].modulation = SR.getRadioModulation(49)
    _data.radios[2].volume = SR.getRadioVolume(0, 511, { 0.0, 1.0 }, false)
    _data.radios[2].volMode = 0
    _data.radios[2].model = SR.RadioModels.R_863

    local guard = SR.getSelectorPosition(507, 1)
    if guard == 1 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    _data.radios[3].name = "R-828"
    _data.radios[3].freq = SR.getRadioFrequency(51)
    _data.radios[3].modulation = 1 --SR.getRadioModulation(50)
    _data.radios[3].volume = SR.getRadioVolume(0, 339, { 0.0, 1.0 }, false)
    _data.radios[3].volMode = 0
    _data.radios[3].model = SR.RadioModels.R_828

    _data.radios[4].name = "JADRO-1I"
    _data.radios[4].freq = SR.getRadioFrequency(50, 500)
    _data.radios[4].modulation = SR.getRadioModulation(50)
    _data.radios[4].volume = SR.getRadioVolume(0, 426, { 0.0, 1.0 }, false)
    _data.radios[4].volMode = 0
    _data.radios[4].model = SR.RadioModels.JADRO_1A

    -- listen only radio - moved to expansion
    _data.radios[5].name = "R-852"
    _data.radios[5].freq = SR.getRadioFrequency(52)
    _data.radios[5].modulation = SR.getRadioModulation(52)
    _data.radios[5].volume = SR.getRadioVolume(0, 517, { 0.0, 1.0 }, false)
    _data.radios[5].volMode = 0
    _data.radios[5].expansion = true
    _data.radios[5].model = SR.RadioModels.R_852

    -- TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    if _seat == 0 then
        _data.radios[1].volume = SR.getRadioVolume(0, 457, { 0.0, 1.0 }, false)

        --Pilot SPU-8 selection
        local _switch = SR.getSelectorPosition(455, 0.2)
        if _switch == 0 then
            _data.selected = 1 -- R-863
        elseif _switch == 1 then
            _data.selected = -1 -- No Function
        elseif _switch == 2 then
            _data.selected = 2 -- R-828
        elseif _switch == 3 then
            _data.selected = 3 -- JADRO
        elseif _switch == 4 then
            _data.selected = 4
        else
            _data.selected = -1
        end

        local _pilotPTT = SR.getButtonPosition(738)
        if _pilotPTT >= 0.1 then
            if _pilotPTT == 0.5 then
                -- intercom
                _data.selected = 0
            end

            _data.ptt = true
        end

        --hot mic
        if SR.getButtonPosition(456) >= 1.0 then
            _data.intercomHotMic = true
        end
    else
        --- copilot
        _data.radios[1].volume = SR.getRadioVolume(0, 661, { 0.0, 1.0 }, false)
        -- For the co-pilot allow volume control
        _data.radios[2].volMode = 1
        _data.radios[3].volMode = 1
        _data.radios[4].volMode = 1
        _data.radios[5].volMode = 1

        local _switch = SR.getSelectorPosition(659, 0.2)
        if _switch == 0 then
            _data.selected = 1 -- R-863
        elseif _switch == 1 then
            _data.selected = -1 -- No Function
        elseif _switch == 2 then
            _data.selected = 2 -- R-828
        elseif _switch == 3 then
            _data.selected = 3 -- JADRO
        elseif _switch == 4 then
            _data.selected = 4
        else
            _data.selected = -1
        end

        local _copilotPTT = SR.getButtonPosition(856)
        if _copilotPTT >= 0.1 then
            if _copilotPTT == 0.5 then
                -- intercom
                _data.selected = 0
            end

            _data.ptt = true
        end

        --hot mic
        if SR.getButtonPosition(660) >= 1.0 then
            _data.intercomHotMic = true
        end
    end

    _data.control = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(9)
        local _doorRight = SR.getButtonPosition(849)

        if _doorLeft > 0.2 or _doorRight > 0.2 then
            _data.ambient = { vol = 0.35, abType = "mi24" }
        else
            _data.ambient = { vol = 0.2, abType = "mi24" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mi24" }
    end

    return _data
end

function SR.exportRadioL39(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = SR.getRadioVolume(0, 288, { 0.0, 0.8 }, false)
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "R-832M"
    _data.radios[2].freq = SR.getRadioFrequency(19)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 289, { 0.0, 0.8 }, false)
    _data.radios[2].model = SR.RadioModels.R_832M

    -- Intercom button depressed
    if SR.getButtonPosition(133) > 0.5 or SR.getButtonPosition(546) > 0.5 then
        _data.selected = 0
        _data.ptt = true
    elseif SR.getButtonPosition(134) > 0.5 or SR.getButtonPosition(547) > 0.5 then
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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 1 -- full radio - for expansion radios - DCS controls must be disabled

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(139)
        local _doorRight = SR.getButtonPosition(140)

        if _doorLeft > 0.2 or _doorRight > 0.2 then
            _data.ambient = { vol = 0.3, abType = "l39" }
        else
            _data.ambient = { vol = 0.2, abType = "l39" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mi24" }
    end

    return _data
end

function SR.exportRadioEagleII(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1 --SR.getRadioVolume(0, 288,{0.0,0.8},false)
    _data.radios[1].model = SR.RadioModels.Intercom

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- HOTAS

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "ChristenEagle" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ChristenEagle" }
    end

    return _data
end

function SR.exportRadioYak52(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "Baklan 5"
    _data.radios[2].freq = SR.getRadioFrequency(27)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 90, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.Baklan_5

    -- Intercom button depressed
    if SR.getButtonPosition(192) > 0.5 or SR.getButtonPosition(196) > 0.5 then
        _data.selected = 1
        _data.ptt = true
    elseif SR.getButtonPosition(194) > 0.5 or SR.getButtonPosition(197) > 0.5 then
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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 1 -- full radio - for expansion radios - DCS controls must be disabled

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "yak52" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "yak52" }
    end

    return _data
end

--for A10C
function SR.exportRadioA10C(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = false,
        desc = "Using cockpit PTT (HOTAS Mic Switch) requires use of VoIP bindings.",
    }

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
    _data.radios[2].volume = SR.getRadioVolume(0, 133, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 225, { 0.0, 1.0 }, false)
        * SR.getButtonPosition(226)
    _data.radios[2].model = SR.RadioModels.AN_ARC186

    -- UHF
    -- Set radio data
    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(54)
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    local modulation = SR.getSelectorPosition(162, 0.1)

    --is HQ selected (A on the Radio)
    if modulation == 2 then
        _data.radios[3].modulation = 4
    else
        _data.radios[3].modulation = 0
    end

    _data.radios[3].volume = SR.getRadioVolume(0, 171, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 227, { 0.0, 1.0 }, false)
        * SR.getButtonPosition(228)
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
    _data.radios[4].volume = SR.getRadioVolume(0, 147, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 223, { 0.0, 1.0 }, false)
        * SR.getButtonPosition(224)
    _data.radios[4].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    -- KY-58 Radio Encryption
    -- Check if encryption is being used
    local _ky58Power = SR.getButtonPosition(784)
    if _ky58Power > 0.5 and SR.getButtonPosition(783) == 0 then
        -- mode switch set to OP and powered on
        -- Power on!

        local _radio = nil
        if SR.round(SR.getButtonPosition(781), 0.1) == 0.2 and SR.getSelectorPosition(149, 0.1) >= 2 then -- encryption disabled when EMER AM/FM selected
            --crad/2 vhf - FM
            _radio = _data.radios[4]
        elseif SR.getButtonPosition(781) == 0 and _selector ~= 2 then -- encryption disabled when GRD selected
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
    -- ED broke this as part of the VoIP work
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

    _data.control = 1 -- Overlay

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(200, 0.1)

    local iffIdent = SR.getButtonPosition(207) -- -1 is off 0 or more is on

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

    local mode1On = SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1) * 100 + SR.round(SR.getButtonPosition(210), 0.1) * 10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000
        + SR.round(SR.getButtonPosition(212), 0.1) * 1000
        + SR.round(SR.getButtonPosition(213), 0.1) * 100
        + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(7)

        if _door > 0.1 then
            _data.ambient = { vol = 0.3, abType = "a10" }
        else
            _data.ambient = { vol = 0.2, abType = "a10" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "a10" }
    end

    -- SR.log("ambient STATUS"..SR.JSON:encode(_data.ambient).."\n\n")
    return _data
end

--for A10C2
local _a10c2 = {}
_a10c2.enc = false
_a10c2.encKey = 1
_a10c2.volume = 1
_a10c2.lastVolPos = 0
_a10c2.increaseVol = false
_a10c2.decreaseVol = false
_a10c2.enableVolumeControl = false

function SR.exportRadioA10C2(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = false,
        desc = "Using cockpit PTT (HOTAS Mic Switch) requires use of VoIP bindings.",
    }

    -- Check if player is in a new aircraft
    if _lastUnitId ~= _data.unitId then
        -- New aircraft; Reset volumes to 100%
        local _device = GetDevice(0)

        if _device then
            --   _device:set_argument_value(133, 1.0) -- VHF AM
            _device:set_argument_value(171, 1.0) -- UHF
            _device:set_argument_value(147, 1.0) -- VHF FM
            _a10c2.enc = false
            _a10c2.encKey = 1
            _a10c2.volume = 1
            _a10c2.increaseVol = false
            _a10c2.decreaseVol = false
            _a10c2.enableVolumeControl = false
        end
    end

    -- VHF AM
    -- Set radio data
    _data.radios[2].name = "AN/ARC-210 VHF/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(55)
    _data.radios[2].modulation = SR.getRadioModulation(55)
    _data.radios[2].encMode = 2 -- Mode 2 is set by aircraft
    _data.radios[2].model = SR.RadioModels.AN_ARC210

    --18 : {"PREV":"PREV","comsec_mode":"KY-58 VOICE","comsec_submode":"CT","dot_mark":".","freq_label_khz":"000","freq_label_mhz":"124","ky_submode_label":"1","lower_left_corner_arc210":"","modulation_label":"AM","prev_manual_freq":"---.---","txt_RT":"RT1"}
    -- 18 : {"PREV":"PREV","comsec_mode":"KY-58 VOICE","comsec_submode":"CT-TD","dot_mark":".","freq_label_khz":"000","freq_label_mhz":"124","ky_submode_label":"4","lower_left_corner_arc210":"","modulation_label":"AM","prev_manual_freq":"---.---","txt_RT":"RT1"}

    pcall(function()
        local _radioDisplay = SR.getListIndicatorValue(18)

        if _radioDisplay["COMSEC"] == "COMSEC" then
            _a10c2.enableVolumeControl = true
        else
            _a10c2.enableVolumeControl = false
        end

        if _radioDisplay.comsec_submode and _radioDisplay.comsec_submode == "PT" then
            _a10c2.encKey = tonumber(_radioDisplay.ky_submode_label)
            _a10c2.enc = false
        elseif
            _radioDisplay.comsec_submode
            and (_radioDisplay.comsec_submode == "CT-TD" or _radioDisplay.comsec_submode == "CT")
        then
            _a10c2.encKey = tonumber(_radioDisplay.ky_submode_label)
            _a10c2.enc = true
        end
    end)

    local _current = SR.getButtonPosition(552)
    local _delta = _a10c2.lastVolPos - _current
    _a10c2.lastVolPos = _current

    if _delta > 0 then
        _a10c2.decreaseVol = true
    elseif _delta < 0 then
        _a10c2.increaseVol = true
    else
        _a10c2.increaseVol = false
        _a10c2.decreaseVol = false
    end

    if _a10c2.enableVolumeControl then
        if _a10c2.increaseVol then
            _a10c2.volume = _a10c2.volume + 0.05
        elseif _a10c2.decreaseVol then
            _a10c2.volume = _a10c2.volume - 0.05
        end

        if _a10c2.volume > 1.0 then
            _a10c2.volume = 1.0
        end

        if _a10c2.volume < 0.0 then
            _a10c2.volume = 0
        end
    end

    _data.radios[2].volume = _a10c2.volume
        * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 225, { 0.0, 1.0 }, false)
        * SR.getButtonPosition(226)
    _data.radios[2].encKey = _a10c2.encKey
    _data.radios[2].enc = _a10c2.enc

    -- CREDIT: Recoil - thank you!
    -- Check ARC-210 function mode (0 = OFF, 1 = TR+G, 2 = TR, 3 = ADF, 4 = CHG PRST, 5 = TEST, 6 = ZERO)
    local arc210ModeKnob = SR.getSelectorPosition(551, 0.1)
    if arc210ModeKnob == 1 and _data.radios[2].freq > 1000 then
        -- Function dial set to TR+G
        -- Listen to Guard as well as designated frequency
        if (_data.radios[2].freq >= (108.0 * 1000000)) and (_data.radios[2].freq < (156.0 * 1000000)) then
            -- Frequency between 108.0 and 156.0 MHz, using VHF Guard
            _data.radios[2].secFreq = 121.5 * 1000000
        else
            -- Other frequency, using UHF Guard
            _data.radios[2].secFreq = 243.0 * 1000000
        end
    else
        -- Function dial set to OFF, TR, ADF, CHG PRST, TEST or ZERO
        -- Not listening to Guard secondarily
        _data.radios[2].secFreq = 0
    end

    -- UHF
    -- Set radio data
    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(54)
    _data.radios[3].model = SR.RadioModels.AN_ARC164

    local modulation = SR.getSelectorPosition(162, 0.1)

    --is HQ selected (A on the Radio)
    if modulation == 2 then
        _data.radios[3].modulation = 4
    else
        _data.radios[3].modulation = 0
    end

    _data.radios[3].volume = SR.getRadioVolume(0, 171, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 227, { 0.0, 1.0 }, false)
        * SR.getButtonPosition(228)
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
    _data.radios[4].volume = SR.getRadioVolume(0, 147, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false)
        * SR.getRadioVolume(0, 223, { 0.0, 1.0 }, false)
        * SR.getButtonPosition(224)
    _data.radios[4].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    -- KY-58 Radio Encryption
    -- Check if encryption is being used
    local _ky58Power = SR.getButtonPosition(784)
    if _ky58Power > 0.5 and SR.getButtonPosition(783) == 0 then
        -- mode switch set to OP and powered on
        -- Power on!

        local _radio = nil
        if SR.round(SR.getButtonPosition(781), 0.1) == 0.2 and SR.getSelectorPosition(149, 0.1) >= 2 then -- encryption disabled when EMER AM/FM selected
            --crad/2 vhf - FM
            _radio = _data.radios[4]
        elseif SR.getButtonPosition(781) == 0 and _selector ~= 2 then -- encryption disabled when GRD selected
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
    -- ED broke this as part of the VoIP work
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

    _data.control = 1 -- Overlay

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(200, 0.1)

    local iffIdent = SR.getButtonPosition(207) -- -1 is off 0 or more is on

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

    local mode1On = SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1) * 100 + SR.round(SR.getButtonPosition(210), 0.1) * 10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000
        + SR.round(SR.getButtonPosition(212), 0.1) * 1000
        + SR.round(SR.getButtonPosition(213), 0.1) * 100
        + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(7)

        if _door > 0.1 then
            _data.ambient = { vol = 0.3, abType = "a10" }
        else
            _data.ambient = { vol = 0.2, abType = "a10" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "a10" }
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
_fa18.iff = {
    status = -1,
    mode1 = -1,
    mode2 = -1,
    mode3 = -1,
    mode4 = true,
    control = 0,
    expansion = false,
}
_fa18.enttries = 0
_fa18.mode3opt = "" -- to distinguish between 3 and 3/C while ED doesn't fix the different codes for those
_fa18.identEnd = 0 -- time to end IFF ident -(18 seconds)

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
]]
--

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
        _fa18.radio1.channel = nil
        _fa18.radio2.guard = 0
        _fa18.radio2.channel = nil
        _fa18.radio3.channel = 127 --127 is disabled for MIDS
        _fa18.radio4.channel = 127
        _fa18.iff = { status = -1, mode1 = -1, mode2 = -1, mode3 = -1, mode4 = true, control = 0, expansion = false }
        _fa18.mode3opt = ""
        _fa18.identEnd = 0
        _fa18.link16 = false
        _fa18.scratchpad = {}
    end

    local getGuardFreq = function(freq, currentGuard, modulation)
        if freq > 1000000 then
            -- check if UFC is currently displaying the GRCV for this radio
            --and change state if so

            if _ufc and _ufc.UFC_OptionDisplay1 == "GRCV" then
                if _ufc.UFC_ScratchPadNumberDisplay then
                    local _ufcFreq = tonumber(_ufc.UFC_ScratchPadNumberDisplay)

                    -- if its the correct radio
                    if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq, 1000) then
                        if _ufc.UFC_OptionCueing1 == ":" then
                            -- GUARD changes based on the tuned frequency
                            if freq > 108 * 1000000 and freq < 135.995 * 1000000 and modulation == 0 then
                                return 121.5 * 1000000
                            end
                            if freq > 108 * 1000000 and freq < 399.975 * 1000000 and modulation == 0 then
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
                if freq > 108 * 1000000 and freq < 135.995 * 1000000 and modulation == 0 then
                    return 121.5 * 1000000
                end
                if freq > 108 * 1000000 and freq < 399.975 * 1000000 and modulation == 0 then
                    return 243 * 1000000
                end
            end

            return currentGuard
        else
            -- reset state
            return 0
        end
    end

    local getCommChannel = function(currentDisplay, memorizedValue)
        local maybeChannel = currentDisplay

        -- Cue, Guard, Manual, Sea - not channels.
        if string.find(maybeChannel, "^[CGMS]$") then
            return nil -- not channels.
        end

        -- ~0 = 20
        if maybeChannel == "~0" then
            maybeChannel = "20"
        else
            -- leading backtick `n -> 1n.
            maybeChannel = string.gsub(maybeChannel, "^`", "1")
        end

        return tonumber(maybeChannel) or memorizedValue
    end

    -- AN/ARC-210 - 1
    -- Set radio data
    local _radio = _data.radios[2]
    _radio.name = "AN/ARC-210 - COMM1"
    _radio.freq = SR.getRadioFrequency(38)
    _radio.modulation = SR.getRadioModulation(38)
    _radio.volume = SR.getRadioVolume(0, 108, { 0.0, 1.0 }, false)
    _radio.model = SR.RadioModels.AN_ARC210
    -- _radio.encMode = 2 -- Mode 2 is set by aircraft

    _fa18.radio1.channel = getCommChannel(_ufc.UFC_Comm1Display, _fa18.radio1.channel)
    _radio.channel = _fa18.radio1.channel
    _fa18.radio1.guard = getGuardFreq(_radio.freq, _fa18.radio1.guard, _radio.modulation)
    _radio.secFreq = _fa18.radio1.guard

    -- AN/ARC-210 - 2
    -- Set radio data
    _radio = _data.radios[3]
    _radio.name = "AN/ARC-210 - COMM2"
    _radio.freq = SR.getRadioFrequency(39)
    _radio.modulation = SR.getRadioModulation(39)
    _radio.volume = SR.getRadioVolume(0, 123, { 0.0, 1.0 }, false)
    _radio.model = SR.RadioModels.AN_ARC210
    -- _radio.encMode = 2 -- Mode 2 is set by aircraft

    _fa18.radio2.channel = getCommChannel(_ufc.UFC_Comm2Display, _fa18.radio2.channel)
    _radio.channel = _fa18.radio2.channel
    _fa18.radio2.guard = getGuardFreq(_radio.freq, _fa18.radio2.guard, _radio.modulation)
    _radio.secFreq = _fa18.radio2.guard

    -- KY-58 Radio Encryption
    local _ky58Power = SR.round(SR.getButtonPosition(447), 0.1)
    local _ky58PoweredOn = _ky58Power == 0.1
    if _ky58PoweredOn and SR.round(SR.getButtonPosition(444), 0.1) == 0.1 then
        -- mode switch set to C and powered on
        -- Power on!

        -- Get encryption key
        local _channel = SR.getSelectorPosition(446, 0.1) + 1
        if _channel > 6 then
            _channel = 6 -- has two other options - lock to 6
        end

        _radio = _data.radios[2 + SR.getSelectorPosition(144, 0.3)]
        _radio.encMode = 2 -- Mode 2 is set by aircraft
        _radio.encKey = _channel
        _radio.enc = true
    end

    -- MIDS

    -- MIDS A
    _radio = _data.radios[4]
    _radio.name = "MIDS A"
    _radio.modulation = 6
    _radio.volume = SR.getRadioVolume(0, 362, { 0.0, 1.0 }, false)
    _radio.encMode = 2 -- Mode 2 is set by aircraft
    _radio.model = SR.RadioModels.LINK16

    local midsAChannel = _fa18.radio3.channel
    if midsAChannel < 127 and _fa18.link16 then
        _radio.freq = SR.MIDS_FREQ + (SR.MIDS_FREQ_SEPARATION * midsAChannel)
        _radio.channel = midsAChannel
    else
        _radio.freq = 1
        _radio.channel = -1
    end

    -- MIDS B
    _radio = _data.radios[5]
    _radio.name = "MIDS B"
    _radio.modulation = 6
    _radio.volume = SR.getRadioVolume(0, 361, { 0.0, 1.0 }, false)
    _radio.encMode = 2 -- Mode 2 is set by aircraft
    _radio.model = SR.RadioModels.LINK16

    local midsBChannel = _fa18.radio4.channel
    if midsBChannel < 127 and _fa18.link16 then
        _radio.freq = SR.MIDS_FREQ + (SR.MIDS_FREQ_SEPARATION * midsBChannel)
        _radio.channel = midsBChannel
    else
        _radio.freq = 1
        _radio.channel = -1
    end

    -- IFF

    -- set initial IFF status based on cold/hot start since it can't be read directly off the panel
    if _fa18.iff.status == -1 then
        local batterySwitch = SR.getButtonPosition(404)

        if batterySwitch == 0 then
            -- cold start, everything off
            _fa18.iff =
                { status = 0, mode1 = -1, mode2 = -1, mode3 = -1, mode4 = false, control = 0, expansion = false }
        else
            -- hot start, M4 on
            _fa18.iff = { status = 1, mode1 = -1, mode2 = -1, mode3 = -1, mode4 = true, control = 0, expansion = false }
        end
    end

    local iff = _fa18.iff

    if _ufc then
        -- Update current state.
        local scratchpadString = _ufc.UFC_ScratchPadString1Display .. _ufc.UFC_ScratchPadString2Display
        if _ufc.UFC_OptionDisplay4 == "VOCA" then
            -- Link16
            _fa18.link16 = scratchpadString == "ON"
        elseif _ufc.UFC_OptionDisplay2 == "2   " then
            -- IFF transponder
            if scratchpadString == "XP" then
                if iff.status <= 0 then
                    iff.status = 1
                end

                -- Update Mode 1
                if _ufc.UFC_OptionCueing1 == ":" then
                    -- 3-bit digit, followed by a 2-bit one, 5-bit total.
                    local code = string.match(_ufc.UFC_OptionDisplay1, "1%-([0-7][0-3])") -- actual code is displayed in the option display
                    if code then
                        iff.mode1 = tonumber(code)
                    end
                else
                    iff.mode1 = -1
                end

                -- Update Mode 2 and 3
                for modeNumber = 2, 3 do
                    local mode = "mode" .. modeNumber
                    if _ufc["UFC_OptionCueing" .. modeNumber] == ":" then
                        local optionDisplay = _ufc["UFC_OptionDisplay" .. modeNumber]
                        if iff[mode] == -1 or _fa18[mode .. "opt"] ~= optionDisplay then -- just turned on
                            local code = string.match(_ufc.UFC_ScratchPadNumberDisplay, modeNumber .. "%-([0-7]+)")
                            if code then
                                iff[mode] = tonumber(code)
                            end
                            _fa18[mode .. "opt"] = optionDisplay
                        end
                    else
                        iff[mode] = -1
                    end
                end

                -- Update Mode 4
                iff.mode4 = _ufc.UFC_OptionCueing4 == ":"
            elseif scratchpadString == "AI" then
                if iff.status <= 0 then
                    iff.status = 1
                end
            else
                iff.status = 0
            end
        end

        -- Process any updates.
        local clrPressed = SR.getButtonPosition(121) > 0
        if not clrPressed then
            local scratchpad = _ufc.UFC_ScratchPadNumberDisplay
            if scratchpad ~= "" then
                local scratchError = scratchpad == "ERROR"
                if _fa18.scratchpad.blanked then
                    _fa18.scratchpad.blanked = false
                    if not scratchError then
                        -- Updated value valid, try and parse based on what's currently required.
                        -- Find what we're updating.
                        if _ufc.UFC_OptionDisplay4 == "VOCA" then
                            -- Link16
                            if scratchpadString == "ON" then
                                -- Link16 ON
                                local targetRadio = nil

                                if _ufc.UFC_OptionCueing4 == ":" then
                                    targetRadio = "radio3"
                                elseif _ufc.UFC_OptionCueing5 == ":" then
                                    targetRadio = "radio4"
                                end

                                if targetRadio then
                                    local channel = tonumber(scratchpad)
                                    if channel then
                                        _fa18[targetRadio].channel = channel
                                    end
                                end
                            end
                        elseif scratchpadString == "XP" then
                            -- IFF
                            local mode, code = string.match(scratchpad, "([23])%-([0-7]+)")
                            if mode and code then
                                _fa18.iff["mode" .. mode] = tonumber(code)
                            end
                            -- Mode 1 is read from the 'cueing' panels (see above)
                        end
                    end
                elseif not scratchError then
                    -- Register that a value is pending confirmation.
                    _fa18.scratchpad.pending = true
                end
            elseif not _fa18.scratchpad.blanked and _fa18.scratchpad.pending then
                -- Hold value until the screen flashes back
                _fa18.scratchpad.blanked = true
            end
        else
            -- CLR pressed, reset scratchpad.
            _fa18.scratchpad = {}
        end
    end

    -- Mode 1/3 IDENT, requires mode 1 or mode 3 to be on and I/P pushbutton press
    if iff.status > 0 then
        if SR.getButtonPosition(99) == 1 and (iff.mode1 ~= -1 or iff.mode3 ~= -1) then
            _fa18.identEnd = LoGetModelTime() + 18
            iff.status = 2
        elseif iff.status == 2 and LoGetModelTime() >= _fa18.identEnd then
            iff.status = 1
        end
    end

    -- set current IFF settings
    _data.iff = _fa18.iff

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.5 then
            _data.ambient = { vol = 0.3, abType = "fa18" }
        else
            _data.ambient = { vol = 0.2, abType = "fa18" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "fa18" }
    end

    -- Relay (RLY):
    local commRelaySwitch = 350 -- 3-pos: PLAIN/OFF/CIPHER.
    local commGuardXmit = 351 -- 3-pos: COMM1/OFF/COMM2.

    -- If relay is not OFF, it creates a 2-way relay between COMM 1 and COMM 2.
    local commRelaySwitchPosition = SR.getButtonPosition(commRelaySwitch)
    if commRelaySwitchPosition ~= 0 then
        local comm1 = 2
        local comm2 = 3

        local spacing = math.abs(_data.radios[comm1].freq - _data.radios[comm2].freq)

        local ky58Desired = commRelaySwitchPosition == 1

        -- we can retransmit if:
        -- * The two radios are at least 10MHz apart.
        -- * IF cipher is requested, KY-58 must be powered on.
        if spacing >= 10e6 and (not ky58Desired or _ky58PoweredOn) then
            -- Apply params on COMM 1 (index 2) and COMM 2 (index 3)
            for commIdx = 2, 3 do
                -- Force in-cockpit
                _data.radios[commIdx].rtMode = 0
                -- Set as relay
                _data.radios[commIdx].retransmit = true
                -- Pilot can no longer transmit on them.
                _data.radios[commIdx].rxOnly = true

                -- Keep encryption only if relaying through the KY-58.
                _data.radios[commIdx].enc = _data.radios[commIdx].enc and ky58Desired
            end
        end
    end

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
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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

        if ded and ded["Receiver Mode"] ~= nil and ded["COM 1 Mode"] == "UHF" then
            if ded["Receiver Mode"] == "BOTH" then
                _f16.radio1.guard = 243.0 * 1000000
            else
                _f16.radio1.guard = 0
            end
        else
            if _data.radios[2].freq < 1000 then
                _f16.radio1.guard = 0
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
    _data.radios[3].model = SR.RadioModels.AN_ARC222

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
            _radio.volume = SR.getRadioVolume(0, 708, { 0.0, 1.0 }, false)
                * SR.getRadioVolume(0, 432, { 0.0, 1.0 }, false) --User KY-58 volume if chiper is used
        end
    end

    local _cipherOnly = SR.round(SR.getButtonPosition(443), 1) < -0.5 --If HOT MIC CIPHER Switch, HOT MIC / OFF / CIPHER set to CIPHER, allow only cipher
    if _cipherOnly and _data.radios[3].enc ~= true then
        _data.radios[3].freq = 0
    end
    if _cipherOnly and _data.radios[2].enc ~= true then
        _data.radios[2].freq = 0
    end

    _data.control = 0 -- SRS Hotas Controls

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(539, 0.1)

    local iffIdent = SR.getButtonPosition(125) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    end

    local modeSelector = SR.getButtonPosition(553)

    if modeSelector == -1 then
        --shares a dial with the mode 3, limit number to max 3
        local _secondDigit = SR.round(SR.getButtonPosition(548), 0.1) * 10

        if _secondDigit > 3 then
            _secondDigit = 3
        end

        _data.iff.mode1 = SR.round(SR.getButtonPosition(546), 0.1) * 100 + _secondDigit
    else
        _data.iff.mode1 = -1
    end

    if modeSelector ~= 0 then
        _data.iff.mode3 = SR.round(SR.getButtonPosition(546), 0.1) * 10000
            + SR.round(SR.getButtonPosition(548), 0.1) * 1000
            + SR.round(SR.getButtonPosition(550), 0.1) * 100
            + SR.round(SR.getButtonPosition(552), 0.1) * 10
    else
        _data.iff.mode3 = -1
    end

    if iffPower == 4 and modeSelector ~= 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(541)

    local mode4Code = SR.getButtonPosition(543)

    if mode4On == 0 and mode4Code ~= -1 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(7)

        if _door > 0.1 then
            _data.ambient = { vol = 0.3, abType = "f16" }
        else
            _data.ambient = { vol = 0.2, abType = "f16" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f16" }
    end

    return _data
end

function SR.exportRadioF86Sabre(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "AN/ARC-27"
    _data.radios[2].freq = SR.getRadioFrequency(26)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 806, { 0.1, 0.9 }, false)
    _data.radios[2].model = SR.RadioModels.AN_ARC27

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- Hotas Controls

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.1 then
            _data.ambient = { vol = 0.3, abType = "f86" }
        else
            _data.ambient = { vol = 0.2, abType = "f86" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f86" }
    end

    return _data
end

function SR.exportRadioMIG15(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "RSI-6K"
    _data.radios[2].freq = SR.getRadioFrequency(30)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 126, { 0.1, 0.9 }, false)
    _data.radios[2].model = SR.RadioModels.RSI_6K

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- Hotas Controls radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(225)

        if _door > 0.3 then
            _data.ambient = { vol = 0.3, abType = "mig15" }
        else
            _data.ambient = { vol = 0.2, abType = "mig15" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mig15" }
    end

    return _data
end

function SR.exportRadioMIG19(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "RSIU-4V"
    _data.radios[2].freq = SR.getRadioFrequency(17)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 327, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.RSI_6K

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- Hotas Controls radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(433)

        if _door > 0.1 then
            _data.ambient = { vol = 0.3, abType = "mig19" }
        else
            _data.ambient = { vol = 0.2, abType = "mig19" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mig19" }
    end

    return _data
end

function SR.exportRadioMIG21(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "R-832"
    _data.radios[2].freq = SR.getRadioFrequency(22)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 210, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.R_832M

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- hotas radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(1)

        if _door > 0.15 then
            _data.ambient = { vol = 0.3, abType = "mig21" }
        else
            _data.ambient = { vol = 0.2, abType = "mig21" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mig21" }
    end

    return _data
end

function SR.exportRadioF5E(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "AN/ARC-164"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].volume = SR.getRadioVolume(0, 309, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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
    if SR.getButtonPosition(135) > 0.5 or (SR.getButtonPosition(131) > 0.5 and SR.getButtonPosition(83) > 0.5) then
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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- hotas radio

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(200, 0.1)

    local iffIdent = SR.getButtonPosition(207) -- -1 is off 0 or more is on

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

    local mode1On = SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1) * 100 + SR.round(SR.getButtonPosition(210), 0.1) * 10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000
        + SR.round(SR.getButtonPosition(212), 0.1) * 1000
        + SR.round(SR.getButtonPosition(213), 0.1) * 100
        + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.1 then
            _data.ambient = { vol = 0.3, abType = "f5" }
        else
            _data.ambient = { vol = 0.2, abType = "f5" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f5" }
    end

    return _data
end

function SR.exportRadioP51(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "SCR522A"
    _data.radios[2].freq = SR.getRadioFrequency(24)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 116, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.SCR522A

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- hotas radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(162)

        if _door > 0.1 then
            _data.ambient = { vol = 0.35, abType = "p51" }
        else
            _data.ambient = { vol = 0.2, abType = "p51" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "p51" }
    end

    return _data
end

function SR.exportRadioP47(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = false,
        dcsRadioSwitch = false,
        intercomHotMic = false,
        desc = "Only one radio by default",
    }

    _data.radios[2].name = "SCR522"
    _data.radios[2].freq = SR.getRadioFrequency(23)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 77, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.SCR522A

    _data.selected = 1

    --Cant find the button in the cockpit?
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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- hotas radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.1 then
            _data.ambient = { vol = 0.35, abType = "p47" }
        else
            _data.ambient = { vol = 0.2, abType = "p47" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "p47" }
    end

    return _data
end

function SR.exportRadioFW190(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FuG 16ZY"
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = 0
    _data.radios[2].model = SR.RadioModels.FUG_16_ZY

    local _volRaw = GetDevice(0):get_argument_value(83)
    if _volRaw >= 0 and _volRaw <= 0.25 then
        _data.radios[2].volume = (1.0 - SR.getRadioVolume(0, 83, { 0, 0.5 }, true)) + 0.5 -- Volume knob is not behaving..
    else
        _data.radios[2].volume = ((1.0 - SR.getRadioVolume(0, 83, { 0, 0.5 }, true)) - 0.5) * -1.0 -- ABS
    end

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- hotas radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(194)

        if _door > 0.1 then
            _data.ambient = { vol = 0.35, abType = "fw190" }
        else
            _data.ambient = { vol = 0.2, abType = "fw190" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "fw190" }
    end

    return _data
end

function SR.exportRadioBF109(_data)
    _data.capabilities = { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FuG 16ZY"
    _data.radios[2].freq = SR.getRadioFrequency(14)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 130, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.FUG_16_ZY

    if (SR.getButtonPosition(150)) > 0.5 then
        _data.ptt = true
    else
        _data.ptt = false
    end

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- hotas radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(95)

        if _door > 0.1 then
            _data.ambient = { vol = 0.35, abType = "bf109" }
        else
            _data.ambient = { vol = 0.2, abType = "bf109" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "bf109" }
    end

    return _data
end

function SR.exportRadioSpitfireLFMkIX(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "A.R.I. 1063" --minimal bug in the ME GUI and in the LUA. SRC5222 is the P-51 radio.
    _data.radios[2].freq = SR.getRadioFrequency(15)
    _data.radios[2].modulation = 0
    _data.radios[2].volMode = 1
    _data.radios[2].volume = 1.0 --no volume control
    _data.radios[2].model = SR.RadioModels.SCR522A

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
    _data.radios[3].model = SR.RadioModels.AN_ARC186

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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0 -- no ptt, same as the FW and 109. No connector.

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(138)

        if _door > 0.1 then
            _data.ambient = { vol = 0.35, abType = "spitfire" }
        else
            _data.ambient = { vol = 0.2, abType = "spitfire" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "spitfire" }
    end

    return _data
end

function SR.exportRadioMosquitoFBMkVI(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "SCR522A"
    _data.radios[2].freq = SR.getRadioFrequency(24)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 364, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.SCR522A

    --TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    if _seat == 0 then
        _data.capabilities =
            { dcsPtt = true, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

        local ptt = SR.getButtonPosition(4)

        if ptt == 1 then
            _data.ptt = true
        end
    else
        _data.capabilities =
            { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    end

    _data.radios[3].name = "R1155"
    _data.radios[3].freq = SR.getRadioFrequency(27, 500, true)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 229, { 0.0, 1.0 }, false)
    _data.radios[3].model = SR.RadioModels.R1155

    _data.radios[4].name = "T1154"
    _data.radios[4].freq = SR.getRadioFrequency(26, 500, true)
    _data.radios[4].modulation = 0
    _data.radios[4].volume = 1
    _data.radios[4].volMode = 1
    _data.radios[4].model = SR.RadioModels.T1154

    -- Expansion Radio - Server Side Controlled
    _data.radios[5].name = "AN/ARC-210"
    _data.radios[5].freq = 124.8 * 1000000
    _data.radios[5].modulation = 0
    _data.radios[5].secFreq = 121.5 * 1000000
    _data.radios[5].volume = 1.0
    _data.radios[5].freqMin = 116 * 1000000
    _data.radios[5].freqMax = 300 * 1000000
    _data.radios[5].volMode = 1
    _data.radios[5].freqMode = 1
    _data.radios[5].expansion = true
    _data.radios[5].model = SR.RadioModels.AN_ARC210

    _data.control = 0 -- no ptt, same as the FW and 109. No connector.
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(250)
        local _doorRight = SR.getButtonPosition(252)

        if _doorLeft > 0.7 or _doorRight > 0.7 then
            _data.ambient = { vol = 0.35, abType = "mosquito" }
        else
            _data.ambient = { vol = 0.2, abType = "mosquito" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "mosquito" }
    end

    return _data
end

function SR.exportRadioC101EB(_data)
    _data.capabilities = {
        dcsPtt = false,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "Pull the HOT MIC breaker up to enable HOT MIC",
    }

    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = SR.getRadioVolume(0, 403, { 0.0, 1.0 }, false)
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-164 UHF"
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 234, { 0.0, 1.0 }, false)
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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
    _data.radios[3].model = SR.RadioModels.AN_ARC134

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

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(347, 0.25)

    -- SR.log("IFF iffPower"..iffPower.."\n\n")

    local iffIdent = SR.getButtonPosition(361) -- -1 is off 0 or more is on

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

    local mode1On = SR.getButtonPosition(349)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(355), 0.1) * 100 + SR.round(SR.getButtonPosition(356), 0.1) * 10

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(351)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(357), 0.1) * 10000
        + SR.round(SR.getButtonPosition(358), 0.1) * 1000
        + SR.round(SR.getButtonPosition(359), 0.1) * 100
        + SR.round(SR.getButtonPosition(360), 0.1) * 10

    if mode3On == 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(354)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end
    _data.control = 1 -- full radio

    local frontHotMic = SR.getButtonPosition(287)
    local rearHotMic = SR.getButtonPosition(891)
    -- only if The hot mic talk button (labeled TALK in cockpit) is up
    if frontHotMic == 1 or rearHotMic == 1 then
        _data.intercomHotMic = true
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(1)
        local _doorRight = SR.getButtonPosition(301)

        if _doorLeft > 0.7 or _doorRight > 0.7 then
            _data.ambient = { vol = 0.3, abType = "c101" }
        else
            _data.ambient = { vol = 0.2, abType = "c101" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "c101" }
    end

    return _data
end

function SR.exportRadioC101CC(_data)
    _data.capabilities = {
        dcsPtt = false,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "The hot mic talk button (labeled TALK in cockpit) must be pulled out",
    }

    -- TODO - figure out channels.... it saves state??
    -- figure out volume
    _data.radios[1].name = "INTERCOM"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = SR.getRadioVolume(0, 403, { 0.0, 1.0 }, false)
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "V/TVU-740"
    _data.radios[2].freq = SR.getRadioFrequency(11)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = 1.0 --SR.getRadioVolume(0, 234,{0.0,1.0},false)
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

    _data.radios[3].name = "20B VHF"
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

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(347, 0.25)

    --SR.log("IFF iffPower"..iffPower.."\n\n")

    local iffIdent = SR.getButtonPosition(361) -- -1 is off 0 or more is on

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

    local mode1On = SR.getButtonPosition(349)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(355), 0.1) * 100 + SR.round(SR.getButtonPosition(356), 0.1) * 10

    if mode1On == 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(351)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(357), 0.1) * 10000
        + SR.round(SR.getButtonPosition(358), 0.1) * 1000
        + SR.round(SR.getButtonPosition(359), 0.1) * 100
        + SR.round(SR.getButtonPosition(360), 0.1) * 10

    if mode3On == 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 0 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(354)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    _data.control = 1

    local frontHotMic = SR.getButtonPosition(287)
    local rearHotMic = SR.getButtonPosition(891)
    -- only if The hot mic talk button (labeled TALK in cockpit) is up
    if frontHotMic == 1 or rearHotMic == 1 then
        _data.intercomHotMic = true
    end

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(1)
        local _doorRight = SR.getButtonPosition(301)

        if _doorLeft > 0.7 or _doorRight > 0.7 then
            _data.ambient = { vol = 0.3, abType = "c101" }
        else
            _data.ambient = { vol = 0.2, abType = "c101" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "c101" }
    end

    return _data
end

function SR.exportRadioMB339A(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = false,
        desc = "To enable the Intercom HotMic pull the INT knob located on ICS",
    }

    local main_panel = GetDevice(0)

    local intercom_device_id = main_panel:get_srs_device_id(0)
    local comm1_device_id = main_panel:get_srs_device_id(1)
    local comm2_device_id = main_panel:get_srs_device_id(2)
    local iff_device_id = main_panel:get_srs_device_id(3)

    local ARC150_device = GetDevice(comm1_device_id)
    local SRT_651_device = GetDevice(comm2_device_id)
    local intercom_device = GetDevice(intercom_device_id)
    local iff_device = GetDevice(iff_device_id)

    -- Intercom Function
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = intercom_device:get_volume()
    _data.radios[1].model = SR.RadioModels.Intercom

    -- AN/ARC-150(V)2 - COMM1 Radio
    _data.radios[2].name = "AN/ARC-150(V)2 - UHF COMM1"
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.975 * 1000000
    _data.radios[2].freq = ARC150_device:is_on() and SR.round(ARC150_device:get_frequency(), 5000) or 0
    _data.radios[2].secFreq = ARC150_device:is_on_guard() and 243.0 * 1000000 or 0
    _data.radios[2].modulation = ARC150_device:get_modulation()
    _data.radios[2].volume = ARC150_device:get_volume()

    -- SRT-651/N - COMM2 Radio
    _data.radios[3].name = "SRT-651/N - V/UHF COMM2"
    _data.radios[3].freqMin = 30 * 1000000
    _data.radios[3].freqMax = 399.975 * 1000000
    _data.radios[3].freq = SRT_651_device:is_on() and SR.round(SRT_651_device:get_frequency(), 5000) or 0
    _data.radios[3].secFreq = SRT_651_device:is_on_guard() and 243.0 * 1000000 or 0
    _data.radios[3].modulation = SRT_651_device:get_modulation()
    _data.radios[3].volume = SRT_651_device:get_volume()

    _data.intercomHotMic = intercom_device:is_hot_mic()

    if intercom_device:is_ptt_pressed() then
        _data.selected = 0
        _data.ptt = true
    elseif ARC150_device:is_ptt_pressed() then
        _data.selected = 1
        _data.ptt = true
    elseif SRT_651_device:is_ptt_pressed() then
        _data.selected = 2
        _data.ptt = true
    else
        _data.ptt = false
    end

    _data.control = 1 -- enables complete radio control

    -- IFF status depend on ident switch as well
    local iff_status
    if iff_device:is_identing() then
        iff_status = 2 -- IDENT
    elseif iff_device:is_working() then
        iff_status = 1 -- NORMAL
    else
        iff_status = 0 -- OFF
    end

    -- IFF trasponder
    _data.iff = {
        status = iff_status,
        mode1 = iff_device:get_mode1_code(),
        mode2 = -1,
        mode3 = iff_device:get_mode3_code(),
        -- Mode 4 - not available in real MB-339 but we have decided to include it for gameplay
        mode4 = iff_device:is_mode4_working(),
        control = 0,
        expansion = false,
    }

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "MB339" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "MB339" }
    end

    return _data
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
    _data.radios[2].model = SR.RadioModels.AN_ARC164

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

    _data.control = 1 -- full radio

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        _data.ambient = { vol = 0.2, abType = "jet" }
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "jet" }
    end

    return _data
end

local _mirageEncStatus = false
local _previousEncState = 0

function SR.exportRadioM2000C(_data)
    local RED_devid = 20
    local GREEN_devid = 19
    local RED_device = GetDevice(RED_devid)
    local GREEN_device = GetDevice(GREEN_devid)

    local has_cockpit_ptt = false

    local RED_ptt = false
    local GREEN_ptt = false
    local GREEN_guard = 0

    pcall(function()
        RED_ptt = RED_device:is_ptt_pressed()
        GREEN_ptt = GREEN_device:is_ptt_pressed()
        has_cockpit_ptt = true
    end)

    pcall(function()
        GREEN_guard = tonumber(GREEN_device:guard_standby_freq())
    end)

    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }
    _data.control = 0

    -- Different PTT/select control if the module version supports cockpit PTT
    if has_cockpit_ptt then
        _data.control = 1
        _data.capabilities.dcsPtt = true
        _data.capabilities.dcsRadioSwitch = true
        if GREEN_ptt then
            _data.selected = 1 -- radios[2] GREEN V/UHF
            _data.ptt = true
        elseif RED_ptt then
            _data.selected = 2 -- radios[3] RED UHF
            _data.ptt = true
        else
            _data.selected = -1
            _data.ptt = false
        end
    end

    _data.radios[2].name = "TRT ERA 7000 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(19)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 707, { 0.0, 1.0 }, false)

    --guard mode for V/UHF Radio
    if GREEN_guard > 0 then
        _data.radios[2].secFreq = GREEN_guard
    end

    -- get channel selector
    local _selector = SR.getSelectorPosition(448, 0.50)

    if _selector == 1 then
        _data.radios[2].channel = SR.getSelectorPosition(445, 0.05) --add 1 as channel 0 is channel 1
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

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local _iffDevice = GetDevice(42)

    if _iffDevice:hasPower() then
        _data.iff.status = 1 -- NORMAL

        if _iffDevice:isIdentActive() then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    else
        _data.iff.status = -1
    end

    if _iffDevice:isModeActive(4) then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if _iffDevice:isModeActive(3) then
        _data.iff.mode3 = tonumber(_iffDevice:getModeCode(3))
    else
        _data.iff.mode3 = -1
    end

    if _iffDevice:isModeActive(2) then
        _data.iff.mode2 = tonumber(_iffDevice:getModeCode(2))
    else
        _data.iff.mode2 = -1
    end

    if _iffDevice:isModeActive(1) then
        _data.iff.mode1 = tonumber(_iffDevice:getModeCode(1))
    else
        _data.iff.mode1 = -1
    end

    --  SR.log(JSON:encode(_data.iff)..'\n\n')

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.3 then
            _data.ambient = { vol = 0.3, abType = "m2000" }
        else
            _data.ambient = { vol = 0.2, abType = "m2000" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "m2000" }
    end

    return _data
end

function SR.exportRadioF1CE(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "TRAP-136 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(6)
    _data.radios[2].modulation = 0
    _data.radios[2].volume = SR.getRadioVolume(0, 311, { 0.0, 1.0 }, false)
    _data.radios[2].volMode = 0

    if SR.getSelectorPosition(280, 0.2) == 0 and _data.radios[2].freq > 1000 then
        _data.radios[2].secFreq = 121.5 * 1000000
    end

    if SR.getSelectorPosition(282, 0.5) == 1 then
        _data.radios[2].channel = SR.getNonStandardSpinner(
            283,
            {
                [0.000] = 1,
                [0.050] = 2,
                [0.100] = 3,
                [0.150] = 4,
                [0.200] = 5,
                [0.250] = 6,
                [0.300] = 7,
                [0.350] = 8,
                [0.400] = 9,
                [0.450] = 10,
                [0.500] = 11,
                [0.550] = 12,
                [0.600] = 13,
                [0.650] = 14,
                [0.700] = 15,
                [0.750] = 16,
                [0.800] = 17,
                [0.850] = 18,
                [0.900] = 19,
                [0.950] = 20,
            },
            0.05,
            3
        )
    end

    _data.radios[3].name = "TRAP-137B UHF"
    _data.radios[3].freq = SR.getRadioFrequency(8)
    _data.radios[3].modulation = 0
    _data.radios[3].volume = SR.getRadioVolume(0, 314, { 0.0, 1.0 }, false)
    _data.radios[3].volMode = 0
    _data.radios[3].channel = SR.getNonStandardSpinner(
        348,
        {
            [0.000] = 1,
            [0.050] = 2,
            [0.100] = 3,
            [0.150] = 4,
            [0.200] = 5,
            [0.250] = 6,
            [0.300] = 7,
            [0.350] = 8,
            [0.400] = 9,
            [0.450] = 10,
            [0.500] = 11,
            [0.550] = 12,
            [0.600] = 13,
            [0.650] = 14,
            [0.700] = 15,
            [0.750] = 16,
            [0.800] = 17,
            [0.850] = 18,
            [0.900] = 19,
            [0.950] = 20,
        },
        0.05,
        3
    )

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local _iffDevice = GetDevice(7)

    if _iffDevice:hasPower() then
        _data.iff.status = 1 -- NORMAL

        if _iffDevice:isIdentActive() then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    else
        _data.iff.status = -1
    end

    if _iffDevice:isModeActive(4) then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if _iffDevice:isModeActive(3) then
        _data.iff.mode3 = tonumber(_iffDevice:getModeCode(3))
    else
        _data.iff.mode3 = -1
    end

    if _iffDevice:isModeActive(2) then
        _data.iff.mode2 = tonumber(_iffDevice:getModeCode(2))
    else
        _data.iff.mode2 = -1
    end

    if _iffDevice:isModeActive(1) then
        _data.iff.mode1 = tonumber(_iffDevice:getModeCode(1))
    else
        _data.iff.mode1 = -1
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-210"
    _data.radios[4].freq = 251.0 * 1000000 --10-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 110 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
    _data.radios[4].model = SR.RadioModels.AN_ARC210

    _data.control = 0

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(1)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "f1" }
        else
            _data.ambient = { vol = 0.2, abType = "f1" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f1" }
    end

    return _data
end

function SR.exportRadioF1BE(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    -- Intercom Function
    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100
    _data.radios[1].modulation = 2
    _data.radios[1].volume = 1.0
    _data.radios[1].volMode = 1.0
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "TRAP-136 V/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(6)
    _data.radios[2].modulation = 0
    _data.radios[2].volMode = 0

    _data.radios[3].name = "TRAP-137B UHF"
    _data.radios[3].freq = SR.getRadioFrequency(8)
    _data.radios[3].modulation = 0
    _data.radios[3].volMode = 0
    _data.radios[3].channel = SR.getNonStandardSpinner(
        348,
        {
            [0.000] = 1,
            [0.050] = 2,
            [0.100] = 3,
            [0.150] = 4,
            [0.200] = 5,
            [0.250] = 6,
            [0.300] = 7,
            [0.350] = 8,
            [0.400] = 9,
            [0.450] = 10,
            [0.500] = 11,
            [0.550] = 12,
            [0.600] = 13,
            [0.650] = 14,
            [0.700] = 15,
            [0.750] = 16,
            [0.800] = 17,
            [0.850] = 18,
            [0.900] = 19,
            [0.950] = 20,
        },
        0.05,
        3
    )

    _data.iff = { status = 0, mode1 = 0, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(739, 0.1)

    local iffIdent = SR.getButtonPosition(744) -- -1 is off 0 or more is on

    if iffPower >= 7 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    end

    local mode1On = SR.getButtonPosition(750)

    local _lookupTable = {
        [0.000] = "0",
        [0.125] = "1",
        [0.250] = "2",
        [0.375] = "3",
        [0.500] = "4",
        [0.625] = "5",
        [0.750] = "6",
        [0.875] = "7",
        [1.000] = "0",
    }
    _data.iff.mode1 = tonumber(
        SR.getNonStandardSpinner(732, _lookupTable, 0.125, 3)
            .. SR.getNonStandardSpinner(
                733,
                {
                    [0.000] = "0",
                    [0.125] = "1",
                    [0.250] = "2",
                    [0.375] = "3",
                    [0.500] = "0",
                    [0.625] = "1",
                    [0.750] = "2",
                    [0.875] = "3",
                    [1.000] = "0",
                },
                0.125,
                3
            )
    )

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(752)

    _data.iff.mode3 = tonumber(
        SR.getNonStandardSpinner(734, _lookupTable, 0.125, 3)
            .. SR.getNonStandardSpinner(735, _lookupTable, 0.125, 3)
            .. SR.getNonStandardSpinner(736, _lookupTable, 0.125, 3)
            .. SR.getNonStandardSpinner(737, _lookupTable, 0.125, 3)
    )

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 10 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(745)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    -- Expansion Radio - Server Side Controlled
    _data.radios[4].name = "AN/ARC-210"
    _data.radios[4].freq = 251.0 * 1000000 --10-399.975 MHZ
    _data.radios[4].modulation = 0
    _data.radios[4].secFreq = 243.0 * 1000000
    _data.radios[4].volume = 1.0
    _data.radios[4].freqMin = 110 * 1000000
    _data.radios[4].freqMax = 399.975 * 1000000
    _data.radios[4].volMode = 1
    _data.radios[4].freqMode = 1
    _data.radios[4].expansion = true
    _data.radios[4].encKey = 1
    _data.radios[4].encMode = 1 -- FC3 Gui Toggle + Gui Enc key setting
    _data.radios[4].model = SR.RadioModels.AN_ARC210

    -- SR.lastKnownSeat = 1

    if SR.lastKnownSeat == 0 then
        _data.radios[2].volume = SR.getRadioVolume(0, 311, { 0.0, 1.0 }, false)

        if SR.getSelectorPosition(280, 0.2) == 0 and _data.radios[2].freq > 1000 then
            _data.radios[2].secFreq = 121.5 * 1000000
        end

        if SR.getSelectorPosition(282, 0.5) == 1 then
            _data.radios[2].channel = SR.getNonStandardSpinner(
                283,
                {
                    [0.000] = 1,
                    [0.050] = 2,
                    [0.100] = 3,
                    [0.150] = 4,
                    [0.200] = 5,
                    [0.250] = 6,
                    [0.300] = 7,
                    [0.350] = 8,
                    [0.400] = 9,
                    [0.450] = 10,
                    [0.500] = 11,
                    [0.550] = 12,
                    [0.600] = 13,
                    [0.650] = 14,
                    [0.700] = 15,
                    [0.750] = 16,
                    [0.800] = 17,
                    [0.850] = 18,
                    [0.900] = 19,
                    [0.950] = 20,
                },
                0.05,
                3
            )
        end

        _data.radios[3].volume = SR.getRadioVolume(0, 314, { 0.0, 1.0 }, false)
    else
        _data.radios[2].volume = SR.getRadioVolume(0, 327, { 0.0, 1.0 }, false)

        if SR.getSelectorPosition(298, 0.2) == 0 and _data.radios[2].freq > 1000 then
            _data.radios[2].secFreq = 121.5 * 1000000
        end

        if SR.getSelectorPosition(300, 0.5) == 1 then
            _data.radios[2].channel = SR.getNonStandardSpinner(
                303,
                {
                    [0.000] = 1,
                    [0.050] = 2,
                    [0.100] = 3,
                    [0.150] = 4,
                    [0.200] = 5,
                    [0.250] = 6,
                    [0.300] = 7,
                    [0.350] = 8,
                    [0.400] = 9,
                    [0.450] = 10,
                    [0.500] = 11,
                    [0.550] = 12,
                    [0.600] = 13,
                    [0.650] = 14,
                    [0.700] = 15,
                    [0.750] = 16,
                    [0.800] = 17,
                    [0.850] = 18,
                    [0.900] = 19,
                    [0.950] = 20,
                },
                0.05,
                3
            )
        end

        _data.radios[3].volume = SR.getRadioVolume(0, 330, { 0.0, 1.0 }, false)
    end

    _data.control = 0

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _doorLeft = SR.getButtonPosition(1)
        local _doorRight = SR.getButtonPosition(6)

        if _doorLeft > 0.2 or _doorRight > 0.2 then
            _data.ambient = { vol = 0.3, abType = "f1" }
        else
            _data.ambient = { vol = 0.2, abType = "f1" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f1" }
    end

    return _data
end

local _jf17 = nil
function SR.exportRadioJF17(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = true, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    -- reset state on aircraft switch
    if _lastUnitId ~= _data.unitId or not _jf17 then
        _jf17 = {
            radios = {
                [2] = {
                    channel = 1,
                    deviceId = 25,
                    volumeKnobId = 934,
                    enc = false,
                    guard = false,
                },
                [3] = {
                    channel = 1,
                    deviceId = 26,
                    volumeKnobId = 938,
                    enc = false,
                    guard = false,
                },
            },
        }
    end

    -- Read ufcp lines.
    local ufcp = {}

    for line = 3, 6 do
        ufcp[#ufcp + 1] = SR.getListIndicatorValue(line)["txt_win" .. (line - 2)]
    end

    -- Check the last line to see if we're editing a radio (and which one!)
    -- Looking for "123   ." (editing left radio) or ".   123" (right radio)
    local displayedRadio = nil

    -- Most likely case - radio channels being displayed.
    local comm1Channel, comm2Channel = string.match(ufcp[#ufcp], "^(%d%d%d)%s+(%d%d%d)$")
    comm1Channel = tonumber(comm1Channel)
    comm2Channel = tonumber(comm2Channel)
    if comm1Channel == nil or comm2Channel == nil then
        -- Check if we have a radio page up.
        local commDot = nil
        comm1Channel, commDot = string.match(ufcp[#ufcp], "^(%d%d%d)%s+(%.)$")
        comm1Channel = tonumber(comm1Channel)
        if comm1Channel ~= nil and commDot ~= nil then
            -- COMM1 being showed on the UFCP.
            displayedRadio = _jf17.radios[2]
        else
            commDot, comm2Channel = string.match(ufcp[#ufcp], "^(%.)%s+(%d%d%d)$")
            comm2Channel = tonumber(comm2Channel)
            if commDot ~= nil and comm2Channel ~= nil then
                -- COMM2 showed on the UFCP.
                displayedRadio = _jf17.radios[3]
            end
        end
    end

    -- Update channels if we have the info.
    if comm1Channel ~= nil then
        _jf17.radios[2].channel = comm1Channel
    end
    if comm2Channel ~= nil then
        _jf17.radios[3].channel = comm2Channel
    end

    if displayedRadio then
        -- Line 1: encryption.
        -- Treat CMS as fixed frequency encryption only,
        -- TRS as HAVEQUICK (frequency hopping) + encryption.
        -- For encryption, use Line 3 MAST/SLAV to change encryption key.
        if string.match(ufcp[1], "^PLN") then
            displayedRadio.enc = false
            displayedRadio.encKey = nil
            displayedRadio.modulation = nil
        elseif string.match(ufcp[1], "^CMS") then
            displayedRadio.enc = true
            displayedRadio.encKey = string.match(ufcp[3], "MAST$") and 2 or 1
            displayedRadio.modulation = nil
        elseif string.match(ufcp[1], "^TRS") then
            displayedRadio.enc = true
            displayedRadio.encKey = string.match(ufcp[3], "MAST$") and 4 or 3
            -- treat as HAVEQUICK
            displayedRadio.modulation = 4
        elseif string.match(ufcp[1], "^DATA") then
            displayedRadio.enc = false
            displayedRadio.encKey = nil
            -- Forcibly set to DISABLED - Datalink has the radio, can't talk on it!
            displayedRadio.modulation = 3
        end

        -- Look at line 2 for RT+G.
        displayedRadio.guard = string.match(ufcp[2], "^RT%+G%s+") ~= nil
    end

    for radioId = 2, 3 do
        local state = _jf17.radios[radioId]
        local dataRadio = _data.radios[radioId]
        dataRadio.name = "R&S M3AR COMM" .. (radioId - 1)
        dataRadio.freq = SR.getRadioFrequency(state.deviceId)
        dataRadio.modulation = state.modulation or SR.getRadioModulation(state.deviceId)
        dataRadio.volume = SR.getRadioVolume(0, state.volumeKnobId, { 0.0, 1.0 }, false)
        dataRadio.encMode = 2 -- Controlled by aircraft.
        dataRadio.channel = state.channel

        -- NOTE: Used to be GetDevice(state.deviceId):get_guard_plus_freq(), but that seems borked.
        if state.guard then
            -- Figure out if we want VHF or UHF guard based on current freq.
            dataRadio.secFreq = dataRadio.freq < 224e6 and 121.5e6 or 243e6
        end
        dataRadio.enc = state.enc
        dataRadio.encKey = state.encKey
    end

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
    _data.control = 0 -- partial radio, allows hotkeys

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

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

    _data.iff.mode4 = _iff:is_m6_trs_on()

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(181)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "jf17" }
        else
            _data.ambient = { vol = 0.2, abType = "jf17" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "jf17" }
    end

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

    local getGuardFreq = function(freq, currentGuard)
        if freq > 1000000 then
            -- check if LEFT UFC is currently displaying the TR-G for this radio
            --and change state if so

            if _ufcScratch and _ufc and _ufcScratch.ufc_right_position then
                local _ufcFreq = tonumber(_ufcScratch.ufc_right_position)

                if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq, 1000) then
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

    local getEncryption = function(freq, currentEnc, currentEncKey)
        if freq > 1000000 then
            -- check if LEFT UFC is currently displaying the encryption for this radio

            if _ufcScratch and _ufcScratch and _ufcScratch.ufc_right_position then
                local _ufcFreq = tonumber(_ufcScratch.ufc_right_position)

                if _ufcFreq and _ufcFreq * 1000000 == SR.round(freq, 1000) then
                    if _ufc.ODU_Option_4_Text == "CIPH" then
                        -- validate number
                        -- ODU_Option_5_Text
                        if string.find(_ufc.ODU_Option_5_Text, "CD", 1, true) then
                            local cduStr = string.gsub(_ufc.ODU_Option_5_Text, "CD ", ""):gsub("^%s*(.-)%s*$", "%1")

                            --remove CD and trim
                            local encNum = tonumber(cduStr)

                            if encNum and encNum > 0 then
                                return true, encNum
                            else
                                return false, -1
                            end
                        else
                            return false, -1
                        end
                    else
                        return false, -1
                    end
                end
            end

            return currentEnc, currentEncKey
        else
            -- reset state
            return false, -1
        end
    end

    _data.radios[2].name = "ARC-210 - COMM1"
    _data.radios[2].freq = SR.getRadioFrequency(2)
    _data.radios[2].modulation = SR.getRadioModulation(2)
    _data.radios[2].volume = SR.getRadioVolume(0, 298, { 0.0, 1.0 }, false)
    _data.radios[2].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[2].model = SR.RadioModels.AN_ARC210

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

    _data.radios[3].name = "ARC-210 - COMM2"
    _data.radios[3].freq = SR.getRadioFrequency(3)
    _data.radios[3].modulation = SR.getRadioModulation(3)
    _data.radios[3].volume = SR.getRadioVolume(0, 299, { 0.0, 1.0 }, false)
    _data.radios[3].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[3].model = SR.RadioModels.AN_ARC210

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
    _data.control = 0 -- partial radio, allows hotkeys

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(38)

        if _door > 0.2 then
            _data.ambient = { vol = 0.35, abType = "av8bna" }
        else
            _data.ambient = { vol = 0.2, abType = "av8bna" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "av8bna" }
    end

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
    _data.radios[1].model = SR.RadioModels.Intercom

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
    _data.radios[3].model = SR.RadioModels.AN_ARC182
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

    --TODO check
    local _seat = SR.lastKnownSeat --get_param_handle("SEAT"):get()

    --   RADIO_ICS_Func_RIO = 402,
    --   RADIO_ICS_Func_Pilot = 2044,

    local _hotMic = false
    if _seat == 0 then
        if SR.getButtonPosition(2044) > -1 then
            _hotMic = true
        end
    else
        if SR.getButtonPosition(402) > -1 then
            _hotMic = true
        end
    end

    _data.intercomHotMic = _hotMic

    if ARC182_ptt then
        _data.selected = 2 -- radios[3] ARC-182
        _data.ptt = true
    elseif ARC159_ptt then
        _data.selected = 1 -- radios[2] ARC-159
        _data.ptt = true
    elseif intercom_transmit and not _hotMic then
        -- CHECK ICS Function Selector
        -- If not set to HOT MIC - switch radios and PTT
        -- if set to hot mic - dont switch and ignore

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
            _data.radios[2].simul = true
            _data.radios[3].simul = true
        end
    end

    _data.control = 1 -- full radio

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iffPower = SR.getSelectorPosition(184, 0.25)

    local iffIdent = SR.getButtonPosition(167)

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

    local mode1On = SR.getButtonPosition(162)
    _data.iff.mode1 = SR.round(SR.getSelectorPosition(201, 0.11111), 0.1) * 10
        + SR.round(SR.getSelectorPosition(200, 0.11111), 0.1)

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On = SR.getButtonPosition(164)
    _data.iff.mode3 = SR.round(SR.getSelectorPosition(199, 0.11111), 0.1) * 1000
        + SR.round(SR.getSelectorPosition(198, 0.11111), 0.1) * 100
        + SR.round(SR.getSelectorPosition(2261, 0.11111), 0.1) * 10
        + SR.round(SR.getSelectorPosition(2262, 0.11111), 0.1)

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On = SR.getButtonPosition(181)

    if mode4On == 0 then
        _data.iff.mode4 = false
    else
        _data.iff.mode4 = true
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(403)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "f14" }
        else
            _data.ambient = { vol = 0.2, abType = "f14" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f14" }
    end

    return _data
end

--for F-4
function SR.exportRadioF4(_data)
    _data.capabilities = {
        dcsPtt = true,
        dcsIFF = true,
        dcsRadioSwitch = true,
        intercomHotMic = true,
        desc = "Expansion Radio requires Always allow SRS Hotkeys on. 2nd radio is receive only",
    }

    local ics_devid = 2
    local arc164_devid = 3
    local iff_devid = 4

    local ICS_device = GetDevice(ics_devid)
    local ARC164_device = GetDevice(arc164_devid)
    local IFF_device = GetDevice(iff_devid)

    local intercom_hot_mic = ICS_device:intercom_transmit()
    local ARC164_ptt = ARC164_device:is_ptt_pressed()
    local radio_modulation = ARC164_device:get_modulation()
    local ky28_key = ICS_device:get_ky28_key()
    local is_encrypted = ICS_device:is_arc164_encrypted()

    _data.radios[1].name = "Intercom"
    _data.radios[1].freq = 100.0
    _data.radios[1].modulation = 2 --Special intercom modulation
    _data.radios[1].volume = ICS_device:get_volume()
    _data.radios[1].model = SR.RadioModels.Intercom

    _data.radios[2].name = "AN/ARC-164 COMM"
    _data.radios[2].freq = ARC164_device:is_on() and SR.round(ARC164_device:get_frequency(), 5000) or 1
    _data.radios[2].modulation = radio_modulation
    _data.radios[2].volume = ARC164_device:get_volume()
    _data.radios[2].model = SR.RadioModels.AN_ARC164
    if ARC164_device:is_guard_enabled() then
        _data.radios[2].secFreq = 243.0 * 1000000
    else
        _data.radios[2].secFreq = 0
    end
    _data.radios[2].freqMin = 225 * 1000000
    _data.radios[2].freqMax = 399.950 * 1000000
    _data.radios[2].encKey = ky28_key
    _data.radios[2].enc = is_encrypted
    _data.radios[2].encMode = 2

    -- RECEIVE ONLY RADIO  https://f4.manuals.heatblur.se/systems/nav_com/uhf.html
    _data.radios[3].name = "AN/ARC-164 AUX"
    _data.radios[3].freq = ARC164_device:is_aux_on() and SR.round(ARC164_device:get_aux_frequency(), 5000) or 1
    _data.radios[3].modulation = radio_modulation
    _data.radios[3].volume = ARC164_device:get_aux_volume()
    _data.radios[3].secFreq = 0
    _data.radios[3].freqMin = 265 * 1000000
    _data.radios[3].freqMax = 284.9 * 1000000
    _data.radios[3].encKey = ky28_key
    _data.radios[3].enc = is_encrypted
    _data.radios[3].encMode = 2
    _data.radios[3].rxOnly = true
    _data.radios[3].model = SR.RadioModels.AN_ARC164

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
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    _data.intercomHotMic = intercom_hot_mic

    if ARC164_ptt then
        _data.selected = 1 -- radios[2] ARC-164
        _data.ptt = true
    else
        _data.selected = -1
        _data.ptt = false
    end

    _data.control = 1 -- full radio

    -- Handle transponder

    _data.iff = { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 0, expansion = false }

    local iff_power = IFF_device:get_is_on()
    local iff_ident = IFF_device:get_ident()

    if iff_power then
        _data.iff.status = 1 -- NORMAL

        if iff_ident then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end
    else
        _data.iff.status = -1
    end

    _data.iff.mode1 = IFF_device:get_mode1()
    _data.iff.mode2 = IFF_device:get_mode2()
    _data.iff.mode3 = IFF_device:get_mode3()
    _data.iff.mode4 = IFF_device:get_mode4_is_on()

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on
        -- Pilot_Canopy = 87,
        local _door = SR.getButtonPosition(87)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "f4" }
        else
            _data.ambient = { vol = 0.2, abType = "f4" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f4" }
    end

    return _data
end

function SR.exportRadioAJS37(_data)
    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = false, intercomHotMic = false, desc = "" }

    _data.radios[2].name = "FR 22"
    _data.radios[2].freq = SR.getRadioFrequency(30)
    _data.radios[2].modulation = SR.getRadioModulation(30)
    _data.radios[2].volume = SR.getRadioVolume(0, 385, { 0.0, 1.0 }, false)
    _data.radios[2].volMode = 0

    _data.radios[3].name = "FR 24"
    _data.radios[3].freq = SR.getRadioFrequency(31)
    _data.radios[3].modulation = SR.getRadioModulation(31)
    _data.radios[3].volume = 1.0
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
    _data.radios[4].model = SR.RadioModels.AN_ARC164

    _data.control = 0
    _data.selected = 1

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = SR.getButtonPosition(10)

        if _door > 0.2 then
            _data.ambient = { vol = 0.3, abType = "ajs37" }
        else
            _data.ambient = { vol = 0.2, abType = "ajs37" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "ajs37" }
    end

    return _data
end

function SR.exportRadioF4U(_data)
    -- Reference manual: https://www.vmfa251.org/pdffiles/Corsair%20Manual.pdf
    -- p59 of the pdf (p53 for the manual) is the radio section.

    _data.capabilities = { dcsPtt = false, dcsIFF = false, dcsRadioSwitch = true, intercomHotMic = false, desc = "" }
    _data.iff =
        { status = 0, mode1 = 0, mode2 = -1, mode3 = 0, mode4 = false, control = 1, expansion = false, mic = -1 }

    local devices = {
        Cockpit = 0,
        Radio = 8,
    }

    local buttons = {
        Battery = 120,
        C38_Receiver_A = 82, -- VHF
        C38_Receiver_C = 83, -- MHF
        C30A_CW_Voice = 95,
    }

    local anarc5 = GetDevice(devices.Radio)

    -- TX ON implies RX ON.
    local txOn = anarc5:is_on()
    local vhfOn = SR.getButtonPosition(buttons.C38_Receiver_A) > 0
    local batteryOn = get_param_handle("DC_BUS"):get() > 1 -- SR.getButtonPosition(buttons.Battery) > 0
    local voiceSelected = SR.getButtonPosition(buttons.C30A_CW_Voice) > 0
    local rxOn = txOn or (batteryOn and vhfOn)

    -- AN/ARC-5 Radio
    _data.radios[2].name = "AN/ARC-5"
    _data.radios[2].channel = SR.getSelectorPosition(088, 0.33) + 1
    _data.radios[2].volume = SR.getRadioVolume(devices.Cockpit, 081, { 0.0, 1.0 }, false)
    _data.radios[2].rxOnly = rxOn and not txOn
    _data.radios[2].modulation = anarc5:get_modulation()
    _data.radios[2].model = SR.RadioModels.AN_ARC5

    if voiceSelected and (txOn or rxOn) then
        _data.radios[2].freq = SR.round(anarc5:get_frequency(), 5e3) or 0
    end

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

    _data.control = 0 -- no ptt, same as the FW and 109. No connector.

    if SR.getAmbientVolumeEngine() > 10 then
        -- engine on

        local _door = get_param_handle("BASE_SENSOR_CANOPY_STATE"):get()

        if _door > 0.5 then
            _data.ambient = { vol = 0.35, abType = "f4u" }
        else
            _data.ambient = { vol = 0.2, abType = "f4u" }
        end
    else
        -- engine off
        _data.ambient = { vol = 0, abType = "f4u" }
    end

    return _data
end

function SR.getRadioVolume(_deviceId, _arg, _minMax, _invert)
    local _device = GetDevice(_deviceId)

    if not _minMax then
        _minMax = { 0.0, 1.0 }
    end

    if _device then
        local _val = tonumber(_device:get_argument_value(_arg))
        local _reRanged = SR.rerange(_val, _minMax, { 0.0, 1.0 }) --re range to give 0.0 - 1.0

        if _invert then
            return SR.round(math.abs(1.0 - _reRanged), 0.005)
        else
            return SR.round(_reRanged, 0.005)
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
    local _num = math.abs(tonumber(string.format("%.0f", _value / _step)))

    return _num
end

function SR.getButtonPosition(_args)
    local _value = GetDevice(0):get_argument_value(_args)

    return _value
end

function SR.getNonStandardSpinner(_deviceId, _range, _step, _round)
    local _value = GetDevice(0):get_argument_value(_deviceId)
    -- round to x decimal places
    _value = SR.advRound(_value, _round)

    -- round to nearest step
    -- then round again to X decimal places
    _value = SR.advRound(SR.round(_value, _step), _round)

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
    end)

    return SR.round(_res, 1)
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
    return ((_limitMinMax[2] - _limitMinMax[1]) * (_val - _minMax[1]) / (_minMax[2] - _minMax[1])) + _limitMinMax[1]
end

function SR.round(number, step)
    if number == 0 then
        return 0
    else
        return math.floor((number + step / 2) / step) * step
    end
end

function SR.advRound(number, decimals, method)
    if string.find(number, "%p") ~= nil then
        decimals = decimals or 0
        local lFactor = 10 ^ decimals
        if method == "ceil" or method == "floor" then
            -- ceil: Returns the smallest integer larger than or equal to number
            -- floor: Returns the smallest integer smaller than or equal to number
            return math[method](number * lFactor) / lFactor
        else
            return tonumber(("%." .. decimals .. "f"):format(number))
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
        return '""'
    else
        if
            (type(var) == "number")
            or (type(var) == "boolean")
            or (type(var) == "function")
            or (type(var) == "table")
            or (type(var) == "userdata")
        then
            return tostring(var)
        elseif type(var) == "string" then
            var = string.format("%q", var)
            return var
        end
    end
end

function SR.debugDump(o)
    if o == nil then
        return "~nil~"
    elseif type(o) == "table" then
        local s = "{ "
        for k, v in pairs(o) do
            if type(k) ~= "number" then
                k = '"' .. k .. '"'
            end
            s = s .. "[" .. k .. "] = " .. SR.debugDump(v) .. ","
        end
        return s .. "} "
    else
        return tostring(o)
    end
end

function SR.tableShow(tbl, loc, indent, tableshow_tbls) --based on serialize_slmod, this is a _G serialization
    tableshow_tbls = tableshow_tbls or {} --create table of tables
    loc = loc or ""
    indent = indent or ""
    if type(tbl) == "table" then --function only works for tables!
        tableshow_tbls[tbl] = loc

        local tbl_str = {}

        tbl_str[#tbl_str + 1] = indent .. "{\n"

        for ind, val in pairs(tbl) do -- serialize its fields
            if type(ind) == "number" then
                tbl_str[#tbl_str + 1] = indent
                tbl_str[#tbl_str + 1] = loc .. "["
                tbl_str[#tbl_str + 1] = tostring(ind)
                tbl_str[#tbl_str + 1] = "] = "
            else
                tbl_str[#tbl_str + 1] = indent
                tbl_str[#tbl_str + 1] = loc .. "["
                tbl_str[#tbl_str + 1] = SR.basicSerialize(ind)
                tbl_str[#tbl_str + 1] = "] = "
            end

            if (type(val) == "number") or (type(val) == "boolean") then
                tbl_str[#tbl_str + 1] = tostring(val)
                tbl_str[#tbl_str + 1] = ",\n"
            elseif type(val) == "string" then
                tbl_str[#tbl_str + 1] = SR.basicSerialize(val)
                tbl_str[#tbl_str + 1] = ",\n"
            elseif type(val) == "nil" then -- won't ever happen, right?
                tbl_str[#tbl_str + 1] = "nil,\n"
            elseif type(val) == "table" then
                if tableshow_tbls[val] then
                    tbl_str[#tbl_str + 1] = tostring(val) .. " already defined: " .. tableshow_tbls[val] .. ",\n"
                else
                    tableshow_tbls[val] = loc .. "[" .. SR.basicSerialize(ind) .. "]"
                    tbl_str[#tbl_str + 1] = tostring(val) .. " "
                    tbl_str[#tbl_str + 1] = SR.tableShow(
                        val,
                        loc .. "[" .. SR.basicSerialize(ind) .. "]",
                        indent .. "        ",
                        tableshow_tbls
                    )
                    tbl_str[#tbl_str + 1] = ",\n"
                end
            elseif type(val) == "function" then
                if debug and debug.getinfo then
                    local fcnname = tostring(val)
                    local info = debug.getinfo(val, "S")
                    if info.what == "C" then
                        tbl_str[#tbl_str + 1] = string.format("%q", fcnname .. ", C function") .. ",\n"
                    else
                        if string.sub(info.source, 1, 2) == [[./]] then
                            tbl_str[#tbl_str + 1] = string.format(
                                "%q",
                                fcnname
                                    .. ", defined in ("
                                    .. info.linedefined
                                    .. "-"
                                    .. info.lastlinedefined
                                    .. ")"
                                    .. info.source
                            ) .. ",\n"
                        else
                            tbl_str[#tbl_str + 1] = string.format(
                                "%q",
                                fcnname .. ", defined in (" .. info.linedefined .. "-" .. info.lastlinedefined .. ")"
                            ) .. ",\n"
                        end
                    end
                else
                    tbl_str[#tbl_str + 1] = "a function,\n"
                end
            else
                tbl_str[#tbl_str + 1] = "unable to serialize value type "
                    .. SR.basicSerialize(type(val))
                    .. " at index "
                    .. tostring(ind)
            end
        end

        tbl_str[#tbl_str + 1] = indent .. "}"
        return table.concat(tbl_str)
    end
end

---- Exporters init ----
SR.exporters["UH-1H"] = SR.exportRadioUH1H
SR.exporters["CH-47Fbl1"] = SR.exportRadioCH47F
SR.exporters["Ka-50"] = SR.exportRadioKA50
SR.exporters["Ka-50_3"] = SR.exportRadioKA50
SR.exporters["Mi-8MT"] = SR.exportRadioMI8
SR.exporters["Mi-24P"] = SR.exportRadioMI24P
SR.exporters["Yak-52"] = SR.exportRadioYak52
SR.exporters["FA-18C_hornet"] = SR.exportRadioFA18C
SR.exporters["FA-18E"] = SR.exportRadioFA18C
SR.exporters["FA-18F"] = SR.exportRadioFA18C
SR.exporters["EA-18G"] = SR.exportRadioFA18C
SR.exporters["F-86F Sabre"] = SR.exportRadioF86Sabre
SR.exporters["MiG-15bis"] = SR.exportRadioMIG15
SR.exporters["MiG-19P"] = SR.exportRadioMIG19
SR.exporters["MiG-21Bis"] = SR.exportRadioMIG21
SR.exporters["F-5E-3"] = SR.exportRadioF5E
SR.exporters["FW-190D9"] = SR.exportRadioFW190
SR.exporters["FW-190A8"] = SR.exportRadioFW190
SR.exporters["Bf-109K-4"] = SR.exportRadioBF109
SR.exporters["C-101EB"] = SR.exportRadioC101EB
SR.exporters["C-101CC"] = SR.exportRadioC101CC
SR.exporters["MB-339A"] = SR.exportRadioMB339A
SR.exporters["MB-339APAN"] = SR.exportRadioMB339A
SR.exporters["Hawk"] = SR.exportRadioHawk
SR.exporters["Christen Eagle II"] = SR.exportRadioEagleII
SR.exporters["M-2000C"] = SR.exportRadioM2000C
SR.exporters["M-2000D"] = SR.exportRadioM2000C
SR.exporters["Mirage-F1CE"] = SR.exportRadioF1CE
SR.exporters["Mirage-F1EE"] = SR.exportRadioF1CE
SR.exporters["Mirage-F1BE"] = SR.exportRadioF1BE
--SR.exporters["Mirage-F1M-EE"] = SR.exportRadioF1
SR.exporters["JF-17"] = SR.exportRadioJF17
SR.exporters["AV8BNA"] = SR.exportRadioAV8BNA
SR.exporters["AJS37"] = SR.exportRadioAJS37
SR.exporters["A-10A"] = SR.exportRadioA10A
SR.exporters["UH-60L"] = SR.exportRadioUH60L
SR.exporters["MH-60R"] = SR.exportRadioUH60L
SR.exporters["SH60B"] = SR.exportRadioSH60B
SR.exporters["AH-64D_BLK_II"] = SR.exportRadioAH64D
SR.exporters["A-4E-C"] = SR.exportRadioA4E
SR.exporters["SK-60"] = SR.exportRadioSK60
SR.exporters["PUCARA"] = SR.exportRadioPUCARA
SR.exporters["T-45"] = SR.exportRadioT45
SR.exporters["A-29B"] = SR.exportRadioA29B
SR.exporters["VSN_F4C"] = SR.exportRadioVSNF4
SR.exporters["VSN_F4B"] = SR.exportRadioVSNF4
SR.exporters["VSN_F104C"] = SR.exportRadioVSNF104C
SR.exporters["Hercules"] = SR.exportRadioHercules
SR.exporters["F-15C"] = SR.exportRadioF15C
SR.exporters["F-15ESE"] = SR.exportRadioF15ESE
SR.exporters["MiG-29A"] = SR.exportRadioMiG29
SR.exporters["MiG-29S"] = SR.exportRadioMiG29
SR.exporters["MiG-29G"] = SR.exportRadioMiG29
SR.exporters["Su-27"] = SR.exportRadioSU27
SR.exporters["J-11A"] = SR.exportRadioSU27
SR.exporters["Su-33"] = SR.exportRadioSU27
SR.exporters["Su-25"] = SR.exportRadioSU25
SR.exporters["Su-25T"] = SR.exportRadioSU25
SR.exporters["F-16C_50"] = SR.exportRadioF16C
SR.exporters["F-16D_50_NS"] = SR.exportRadioF16C
SR.exporters["F-16D_52_NS"] = SR.exportRadioF16C
SR.exporters["F-16D_50"] = SR.exportRadioF16C
SR.exporters["F-16D_52"] = SR.exportRadioF16C
SR.exporters["F-16D_Barak_40"] = SR.exportRadioF16C
SR.exporters["F-16D_Barak_30"] = SR.exportRadioF16C
SR.exporters["F-16I"] = SR.exportRadioF16C
SR.exporters["SA342M"] = SR.exportRadioSA342
SR.exporters["SA342L"] = SR.exportRadioSA342
SR.exporters["SA342Mistral"] = SR.exportRadioSA342
SR.exporters["SA342Minigun"] = SR.exportRadioSA342
SR.exporters["OH58D"] = SR.exportRadioOH58D
SR.exporters["OH-6A"] = SR.exportRadioOH6A
SR.exporters["L-39C"] = SR.exportRadioL39
SR.exporters["L-39ZA"] = SR.exportRadioL39
SR.exporters["F-14B"] = SR.exportRadioF14
SR.exporters["F-14A-135-GR"] = SR.exportRadioF14
SR.exporters["A-10C"] = SR.exportRadioA10C
SR.exporters["A-10C_2"] = SR.exportRadioA10C2
SR.exporters["P-51D"] = SR.exportRadioP51
SR.exporters["P-51D-30-NA"] = SR.exportRadioP51
SR.exporters["TF-51D"] = SR.exportRadioP51
SR.exporters["P-47D-30"] = SR.exportRadioP47
SR.exporters["P-47D-30bl1"] = SR.exportRadioP47
SR.exporters["P-47D-40"] = SR.exportRadioP47
SR.exporters["SpitfireLFMkIX"] = SR.exportRadioSpitfireLFMkIX
SR.exporters["SpitfireLFMkIXCW"] = SR.exportRadioSpitfireLFMkIX
SR.exporters["MosquitoFBMkVI"] = SR.exportRadioMosquitoFBMkVI
SR.exporters["F-4E-45MC"] = SR.exportRadioF4
SR.exporters["F4U-1D"] = SR.exportRadioF4U
SR.exporters["F4U-1D_CW"] = SR.exportRadioF4U

--- DCS EXPORT FUNCTIONS
LuaExportActivityNextEvent = function(tCurrent)
    -- we only want to send once every 0.2 seconds
    -- but helios (and other exports) require data to come much faster
    if _tNextSRS - tCurrent < 0.01 then -- has to be written this way as the function is being called with a loss of precision at times
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
            SR.error("Calling other LuaExportActivityNextEvent from another script: " .. SR.debugDump(_result))
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
        SR.error("LuaExportBeforeNextFrame readLOSSocket SRS: " .. SR.debugDump(_result))
    end

    _status, _result = pcall(SR.readSeatSocket)

    if not _status then
        SR.error("LuaExportBeforeNextFrame readSeatSocket SRS: " .. SR.debugDump(_result))
    end

    -- call original
    if _prevLuaExportBeforeNextFrame then
        _status, _result = pcall(_prevLuaExportBeforeNextFrame)
        if not _status then
            SR.error("Calling other LuaExportBeforeNextFrame from another script: " .. SR.debugDump(_result))
        end
    end
end

-- Load mods' SRS plugins
SR.LoadModsPlugins()

SR.log("Loaded SimpleRadio Standalone Export version: 2.3.0.3")
