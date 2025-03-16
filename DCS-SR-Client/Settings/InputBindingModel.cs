using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;


public partial class InputBindingModel : ObservableObject, ICloneable
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage(SettingCatagory.Profile));
		base.OnPropertyChanging(e);
	}
	
	[ObservableProperty] private string _inputName = "InputName";
	[ObservableProperty] private InputDevice _primary = new InputDevice();
	[ObservableProperty] private InputDevice _modifier = new InputDevice();

	public bool IsEnabled { get; set; }
	public bool IsPrimaryPressed { get; set; }
	public bool IsModifiedPressed { get; set; }
	
	public object Clone()
	{
		return this.MemberwiseClone();
	}
}

public partial class InputDevice : ObservableObject, ICloneable
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage(SettingCatagory.Profile));
		base.OnPropertyChanging(e);
	}
	
	[ObservableProperty] private string _deviceName = string.Empty;
	[ObservableProperty] private int _button = (int)0;
	public int ButtonValue { get; internal set; } = (int)0;
	public Guid Guid { get; internal set; } = Guid.Empty;

	public object Clone()
	{
		return this.MemberwiseClone();
	}
	
	public bool IsSameBind(InputDevice compare)
	{
		return Button == compare.Button &&
		       compare.Guid == Guid &&
		       ButtonValue == compare.ButtonValue;
	}
}
