using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public partial class ProfileSettingsModel : ObservableObject, ICloneable
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage(SettingCatagory.Profile));
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
	
	[ObservableProperty] private InputBindingModel _inputIntercom = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch01 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch02 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch03 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch04 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch05 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch06 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch07 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch08 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch09 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputSwitch10 = new InputBindingModel();
	
	[ObservableProperty] private InputBindingModel _radioNext = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _radioPrevious = new InputBindingModel();
	
	[ObservableProperty] private InputBindingModel _inputIntercomPtt = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputPushToTalk = new InputBindingModel();
	
	[ObservableProperty] private InputBindingModel _awacsOverlayToggle = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _overlayToggle = new InputBindingModel();

	[ObservableProperty] private InputBindingModel _inputUp100 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputUp10 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputUp1 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputUp01 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputUp001 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputUp0001 = new InputBindingModel();

	[ObservableProperty] private InputBindingModel _inputDown100 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputDown10 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputDown1 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputDown01 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputDown001 = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _inputDown0001 = new InputBindingModel();
	
	[ObservableProperty] private InputBindingModel _transponderIdent = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _guardToggle = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _encryptionToggle = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _encryptionKeyUp = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _encryptionKeyDown = new InputBindingModel();
	
	[ObservableProperty] private InputBindingModel _radioChannelUp = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _radioChannelDown = new InputBindingModel();
	
	[ObservableProperty] private InputBindingModel _radioVolumeUp = new InputBindingModel();
	[ObservableProperty] private InputBindingModel _radioVolumeDown = new InputBindingModel();
	
	
	[JsonIgnore]
	public List<InputBindingModel> InputSettingsList 
	{
		get
		{
			var temp = GetType().GetProperties()
				.Where(property => property.PropertyType == typeof(InputBindingModel))
				.Select(x => (InputBindingModel)x.GetValue(this))
				.ToList();

			return temp;
		} 
	}

	[JsonIgnore]
	public List<float> RadioBalanceList => new(){
		BalanceIntercom, 
		BalanceRadio01, BalanceRadio02, BalanceRadio03, BalanceRadio04, BalanceRadio05, 
		BalanceRadio06, BalanceRadio07, BalanceRadio08, BalanceRadio09, BalanceRadio10
	};
	
	public object Clone()
	{
		return this.MemberwiseClone();
	}
}