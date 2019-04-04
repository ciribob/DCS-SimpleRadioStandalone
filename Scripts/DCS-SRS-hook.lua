--Hook to load SRS GameGUI for getting current user coalition and multiplayer name
local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Scripts\DCS-SRSGameGUI.lua]])

pcall(function() local dcsSrOverlay=require('lfs');dofile(dcsSr.writedir()..[[Scripts\DCS-SRS-OverlayGameGUI.lua]]); end,nil)
