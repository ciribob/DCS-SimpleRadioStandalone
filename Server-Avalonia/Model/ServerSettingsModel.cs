using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

public partial class ServerSettingsModel : ServerSettingsStore
{
	private  IEventAggregator EventAggregator => Ioc.Default.GetRequiredService<IEventAggregator>();
	
	protected override void OnPropertyChanged(string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);
		
		// Any time a property changes here, send this signal.
		EventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
		if (propertyName is nameof(TestFrequencies) or nameof(GlobalLobbyFrequencies))
		{
			EventAggregator.PublishOnBackgroundThreadAsync(
				new ServerFrequenciesChanged()
				{
					TestFrequencies = string.Join(",", TestFrequencies),
					GlobalLobbyFrequencies = string.Join(",", GlobalLobbyFrequencies)
				});
		}
	}
	
	[RelayCommand]
	private void TestFrequencyAdd(double value)
	{
		var temp = TestFrequencies;
		temp.Add(value);
		TestFrequencies = temp;
	}
	[RelayCommand]
	private void TestFrequencyRemove(double value)
	{
		var temp = TestFrequencies;
		temp.Remove(value);
		TestFrequencies = temp;
	}
	[RelayCommand]
	private void GlobalFrequencyAdd(double value)
	{
		var temp = GlobalLobbyFrequencies;
		temp.Add(value);
		GlobalLobbyFrequencies = temp;
	}
	[RelayCommand]
	private void GlobalFrequencyRemove(double value)
	{
		var temp = GlobalLobbyFrequencies;
		temp.Remove(value);
		GlobalLobbyFrequencies = temp;
	}
}