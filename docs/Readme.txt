Extract all the files from the zip archive before running the Installer!

Installer needs to be run by CLIENTS ONLY

Dedicated servers DO NOT need the installer run.

*** To Install Manually for Clients ***

Copy Scripts\DCS-SRS folder to C:\Users\USERNAME\Saved Games\DCS\Mods\Services 

Copy Scripts\Hooks\DCS-SRS-Hook.lua to C:\Users\USERNAME\Saved Games\DCS\Scripts\Hooks -- NOTE: Sub-folder in Scripts folder

Create the folders if they dont exist

Add:

pcall(function() local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Mods\Services\\DCS-SRS\\Scripts\\DCS-SimpleRadioStandalone.lua]]); end,nil)

To the END of the Export.lua file in C:\Users\USERNAME\Saved Games\DCS\Scripts

If it doesnt exist, just create the file and add the single line to it.

Copy the rest of the zip archive where ever you like and then run, don't forget to keep opus.dll with the rest of the .exes

Thread on Forums: http://forums.eagle.ru/showthread.php?t=169387


*** To Install AutoConnect System for SERVERS only ***
To enable SRS clients to be prompted automatically to connect just add the DCS-SRS-AutoConnectGameGUI.lua file 
to the appropriate DCS Saved Games  folder e.g. DCS.openbeta/Scripts/Hooks, DCS.openalpha/Scripts/Hooks or just DCS/Scripts/Hooks

Edit the line below to your server address where SRS server is running. Port is optional. DCS must be restarted on the server for this file and any changes to take effect.


-- CHANGE FROM
SRSAuto.SERVER_SRS_HOST = "127.0.0.1" -- Port optional e.g. "127.0.0.1:5002"
--TO
SRSAuto.SERVER_SRS_HOST = "5.189.162.17:5016" -- BuddySpike One
-- OR
SRSAuto.SERVER_SRS_HOST = " 37.59.10.136" -- TAW One (port optional)

And thats it. 

If a client isn't connected and has SRS running they'll be prompted to connect automatically. You'll also see the message posted in the chat listing the address when slots change or a client connects.

***** FAQ *****
Q: I Hear Static on certain frequencies
A: This is likely an encrypted transmission. You will need to configure your KY-58 or Encrypted Radio Appropriately 

Q: How do I run a server?
A: Run SR-Server.exe (no need to run installer or add scripts!) and make sure TCP + UDP PORT 5002 are open (if port is left as default) 
   It does NOT need to be on a PC running DCS and you do NOT need to port forward if you're just using the client.

Q: I've installed everything manually and its not working
A: Delete your entire Scripts folder -  DCS\Scripts, DCS.openbeta\Scripts and DCS.openalpha\Scripts and run the installer again

