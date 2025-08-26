using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI;

public partial class MainWindow : Window
{
	private MainViewModel ViewModel { get; }
	
	public MainWindow(MainViewModel viewModelFromConstructor)
	{
		ViewModel = viewModelFromConstructor;
		InitializeComponent();
	}

	private void TestFrequenciesAddButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if (TestFrequencyTextBox.Text == null) return;
		ViewModel.ServerSettings.TestFrequencies.Add(TestFrequencyTextBox.Text);
		TestFrequencyTextBox.Text = string.Empty;
	}

	private void TestFrequenciesRemoveButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Button)?.Tag is string toRemove)
		{
			ViewModel.ServerSettings.TestFrequencies.Remove(toRemove);
		}
	}

	private void GlobalFrequenciesAddButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if (GlobalFrequencyTextBox.Text == null) return;
		ViewModel.ServerSettings.GlobalLobbyFrequencies.Add(TestFrequencyTextBox.Text);
		GlobalFrequencyTextBox.Text = string.Empty;
	}
	
	private void GlobalFrequenciesRemoveButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Button)?.Tag is string toRemove)
		{
			ViewModel.ServerSettings.GlobalLobbyFrequencies.Remove(toRemove);
		}
	}
}