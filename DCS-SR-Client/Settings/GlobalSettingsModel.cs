using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public partial class GlobalSettingsModel : ObservableObject
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage());
		base.OnPropertyChanging(e);
	}

	[ObservableProperty] private bool _autoConnect = true;
	[ObservableProperty] private bool _autoConnectPrompt = false;
	[ObservableProperty] private bool _autoConnectMismatchPrompt = true;
	[ObservableProperty] private bool _radioOverlayTaskbarHide = false;
	[ObservableProperty] private bool _refocusDcs = false;
	[ObservableProperty] private bool _expandControls = false;
	
	[ObservableProperty] private bool _minimiseToTray = false;
	[ObservableProperty] private bool _startMinimised = false;
	
	[ObservableProperty] private string _audioInputDeviceId = string.Empty;
	[ObservableProperty] private string _audioOutputDeviceId = string.Empty;
	[ObservableProperty] private string _sideToneDeviceId = string.Empty;

	[ObservableProperty] private string _lastServer = "127.0.0.1";
	
	[ObservableProperty] private double _micBoost = (double)0.514;
	[ObservableProperty] private double _speakerBoost = (double)0.514;
	
	[ObservableProperty] private double _radioX = (double)300;
	[ObservableProperty] private double _radioY = (double)300;
	[ObservableProperty] private double _radioSize = (double)1.0;
	[ObservableProperty] private double _radioOpacity = (double)1.0;
	
	[ObservableProperty] private double _radioWidth = (double)122;
	[ObservableProperty] private double _radioHeight = (double)270;
	
	[ObservableProperty] private double _clientX = (double)200;
	[ObservableProperty] private double _clientY = (double)200;
	
	[ObservableProperty] private double _awacsX = 0;
	[ObservableProperty] private double _awacsY = (double)300;
	
	
	[ObservableProperty] private string _cliendIdShort = string.Empty;
	[ObservableProperty] private string _clientIdLong = string.Empty;
	
	[ObservableProperty] private int _dcsLosOutgoingUdp = (int)9086;
	[ObservableProperty] private int _dcsIncomingUdp = (int)9084;
	[ObservableProperty] private int _commandListenerUdp = (int)9040;
	[ObservableProperty] private int _outgoingDcsUdpInfo = (int)7080;
	[ObservableProperty] private int _outgoingDcsUdpOther = (int)7082;
	[ObservableProperty] private int _dcsIncomingGameGuiUdp = (int)5068;
	[ObservableProperty] private int _dcsLosIncomingUdp = (int)9085;
	[ObservableProperty] private int _dcsAutoConnectUdp = (int)5069;
	
	[ObservableProperty] private bool _automaticGainControl = true;
	[ObservableProperty] private int _agcTarget = (int)30000;
	[ObservableProperty] private int _agcDecrement = (int)-60;
	[ObservableProperty] private int _agcLevelMax = (int)68;
	
	[ObservableProperty] private bool _denoise = true;
	[ObservableProperty] private int _denoiseAttenuation = (int)-30;
	
	[ObservableProperty] private string _lastSeenName = string.Empty;
	
	[ObservableProperty] private bool _checkForBetaUpdates = false;
	
	[ObservableProperty] private bool _allowMultipleInstances = false;
	
	[ObservableProperty] private bool _disableWindowVisibilityCheck = false;

	[ObservableProperty] private bool _playConnectionSounds = true;

	[ObservableProperty] private bool _requireAdmin = true;
	[ObservableProperty] private bool _allowAnonymousUsage = false;
	
	[ObservableProperty] private string _currentProfileName  = "default";

	[ObservableProperty] private bool _autoSelectSettingsProfile = false;
	
	[ObservableProperty] private int _lotAtcIncomingUdp = (int)10710;
	[ObservableProperty] private int _lotAtcOutgoingUdp = (int)10711;
	[ObservableProperty] private int _lotAtcHeightOffset = (int)50;
	
	[ObservableProperty] private int _vaicomIncomingUdp = (int)33501;
	[ObservableProperty] private bool _vaicomTxInhibitEnabled = false;
	[ObservableProperty] private bool _showTransmitterName = true;
	
	[ObservableProperty] private int _idleTimeOut = (int)600;
	
	[ObservableProperty] private bool _allowRecording = false;
	[ObservableProperty] private bool _recordAudio = false;
	[ObservableProperty] private bool _singleFileMixdown = false;
	[ObservableProperty] private int _recordingQuality = (int)3;
	[ObservableProperty] private bool _disallowedAudioTone = false;
	
	[ObservableProperty] private bool _vox = false;
	[ObservableProperty] private int _voxMode = (int)3;
	[ObservableProperty] private int _voxMinimumTime = (int)300;
	[ObservableProperty] private double _voxMinimumDb = (double)-59.0;
	
	[ObservableProperty] private bool _allowXInputController = false;
	[ObservableProperty] private string _lastPresetsFolder = Directory.GetCurrentDirectory();
	[ObservableProperty] private string _lastPresetsFile = "(Default)";

}