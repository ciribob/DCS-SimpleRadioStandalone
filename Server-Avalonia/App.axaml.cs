using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Application = Avalonia.Application;
using IServiceProvider = System.IServiceProvider;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

public class App : Application
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
		
		Ioc.Default.ConfigureServices(collection.BuildServiceProvider());
		
		Properties.Resources.Culture = CultureInfo.CurrentUICulture;
		
		var vm =  Ioc.Default.GetRequiredService<MainViewModel>();
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
		var _temp = ServerSettingsStore.Instance.GetServerPort();
		
		collection.AddSingleton<ServerSettingsStore>(ServerSettingsStore.Instance);
		collection.AddSingleton<IEventAggregator, EventAggregator>();
		collection.AddSingleton<ServerStateModel>();
		
		collection.AddTransient<ServerSettingsModel>();
		
		// View Models
		collection.AddTransient<MainViewModel>();
	}
}