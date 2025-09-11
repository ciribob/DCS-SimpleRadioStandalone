using Avalonia;
using System;
using System.Runtime;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
		var collection = new ServiceCollection();
		collection.AddCommonServices();
		Ioc.Default.ConfigureServices(collection.BuildServiceProvider());
		
		
		
		SentrySdk.Init("https://0935ffeb7f9c46e28a420775a7f598f4@o414743.ingest.sentry.io/5315043");
		
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
	
	
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
		collection.AddSingleton<ServerSettingsStore>(ServerSettingsStore.Instance);
		collection.AddSingleton<IEventAggregator, EventAggregator>();
		collection.AddSingleton<ServerStateModel>();
		
		collection.AddTransient<ServerSettingsModel>();
		
		// View Models
		collection.AddTransient<MainViewModel>();
	}
}