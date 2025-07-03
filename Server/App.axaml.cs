using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		Properties.Resources.Culture = CultureInfo.CurrentUICulture;
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new UI.MainWindow()
			{
				DataContext = new MainViewModel(),
			};
		}

		base.OnFrameworkInitializationCompleted();
	}
}