using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.View;

public partial class ClientPanel : Panel
{
	public ClientPanel()
	{
		InitializeComponent();
	}

	private void Confirm_OnClick(object? sender, RoutedEventArgs e)
	{
		Console.WriteLine("Confirm_OnClick");
	}
}