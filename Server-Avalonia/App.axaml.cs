using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Application = Avalonia.Application;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using LogManager = NLog.LogManager;

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
			//TODO command line args
			//var args = desktop.Args;
			
			desktop.MainWindow = new View.MainWindow()
			{
				DataContext = vm
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	#region Logging
	private void SetupLogging()
	{
		// If there is a configuration file then this will already be set
		if (LogManager.Configuration != null)
		{
			return;
		}

		var config = new LoggingConfiguration();
		var fileTarget = new FileTarget
		{
			FileName = "serverlog.txt",
			ArchiveFileName = "serverlog.old.txt",
			MaxArchiveFiles = 1,
			ArchiveAboveSize = 104857600,
			Layout =
				@"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=1}"
		};

		var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
		config.AddTarget("asyncFileTarget", wrapper);
		config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, wrapper));

		// only add transmission logging at launch if its enabled, defer rule and target creation otherwise
		if (ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue)
		{
			config = LoggingHelper.GenerateTransmissionLoggingConfig(config,
				ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue);
		}

		LogManager.Configuration = config;
	}
	#endregion
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