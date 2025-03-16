using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
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
	[ObservableProperty] private InputDevice _primary = null;
	[ObservableProperty] private InputDevice _modifier = null;
	[JsonIgnore] public Action BindingAction { get; init; }
	
	/// <summary>
	/// Is active when Primary (and Modifier, if not null) is pressed.
	/// </summary>
	public bool IsBindingPressed
	{
		get
		{
			if (HasModifier)
			{
				return IsPrimaryPressed && IsModifiedPressed;
			}
			return IsPrimaryPressed;
		}
	}
	
	public bool IsPrimaryPressed { get; set; }
	public bool IsModifiedPressed { get; set; }
	
#pragma warning disable MVVMTK0034
	// We need this to easily set Binding Pressed.
	public bool HasModifier => _modifier != null;
#pragma warning restore MVVMTK0034
	
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
