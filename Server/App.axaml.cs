using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
		
		// Register all the services needed for the application to run
		var collection = new ServiceCollection();
		collection.AddCommonServices();

		// Creates a ServiceProvider containing services from the provided IServiceCollection
		var services = collection.BuildServiceProvider();
		
		Properties.Resources.Culture = CultureInfo.CurrentUICulture;
		
		var vm = services.GetRequiredService<MainViewModel>();
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var args = desktop.Args;
			
			desktop.MainWindow = new UI.MainWindow()
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
		collection.AddTransient<MainViewModel>();
	}
}