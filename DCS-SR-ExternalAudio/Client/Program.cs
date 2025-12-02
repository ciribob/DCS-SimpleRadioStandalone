using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using CommandLine;
using NLog;


namespace Ciribob.DCS.SimpleRadio.Standalone.ExternalAudioClient.Client
{
    public class Program
    {
        const Int32 SW_MINIMIZE = 6;

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] Int32 nCmdShow);

        private static void MinimizeConsoleWindow()
        {
            IntPtr hWndConsole = GetConsoleWindow();
            ShowWindow(hWndConsole, SW_MINIMIZE);
        }

        private static void ConfigureLogging()
        {

            // If there is a configuration file then this will already be set
            if (LogManager.Configuration != null)
            {
                return;
            }

            var config = new NLog.Config.LoggingConfiguration();
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "${longdate}|${level:uppercase=true}|${message}";
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

            LogManager.Configuration = config;
        }

        public class Options
        {
            [Option('i', "file",
                SetName = "file",
                HelpText = "Full path to MP3 or Ogg - File must end .mp3 or .ogg",
                Required = true)]
            public string File { get; set; }

            [Option('t', "text",
                HelpText = "Text to say",
                SetName = "TTS",
                Required = true)]
            public string Text { get; set; }

            [Option('z', "ssml",
                HelpText = "Text is SSML - this is only for Google TTS",
                SetName = "TTS",
                Default = false,
                Required = false)]
            public bool SSML { get; set; }

            [Option('I', "textFile",
                SetName = "textFile",
                HelpText = "Path to text file for TTS ",
                Required = true)]
            public string TextFile { get; set; }

            [Option('f', "freqs",
                HelpText = "Frequency in MHz comma separated - 251.0,252.0 or just 252.0 ",
                Required = true)]
            public string Freqs { get; set; }


            [Option('m', "modulations",
                HelpText = "Modulation AM or FM comma separated - AM,FM or just AM  ",
                Required = true)]
            public string Modulations { get; set; }
            
            [Option("ambient",
                HelpText = "Ambient Cockpit - UH1 FA18 etc",
                Default = "",
                Required = false)]
            public string AmbientCockpit { get; set; }
            
            [Option("ambientVolume",
                HelpText = "Ambient Cockpit Volume",
                Required = false)]
            public float AmbientCockpitVolume { get; set; }
            
            [Option('c', "coalition",
                HelpText = "Coalition - 0 is Spectator, 1 is Red, 2 is Blue",
                Required = true)]
            public int Coalition { get; set; }

            [Option('s', "speed",
                Default = 1,
                HelpText = "Speed - 1 is normal -10 to 10 is the range",
                Required = false)]
            public int speed { get; set; }

            [Option('p', "port",
                HelpText = "Port - 5002 is the default",
                Default = 5002,
                Required = false)]
            public int Port { get; set; }

            [Option('n', "name",
                HelpText = "Name - name of your transmitter - no spaces",
                Default = "DCS-STTS",
                Required = false)]
            public string Name { get; set; }

            [Option('v', "volume",
                HelpText = "Volume - 1.0 is max, 0.0 is silence",
                Default = 1.0f,
                Required = false)]
            public float Volume { get; set; }

            [Option('l', "culture",
                HelpText = "TTS culture - local for the voice",
                Required = false,
                Default = "en-GB")]
            public string Culture { get; set; }

            [Option('g', "gender",
                HelpText = "TTS Gender - male/female",
                Required = false,
                Default = "female")]
            public string Gender { get; set; }

            [Option('V', "voice",
                HelpText = "The voice NAME - see the list from --help or if using google see: https://cloud.google.com/text-to-speech/docs/voices ",
                Required = false)]
            public string Voice { get; set; }

            [Option('h', "minimise",
                HelpText = "Minimise the command line window on run",
                Required = false,
                Default = false)]
            public bool Minimise { get; set; }

            [Option('G', "googleCredentials",
                HelpText = "Full path to Google JSON Credentials file - see https://cloud.google.com/text-to-speech/docs/quickstart-client-libraries",
                Required = false)]
            public string GoogleCredentials { get; set; }

            [Option('a', "azureCredentials",
                HelpText = "Use your Azure AI Speech key and region, separated by semicolon",
                Required = false)]
            public string AzureCredentials { get; set; }

            [Option('L', "latitude",
                HelpText = "Latitude of transmission",
                Required = false, Default = 0.0)]
            public double Latitude { get; set; }

            [Option('O', "longitude",
                HelpText = "Longitude of transmission",
                Required = false, Default = 0.0)]
            public double Longitude { get; set; }

            [Option('A', "altitude",
                HelpText = "Altitude of transmission in meters above sea level",
                Required = false, Default = 0.0)]
            public double Altitude { get; set; }

            [Option('R', "Record",
                HelpText = "Allow audio sent by ExternalAudio to be recorded by clients",
                Required = false, Default = true)]
            public bool Record { get; set; }
            // //
            // // [Option( "IP",
            // //     HelpText = "",
            // //     Required = false, Default = "127.0.0.1")]
            // public string IP { get; set; } = "127.0.0.1";

            [Option("unitId",
                HelpText =
                    "Sets the Unit ID of the transmitter - if you set this to the same as an aircraft you can then communicate over intercom with that aircraft",
                Required = false, Default = 1000u)]
            public uint UnitId { get; set; }
        }
        public static void Main(string[] args)
        {
            ConfigureLogging();

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(ProcessArgs).WithNotParsed(HandleParseError);
        }

        private static void ProcessArgs(Options opts)
        {
            if (opts.Minimise)
            {
                MinimizeConsoleWindow();
            }

            //process freqs
            var freqStr = opts.Freqs.Split(',');

            List<double> freqDouble = new List<double>();
            foreach (var s in freqStr)
            {
                freqDouble.Add(double.Parse(s, CultureInfo.InvariantCulture) * 1000000d);
            }

            var modulationStr = opts.Modulations.Split(',');

            List<Modulation> modulation = new List<Modulation>();
            foreach (var s in modulationStr)
            {
                Modulation mod;
                if (Modulation.TryParse(s.Trim().ToUpper(), out mod))
                {
                    modulation.Add(mod);
                }
            }

            if (modulation.Count != freqDouble.Count)
            {
                Console.WriteLine($"Number of frequencies ({freqDouble.Count}) does not match number of modulations ({modulation.Count}) - They must match!" +
                                  $"\n\nFor example: --freq=251.0,252.0 --modulations=AM,AM ");
                Console.WriteLine("QUITTING!");
            }

            ExternalAudioClient client = new ExternalAudioClient(freqDouble.ToArray(), modulation.ToArray(), opts);
            client.Start();
        }

        private static void HandleParseError(IEnumerable errs)
        {

            Console.WriteLine("");
            Console.WriteLine("Example:\n --file=\"C:\\FULL\\PATH\\TO\\File.mp3\" --freqs=251.0 --modulations=AM --coalition=1 --port=5002 --name=\"ciribob-robot\" --volume=0.5");
            Console.WriteLine("Example:\n --file=\"C:\\FULL\\PATH\\TO\\File.mp3\" --freqs=251.0 --modulations=AM --coalition=1 --port=5002 --name=\"ciribob-robot\" --volume=0.5 --latitude=50.82653 --longitude=-0.15210 --altitude=20");
            Console.WriteLine("Example:\n --text=\"I want this read out over this frequency - hello world! \" --freqs=251.0 --modulations=AM --coalition=1 --port=5002 --name=\"ciribob-robot\" --volume=0.5");
            Console.WriteLine("Example:\n --text=\"I want this read out over TWO frequencies - hello world! \" --freqs=251.0,252.0 --modulations=AM,AM --coalition=1 --port=5002 --name=\"ciribob-robot\" --volume=0.5");

            Console.WriteLine("");
            Console.WriteLine("Currently compatible voices on this system: \n");
            var synthesizer = new SpeechSynthesizer();
            foreach (var voice in synthesizer.GetInstalledVoices())
            {
                if (voice.Enabled)
                {
                    Console.WriteLine($"Name: {voice.VoiceInfo.Name}, Culture: {voice.VoiceInfo.Culture},  Gender: {voice.VoiceInfo.Gender}, Age: {voice.VoiceInfo.Age}, Desc: {voice.VoiceInfo.Description}");
                }
            }

            Console.WriteLine("");

            var first = synthesizer.GetInstalledVoices().First();
            Console.WriteLine($"Example:\n --text=\"I want a specific voice \" --freqs=251.0 --modulations=AM --coalition=1 --voice=\"{first.VoiceInfo.Name}\"");

            Console.WriteLine($"Example:\n --text=\"I want any female voice \" --freqs=251.0 --modulations=AM --coalition=1 --gender=female");

            Console.WriteLine($"Example:\n --text=\"I want any female voice at a location \" --freqs=251.0 --modulations=AM --coalition=1 --gender=female --latitude=50.82653 --longitude=-0.15210 --altitude=20");

            Console.WriteLine($"Example:\n --text=\"I want any male voice \" --freqs=251.0 --modulations=AM --coalition=1 --gender=male");

            Console.WriteLine("");
            Console.WriteLine("Google Cloud Text to Speech Examples - see locale and voices https://cloud.google.com/text-to-speech/docs/voices  : \n");

            Console.WriteLine($"Example:\n --text=\"Ahoj, jak se máš - Specific Czech voice\" --freqs=251.0 --modulations=AM --coalition=1 --googleCredentials=\"C:\\\\folder\\\\credentials.json\" --voice=\"cs-CZ-Wavenet-A\"");

            Console.WriteLine($"Example:\n --text=\"I want any female voice \" --freqs=251.0 --modulations=AM --coalition=1 --gender=female --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

            Console.WriteLine($"Example:\n --text=\"I want any male voice \" --freqs=251.0 --modulations=AM --coalition=1 --gender=male --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

            Console.WriteLine($"Example:\n --text=\"I want any male voice with a French accent \" --freqs=251.0 --modulations=AM --coalition=1 --gender=male --locale=fr-FR --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

            Console.WriteLine($"Example:\n --text=\"I want any female voice with a German accent \" --freqs=251.0 --modulations=AM --coalition=1 --gender=male --locale=de-DE --googleCredentials=\"C:\\\\folder\\\\credentials.json\" ");

            Console.WriteLine($"Example:\n --text=\"I want any female voice with a German accent \" --freqs=251.0 --modulations=AM --coalition=1 --gender=male --locale=de-DE --googleCredentials=\"xxxxxxxxxxxxxxxxxx;westeurope\" ");


        }

    }
}
