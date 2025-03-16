using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

	#region Bulk Fields
	/// <summary>
	/// This section contains only fields. The ObservableProperty attributes, provided by the MVVM Toolkit,
	/// use codegen to build properties.
	/// These properties are a able to be traded around and pointed to more consistently.
	/// </summary>

	[ObservableProperty] private bool _radioEffects = true;
	[ObservableProperty] private bool _radioEffectsClipping = false;
	[ObservableProperty] private bool _radioEncryptionEffects = true;
	[ObservableProperty] private bool _natoFmTone = true;
	[ObservableProperty] private bool _isHaveQuickToneEnabled = true;
	
	[ObservableProperty] private string _radioTransmissionStartEffectName = "";
	[ObservableProperty] private string _radioTransmissionEndEffectName = "";
	[ObservableProperty] private string _intercomTransmissionStartEffectName = "";
	[ObservableProperty] private string _intercomTransmissionEndEffectName = "";
	
	[ObservableProperty] private bool _isRadioTxStartEffectsEnabled = true;
	[ObservableProperty] private bool _isRadioTxEndEffectsEnabled = true;
	[ObservableProperty] private bool _isRadioRxStartEffectsEnabled = true;
	[ObservableProperty] private bool _isRadioRxEndEffectsEnabled = true;
	[ObservableProperty] private bool _midsRadioEffect = true;
	
	[ObservableProperty] private bool _autoSelectPresetChannel = true;
	[ObservableProperty] private bool _alwaysAllowHotasControls = false;

	[ObservableProperty] private bool _allowDcsPtt = true;
	[ObservableProperty] private bool _radioSwitchIsPtt = false;
	[ObservableProperty] private bool _radioSwitchIsPttOnlyWhenValid =  false;
	[ObservableProperty] private bool _alwaysAllowTransponderOverlay = false;

	[ObservableProperty] private float _pttReleaseDelay = 0.0f;
	[ObservableProperty] private float _pttStartDelay = 0.0f;
	[ObservableProperty] private bool _isRadioBackgroundNoiseEffectEnabled = false;
	
	[ObservableProperty] private float _natoFmToneVolume = 1.2f;
	[ObservableProperty] private float _haveQuickToneVolume = 0.3f;
	[ObservableProperty] private float _vhfNoiseVolume = 0.15f;
	[ObservableProperty] private float _hfNoiseVolume = 0.15f;
	[ObservableProperty] private float _uhfNoiseVolume = 0.15f;
	[ObservableProperty] private float _fmNoiseVolume = 0.4f;
	
	[ObservableProperty] private float _amCollisionToneVolume = 1.0f;
	[ObservableProperty] private bool _rotaryStyleIncrement = false;
	[ObservableProperty] private bool _isAmbientCockpitNoiseEffectEnabled = true;
	[ObservableProperty] private float _ambientCockpitNoiseEffectVolume = 1.0f;
	[ObservableProperty] private bool _isAmbientCockpitIntercomNoiseEffectEnabled = false;
	[ObservableProperty] private bool _isExpansionRadiosDisabled = false;
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

	[ObservableProperty] private InputBindingModel _inputIntercom = new();
	[ObservableProperty] private InputBindingModel _inputSwitch01 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch02 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch03 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch04 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch05 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch06 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch07 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch08 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch09 = new();
	[ObservableProperty] private InputBindingModel _inputSwitch10 = new();
	
	[ObservableProperty] private InputBindingModel _radioNext = new();
	[ObservableProperty] private InputBindingModel _radioPrevious = new();
	
	[ObservableProperty] private InputBindingModel _inputIntercomPtt = new();
	[ObservableProperty] private InputBindingModel _inputPushToTalk = new();
	
	[ObservableProperty] private InputBindingModel _awacsOverlayToggle = new();
	[ObservableProperty] private InputBindingModel _overlayToggle = new();

	[ObservableProperty] private InputBindingModel _inputUp100 = new();
	[ObservableProperty] private InputBindingModel _inputUp10 = new();
	[ObservableProperty] private InputBindingModel _inputUp1 = new();
	[ObservableProperty] private InputBindingModel _inputUp01 = new();
	[ObservableProperty] private InputBindingModel _inputUp001 = new();
	[ObservableProperty] private InputBindingModel _inputUp0001 = new();

	[ObservableProperty] private InputBindingModel _inputDown100 = new();
	[ObservableProperty] private InputBindingModel _inputDown10 = new();
	[ObservableProperty] private InputBindingModel _inputDown1 = new();
	[ObservableProperty] private InputBindingModel _inputDown01 = new();
	[ObservableProperty] private InputBindingModel _inputDown001 = new();
	[ObservableProperty] private InputBindingModel _inputDown0001 = new();
	
	[ObservableProperty] private InputBindingModel _transponderIdent = new();
	[ObservableProperty] private InputBindingModel _guardToggle = new();
	[ObservableProperty] private InputBindingModel _encryptionToggle = new();
	[ObservableProperty] private InputBindingModel _encryptionKeyUp = new();
	[ObservableProperty] private InputBindingModel _encryptionKeyDown = new();
	
	[ObservableProperty] private InputBindingModel _radioChannelUp = new();
	[ObservableProperty] private InputBindingModel _radioChannelDown = new();
	
	[ObservableProperty] private InputBindingModel _radioVolumeUp = new();
	[ObservableProperty] private InputBindingModel _radioVolumeDown = new();
	
	#endregion
	
	[JsonIgnore] // Make a list of All InputBindingModel's with the Magic of LINQ
	public List<InputBindingModel> InputBindingsList => 
		GetType().GetProperties()
			.Where(property => property.PropertyType == typeof(InputBindingModel))
			.Select(x => (InputBindingModel)x.GetValue(this))
			.ToList();

	/// <summary>
	/// A list of All InputBindingModel's that are special/handled by the UDPVoiceHandler instead of InputDeviceManager.
	/// </summary>
	[JsonIgnore] 
	public List<InputBindingModel> RadioAndPttBindingsList => new()
	{
		InputIntercom,
		InputSwitch01, InputSwitch02, InputSwitch03, InputSwitch04, InputSwitch05, 
		InputSwitch06, InputSwitch07, InputSwitch08, InputSwitch09, InputSwitch10, 
		InputIntercomPtt, InputPushToTalk
	};

	[JsonIgnore]
	public List<float> RadioBalanceList => new(){
		BalanceIntercom, 
		BalanceRadio01, BalanceRadio02, BalanceRadio03, BalanceRadio04, BalanceRadio05, 
		BalanceRadio06, BalanceRadio07, BalanceRadio08, BalanceRadio09, BalanceRadio10
	};

	/// <summary>
	/// Todo: Move to portable Delegates instead of strictly coded actions
	/// </summary>
	[JsonIgnore] 
	public Dictionary<InputBindingModel, Action> InputActionList => new()
	{
		{ InputIntercom, OnInputIntercomPressed },
		{ InputSwitch01, OnInputSwitch01Pressed },
		{ InputSwitch02, OnInputSwitch02Pressed },
		{ InputSwitch03, OnInputSwitch03Pressed },
		{ InputSwitch04, OnInputSwitch04Pressed },
		{ InputSwitch05, OnInputSwitch05Pressed },
		{ InputSwitch06, OnInputSwitch06Pressed },
		{ InputSwitch07, OnInputSwitch07Pressed },
		{ InputSwitch08, OnInputSwitch08Pressed },
		{ InputSwitch09, OnInputSwitch09Pressed },
		{ InputSwitch10, OnInputSwitch10Pressed },
		
		{ RadioNext, OnRadioNextPressed },
		{ RadioPrevious, OnRadioPreviousPressed },
		
		{ InputIntercomPtt, OnInputIntercomPttPressed },
		{ InputPushToTalk, OnInputPushToTalkPressed },
		
		{ AwacsOverlayToggle, OnAwacsOverlayTogglePressed },
		{ OverlayToggle, OnOverlayTogglePressed },
		
		{ InputUp100, OnInputUp100Pressed },
		{ InputUp10, OnInputUp10Pressed },
		{ InputUp1, OnInputUp1Pressed },
		{ InputUp01, OnInputUp01Pressed },
		{ InputUp001, OnInputUp001Pressed },
		{ InputUp0001, OnInputUp0001Pressed },
		
		{ InputDown100, OnInputDown100Pressed },
		{ InputDown10, OnInputDown10Pressed },
		{ InputDown1, OnInputDown1Pressed },
		{ InputDown01, OnInputDown01Pressed },
		{ InputDown001, OnInputDown001Pressed },
		{ InputDown0001, OnInputDown0001Pressed },
		
		{ TransponderIdent, OnTransponderIdentPressed },
		{ GuardToggle, OnGuardTogglePressed },
		{ EncryptionToggle, OnEncryptionTogglePressed },
		{ EncryptionKeyUp, OnEncryptionKeyUpPressed },
		{ EncryptionKeyDown, OnEncryptionKeyDownPressed },
		
		{ RadioChannelUp, OnRadioChannelUpPressed },
		{ RadioChannelDown, OnRadioChannelDownPressed },
		
		{ RadioVolumeUp, OnRadioVolumeUpPressed },
		{ RadioVolumeDown, OnRadioVolumeDownPressed },
	};
	
	private static void OnInputIntercomPressed() { }

	private static void OnInputSwitch01Pressed() { }

    private static void OnInputSwitch02Pressed() { }

    private static void OnInputSwitch03Pressed() { }

    private static void OnInputSwitch04Pressed() { }

    private static void OnInputSwitch05Pressed() { }

    private static void OnInputSwitch06Pressed() { }

    private static void OnInputSwitch07Pressed() { }

    private static void OnInputSwitch08Pressed() { }

    private static void OnInputSwitch09Pressed() { }
    private static void OnInputSwitch10Pressed() { }

    private static void OnRadioNextPressed() { }

    private static void OnRadioPreviousPressed() { }

    private static void OnInputIntercomPttPressed() { }

    private static void OnInputPushToTalkPressed() { }

    private static void OnAwacsOverlayTogglePressed() { }

    private static void OnOverlayTogglePressed() { }

    private static void OnInputUp100Pressed() { }

    private static void OnInputUp10Pressed() { }

    private static void OnInputUp1Pressed() { }

    private static void OnInputUp01Pressed() { }

    private static void OnInputUp001Pressed() { }

    private static void OnInputUp0001Pressed() { }

    private static void OnInputDown100Pressed() { }

    private static void OnInputDown10Pressed() { }

    private static void OnInputDown1Pressed() { }

    private static void OnInputDown01Pressed() { }

    private static void OnInputDown001Pressed() { }

    private static void OnInputDown0001Pressed() { }

    private static void OnTransponderIdentPressed() { }

    private static void OnGuardTogglePressed() { }

    private static void OnEncryptionTogglePressed() { }

    private static void OnEncryptionKeyUpPressed() { }

    private static void OnEncryptionKeyDownPressed() { }

    private static void OnRadioChannelUpPressed() { }
    private static void OnRadioChannelDownPressed() { }

    private static void OnRadioVolumeUpPressed() { }

    private static void OnRadioVolumeDownPressed() { }
    
    public object Clone()
    {
	    return this.MemberwiseClone();
    }
}