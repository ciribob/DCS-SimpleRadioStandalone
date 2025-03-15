using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public partial class ProfileSettingsModel : ObservableObject, ICloneable
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage());
		base.OnPropertyChanging(e);
	}
	
	[ObservableProperty] private bool _radioEffects = true;
	[ObservableProperty] private bool _radioEffectsClipping = false;
	[ObservableProperty] private bool _radioEncryptionEffects = true;
	[ObservableProperty] private bool _natoFmTone = true;
	[ObservableProperty] private bool _haveQuickTone = true;
	[ObservableProperty] private bool _radioRxEffects_Start = true;
	[ObservableProperty] private bool _radioRxEffects_End = true;
	
	[ObservableProperty] private string _radioTransmissionStartEffectName = "";
	[ObservableProperty] private string _radioTransmissionEndEffectName = "";
	[ObservableProperty] private string _intercomTransmissionStartEffectName = "";
	[ObservableProperty] private string _intercomTransmissionEndEffectName = "";
	
	[ObservableProperty] private bool _radioTxEffectsStart = true;
	[ObservableProperty] private bool _radioTxEffectsEnd = true;
	[ObservableProperty] private bool _midsRadioEffect = true;
	
	[ObservableProperty] private bool _autoSelectPresetChannel = true;
	[ObservableProperty] private bool _alwaysAllowHotasControls = false;

	[ObservableProperty] private bool _allowDcsPtt = true;
	[ObservableProperty] private bool _radioSwitchIsPtt = false;
	[ObservableProperty] private bool _radioSwitchIsPttOnlyWhenValid =  false;
	[ObservableProperty] private bool _alwaysAllowTransponderOverlay = false;

	[ObservableProperty] private float _pttReleaseDelay = 0.0f;
	[ObservableProperty] private float _pttStartDelay = 0.0f;
	[ObservableProperty] private bool _radioBackgroundNoiseEffect = false;
	
	[ObservableProperty] private float _natoFmToneVolume = 1.2f;
	[ObservableProperty] private float _hqToneVolume = 0.3f;
	[ObservableProperty] private float _vhfNoiseVolume = 0.15f;
	[ObservableProperty] private float _hfNoiseVolume = 0.15f;
	[ObservableProperty] private float _uhfNoiseVolume = 0.15f;
	[ObservableProperty] private float _fmNoiseVolume = 0.4f;
	
	[ObservableProperty] private float _amCollisionToneVolume = 1.0f;
	[ObservableProperty] private bool _rotaryStyleIncrement = false;
	[ObservableProperty] private bool _ambientCockpitNoiseEffect = true;
	[ObservableProperty] private float _ambientCockpitNoiseEffectVolume = 1.0f;
	[ObservableProperty] private bool _ambientCockpitIntercomNoiseEffect = false;
	[ObservableProperty] private bool _disableExpansionRadios = false;
	[ObservableProperty] private float _balanceIntercom = 0.0f;
	[ObservableProperty] private float _balanceRadio01 = 0.0f;
	[ObservableProperty] private float _balanceRadio02 = 0.0f;
	[ObservableProperty] private float _balanceRadio03 = 0.0f;
	[ObservableProperty] private float _balanceRadio04 = 0.0f;
	[ObservableProperty] private float _balanceRadio05 = 0.0f;
	[ObservableProperty] private float _balanceRadio06 = 0.0f;
	[ObservableProperty] private float _balanceRadio07 = 0.0f;
	[ObservableProperty] private float _balanceRadio08 = 0.0f;
	[ObservableProperty] private float _balanceRadio09 = 0.0f;
	[ObservableProperty] private float _balanceRadio10 = 0.0f;
	
	[ObservableProperty] private InputSettingModel _inputIntercom = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch01 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch02 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch03 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch04 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch05 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch06 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch07 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch08 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch09 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputSwitch10 = new InputSettingModel();
	
	[ObservableProperty] private InputSettingModel _radioNext = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _radioPrevious = new InputSettingModel();
	
	[ObservableProperty] private InputSettingModel _inputIntercomPtt = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputPushToTalk = new InputSettingModel();
	
	[ObservableProperty] private InputSettingModel _awacsOverlayToggle = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _overlayToggle = new InputSettingModel();

	[ObservableProperty] private InputSettingModel _inputUp100 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputUp10 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputUp1 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputUp01 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputUp001 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputUp0001 = new InputSettingModel();

	[ObservableProperty] private InputSettingModel _inputDown100 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputDown10 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputDown1 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputDown01 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputDown001 = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _inputDown0001 = new InputSettingModel();
	
	[ObservableProperty] private InputSettingModel _transponderIdent = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _guardToggle = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _encryptionToggle = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _encryptionKeyUp = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _encryptionKeyDown = new InputSettingModel();
	
	[ObservableProperty] private InputSettingModel _radioChannelUp = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _radioChannelDown = new InputSettingModel();
	
	[ObservableProperty] private InputSettingModel _radioVolumeUp = new InputSettingModel();
	[ObservableProperty] private InputSettingModel _radioVolumeDown = new InputSettingModel();
	
	public object Clone()
	{
		return this.MemberwiseClone();
	}
}