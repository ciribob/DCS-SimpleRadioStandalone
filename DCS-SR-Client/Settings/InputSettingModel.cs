using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;


public partial class InputSettingModel : ObservableObject, ICloneable
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage());
		base.OnPropertyChanging(e);
	}
	
	[ObservableProperty] private string _inputName = "InputName";
	[ObservableProperty] private InputModel _primary = new InputModel();
	[ObservableProperty] private InputModel _modifier = new InputModel();
	
	
	public object Clone()
	{
		return this.MemberwiseClone();
	}
}

public partial class InputModel : ObservableObject, ICloneable
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage());
		base.OnPropertyChanging(e);
	}
		
	[ObservableProperty] private Guid _guid = Guid.Empty;
	[ObservableProperty] private string _deviceName = string.Empty;
	[ObservableProperty] private int _button = (int)0;
	public int ButtonValue { get; internal set; } = (int)0;
	
	public object Clone()
	{
		return this.MemberwiseClone();
	}
}
