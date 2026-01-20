using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Application = Avalonia.Application;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

public class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// line below is needed to remove Avalonia data validation, due to MVVM toolkit being in use.
		// Without this line you will get duplicate validations from both Avalonia and CT
		BindingPlugins.DataValidators.RemoveAt(0);

		Properties.Resources.Culture = CultureInfo.CurrentUICulture;
		
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			//TODO command line args
			//var args = desktop.Args;
			
			desktop.MainWindow = new View.MainWindow()
			{
				DataContext = Ioc.Default.GetRequiredService<MainViewModel>()
			};
		}

		base.OnFrameworkInitializationCompleted();
	}


}

