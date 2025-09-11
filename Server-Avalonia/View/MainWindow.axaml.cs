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
	private MainViewModel ViewModel { 
		get => (MainViewModel)this.DataContext!; 
		set => DataContext = value;
	}

	public MainWindow()
	{
		Title = string.Concat("SRS Server - ", Assembly.GetExecutingAssembly().GetName().Version!.ToString() );
		
		// Set Start stop button to: {Properties.Resources.BtnStopServer}" : $"{Properties.Resources.BtnStartServer}
		
		InitializeComponent();
	}


	protected override void OnClosed(EventArgs e)
	{
		ViewModel.StopServerCommand.Execute(null);
		
		base.OnClosed(e);
	}
}