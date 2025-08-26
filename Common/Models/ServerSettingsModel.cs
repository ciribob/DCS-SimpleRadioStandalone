using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public class ServerSettingsModel : INotifyPropertyChanged
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

	public ObservableCollection<string> TestFrequencies { get; set; } = new() { "125.2", "142.5" };

	public int ServerPort { get; set; }
	public bool IsClientExportEnabled {get; set;}
	public bool IsExternalModeEnabled {get; set;}
	public string ExternalModeBlue { get; set; }
	public string ExternalModeRed { get; set; }
	public string ClientExportFilePath { get; set; }
	public bool IsCheckForBetaUpdatesEnabled { get; set; }
	public bool IsRadioEncryptionAllowed { get; set; }
	public bool IsShowTunedCountEnabled { get; set; }
	public ObservableCollection<string> GlobalLobbyFrequencies { get; set; } = new();
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
	
	
	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}