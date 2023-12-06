declare_plugin("DCS-SRS", {
	installed = true,
	dirName = current_mod_path,
	developerName = _("Ciribob"),
	developerLink = _("https://github.com/ciribob/DCS-SimpleRadioStandalone"),
	displayName = _("DCS SimpleRadio Standalone"),
	version = "2.0.9.1",
	state = "installed",
	info = _("DCS-SimpleRadio Standalone\n\nBrings realistic VoIP comms to DCS with a cockpit integration with every aircraft\n\nCheck Special Settings for SRS integration settings\n\nSRS Discord for Support: https://discord.gg/baw7g3t"),
	binaries = {"srs.dll"},
    load_immediate = true,
	Skins = {
		{ name = "DCS-SRS", dir = "Theme" },
	},
	Options = {
		{ name = "DCS-SRS", nameId = "DCS-SRS", dir = "Options", allow_in_simulation = true; },
	},
})

plugin_done()