--Hook to load SRS GameGUI for getting current user coalition and multiplayer name
local status, result = pcall(function()
    local dcsSr = require("lfs")
    dofile(dcsSr.writedir() .. [[Mods\Services\DCS-SRS\Scripts\DCS-SRSGameGUI.lua]])
end, nil)

if not status then
    net.log(result)
end

status, result = pcall(function()
    local dcsSr = require("lfs")
    dofile(dcsSr.writedir() .. [[Mods\Services\DCS-SRS\Scripts\DCS-SRS-OverlayGameGUI.lua]])
end, nil)
if not status then
    net.log(result)
end
