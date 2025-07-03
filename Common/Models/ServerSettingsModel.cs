namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public class ServerSettingsModel
{
	public bool IsLineOfSightEnabled {get; set;}
	public bool IsDistanceLimitEnabled {get; set;}
	public bool IsIRLRadioTXEffectsEnabled {get; set;}
	public bool IsIRLRadioRXEffectsEnabled {get; set;}
	public bool IsRadioExpansionAllowed {get; set;}
	public bool IsRadioEffectOverrideOnGlobalEnabled { get; set; }
	public int RetransmissionNodeLimit { get; set; }
	
	public bool IsCoalitionAudioSecurityEnabled {get; set;}
	public bool IsSpectatorAudioDisabled {get; set;}

	public string[] TestFrequencies { get; set; } = new string[2] { "125.2", "142.5" };





	public int ServerPort { get; set; }
	public bool IsClientExportEnabled {get; set;}
	public bool IsExternalModeEnabled {get; set;}
	public string ExternalModeBlue { get; set; }
	public string ExternalModeRed { get; set; }
	public string ClientExportFilePath { get; set; }
	public bool IsCheckForBetaUpdatesEnabled { get; set; }
	public bool IsRadioEncryptionAllowed { get; set; }
	public bool IsShowTunedCountEnabled { get; set; }
	public string GlobalLobbyFrequencies { get; set; }
	public bool IsShowTransmitterNameEnabled { get; set; }
	public bool IsLotAtcExportEnabled { get; set; }
	public int LotAtcExportPort { get; set; }
	public string LotAtcExportIP { get; set; }
	public bool isUPNPEnabled { get; set; }
	public bool IsStrictRadioEncryptionEnabled { get; set; }
	public bool IsTransmissionLogEnabled { get; set; }
	public int TransmissionLogRetentionLimit { get; set; }
	public string ServerIP { get; set; }
	public bool IsServerPresetsEnabled { get; set; }
	public string ServerPresetsPath { get; set; }
	public bool IsHTTPServerEnabled { get; set; }
	public int HTTPServerPort { get; set; }
}