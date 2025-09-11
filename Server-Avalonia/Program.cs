using Avalonia;
using System;
using Sentry;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		SentrySdk.Init("https://0935ffeb7f9c46e28a420775a7f598f4@o414743.ingest.sentry.io/5315043");
		
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
}