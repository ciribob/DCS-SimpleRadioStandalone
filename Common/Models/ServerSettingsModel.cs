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
	public ObservableCollection<double> TestFrequencies { get; set; } = new() { 125.2, 142.5 };
	[ObservableProperty] private int _serverPort = 5002;
	[ObservableProperty] private bool _isClientExportEnabled = false;
	[ObservableProperty] private bool _isExternalModeEnabled = false;
	[ObservableProperty] private string _externalModePassBlue = string.Empty;
	[ObservableProperty] private string _externalModePassRed = string.Empty;
	[ObservableProperty] private string _clientExportFilePath = string.Empty;
	[ObservableProperty] private bool _isCheckForBetaUpdatesEnabled = false;
	[ObservableProperty] private bool _isRadioEncryptionAllowed = true;
	[ObservableProperty] private bool _isShowTunedCountEnabled = true;
	public ObservableCollection<double> GlobalLobbyFrequencies { get; set; } = new() { 248.22 };
	[ObservableProperty] private bool _isShowTransmitterNameEnabled = false;
	[ObservableProperty] private bool _isLotAtcExportEnabled = false;
	[ObservableProperty] private int _lotAtcExportPort = 10712;
	[ObservableProperty] private string _lotAtcExportIP = "127.0.0.1";
	[ObservableProperty] private bool _isUPNPEnabled = true;
	[ObservableProperty] private bool _isStrictRadioEncryptionEnabled = false;
	[ObservableProperty] private bool _isTransmissionLogEnabled = false;
	[ObservableProperty] private int _transmissionLogRetentionLimit = 2;
	[ObservableProperty] private string _serverIP = "0.0.0.0";
	[ObservableProperty] private bool _isServerPresetsEnabled = false;
	[ObservableProperty] private string _serverPresetsPath = string.Empty;
	[ObservableProperty] private bool _isHTTPServerEnabled = false;
	[ObservableProperty] private int _httpServerPort = 8080;
}