using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.UI;

public partial class MainWindow : Window
{
	private MainViewModel ViewModel { get; }

	public MainWindow()
	{
		ViewModel = DataContext as MainViewModel ?? new();
		
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
		ViewModel.ServerSettings.GlobalLobbyFrequencies.Add(GlobalFrequencyTextBox.Text);
		GlobalFrequencyTextBox.Text = string.Empty;
	}
	
	private void GlobalFrequenciesRemoveButton_OnClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Button)?.Tag is string toRemove)
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
	
	private async Task<string> OpenFolderPicker(string title)
	{
		FolderPickerOpenOptions options = new()
		{
			Title = title, 
			AllowMultiple = false
		};
		
		IReadOnlyList<IStorageFolder> folder = await GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(options);
		if (folder == null) return string.Empty;
		
		return folder[0].Path.AbsolutePath;
	}


}