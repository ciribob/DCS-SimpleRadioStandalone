using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.View;

public partial class MainWindow : Window
{
	private NumberFormatInfo _freqencyFormat = new NumberFormatInfo()
	{
		NumberDecimalSeparator = ".",
		NumberGroupSeparator = ",",
		NumberDecimalDigits = 3,
	};
	
	private MainViewModel ViewModel { 
		get => (MainViewModel)this.DataContext!; 
		set => DataContext = value;
	}

	public MainWindow()
	{
		Title = string.Concat("SRS Server - ", Assembly.GetExecutingAssembly().GetName().Version.ToString() );
		
		// Set Start stop button to: {Properties.Resources.BtnStopServer}" : $"{Properties.Resources.BtnStartServer}
		
		InitializeComponent();
	}

	private void TestFrequenciesAddButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if (TestFrequencyTextBox.Text == null) return;
		if (double.TryParse(TestFrequencyTextBox.Text, out double value))
		{
			ViewModel.ServerSettings.TestFrequencies.Add(value);
		}
		TestFrequencyTextBox.Text = string.Empty;
	}

	private void TestFrequenciesRemoveButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Button)?.Tag is double toRemove)
		{
			ViewModel.ServerSettings.TestFrequencies.Remove(toRemove);
		}
	}

	private void GlobalFrequenciesAddButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if (GlobalFrequencyTextBox.Text == null) return;		
		if (double.TryParse(GlobalFrequencyTextBox.Text, out double value))
		{
			ViewModel.ServerSettings.GlobalLobbyFrequencies.Add(value);
		}
		GlobalFrequencyTextBox.Text = string.Empty;
	}
	
	private void GlobalFrequenciesRemoveButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Button)?.Tag is double toRemove)
		{
			ViewModel.ServerSettings.GlobalLobbyFrequencies.Remove(toRemove);
		}
	}

	private void ClientExportPathButton_OnClick(object? sender, RoutedEventArgs e)
	{
		Task<string> task = Task.Run(() =>  OpenFolderPicker("Client Export Path"));
		if (task.Exception != null)
		{
			ViewModel.ServerSettings.ClientExportFilePath = Path.Combine( task.Result, "clients-list.json");
		}
	}
	
	private void ServerPresetsPathButton_OnClick(object? sender, RoutedEventArgs e)
	{
		Task<string> task = Task.Run(() =>  OpenFolderPicker("Server Presets Path"));
		if (task.Exception != null)
		{
			ViewModel.ServerSettings.ServerPresetsPath = task.Result;
		}
	}
	
	private async Task<string> OpenFolderPicker(string title, string? exitingFolder = null)
	{
		FolderPickerOpenOptions options = new()
		{
			Title = title, 
			AllowMultiple = false
		};

		try
		{
			IReadOnlyList<IStorageFolder> folder = await GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(options);
			return folder[0].Path.AbsolutePath;
		}
		catch (Exception e)
		{
			// If the picker gets cancelled, use existing or empty (but not null):
			return exitingFolder ?? string.Empty;
		}
	}


}