using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;
using Microsoft.Extensions.DependencyInjection;
using Application = Avalonia.Application;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// If you use CommunityToolkit, line below is needed to remove Avalonia data validation.
		// Without this line you will get duplicate validations from both Avalonia and CT
		BindingPlugins.DataValidators.RemoveAt(0);
		
		var collection = new ServiceCollection();
		collection.AddCommonServices();
		
		var services = collection.BuildServiceProvider();
		
		Properties.Resources.Culture = CultureInfo.CurrentUICulture;
		
		var vm = services.GetRequiredService<MainViewModel>();
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			//var args = desktop.Args;
			
			desktop.MainWindow = new View.MainWindow()
			{
				DataContext = vm
			};
		}

		base.OnFrameworkInitializationCompleted();
	}
}

public static class ServiceCollectionExtensions
{
	public static void AddCommonServices(this IServiceCollection collection)
	{
		collection.AddSingleton<IEventAggregator, EventAggregator>();
		collection.AddSingleton<ServerState>();
		collection.AddSingleton<ServerSettingsStore>();
		
		// View Models
		collection.AddTransient<MainViewModel>();

		collection.AddTransient<ServerSettingsModel>();
	}
}