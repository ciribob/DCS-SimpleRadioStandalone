using System;
using System.Reflection;
using Avalonia.Controls;
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
		
		InitializeComponent();
	}
	
	protected override void OnClosed(EventArgs e)
	{
		ViewModel.StopServerCommand.Execute(null);
		
		base.OnClosed(e);
	}

}