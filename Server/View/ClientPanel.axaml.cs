using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.View;

public partial class ClientPanel : Panel
{
	private MainViewModel ViewModel { 
		get => (MainViewModel)this.DataContext!; 
		set => DataContext = value;
	}
	
	public ClientPanel()
	{
		InitializeComponent();
	}

	private void Confirm_OnClick(object? sender, RoutedEventArgs e)
	{
		if ((sender as Button)?.Tag is double toRemove)
		{
			ViewModel.ServerSettings.GlobalLobbyFrequencies.Remove(toRemove);
		}
	}
}