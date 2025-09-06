using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public partial class ServerSettingsModel : ObservableObject
{
	
	[ObservableProperty] private bool _isLineOfSightEnabled = false;
	[ObservableProperty] private bool _isDistanceLimitEnabled = false;
	[ObservableProperty] private bool _isIRLRadioTXEffectsEnabled = false;
	[ObservableProperty] private bool _isIRLRadioRXEffectsEnabled = false;
	[ObservableProperty] private bool _isRadioExpansionAllowed = false;
	[ObservableProperty] private bool _isRadioEffectOverrideOnGlobalEnabled = false;
	[ObservableProperty] private int _retransmissionNodeLimit = 0;
	
	[ObservableProperty] private bool _isCoalitionAudioSecurityEnabled = false;
	[ObservableProperty] private bool _isSpectatorAudioDisabled = false;

	public ObservableCollection<string> TestFrequencies { get; set; } = new() { "125.2", "142.5" };

	[ObservableProperty] private int _serverPort = 5002;
	[ObservableProperty] private bool _isClientExportEnabled = false;
	public bool IsExternalModeEnabled {get; set;} = false;
	public string ExternalModePassBlue { get; set; } = string.Empty;
	public string ExternalModePassRed { get; set; } = string.Empty;
	public string ClientExportFilePath { get; set; } = string.Empty;
	public bool IsCheckForBetaUpdatesEnabled { get; set; } = false;
	public bool IsRadioEncryptionAllowed { get; set; } = true;
	public bool IsShowTunedCountEnabled { get; set; } = true;
	public ObservableCollection<string> GlobalLobbyFrequencies { get; set; } = new(){ "248.22" };
	public bool IsShowTransmitterNameEnabled { get; set; } = false;
	public bool IsLotAtcExportEnabled { get; set; } = false;
	public int LotAtcExportPort { get; set; } = 10712;
	public string LotAtcExportIP { get; set; } = "127.0.0.1";
	public bool isUPNPEnabled { get; set; } = true;
	public bool IsStrictRadioEncryptionEnabled { get; set; } = false;
	public bool IsTransmissionLogEnabled { get; set; } = false;
	public int TransmissionLogRetentionLimit { get; set; } = 2;
	public string ServerIP { get; set; } = "0.0.0.0";
	public bool IsServerPresetsEnabled { get; set; } = false;
	public string ServerPresetsPath { get; set; } = string.Empty;
	public bool IsHTTPServerEnabled { get; set; } = false;
	public int HTTPServerPort { get; set; } = 8080;
}