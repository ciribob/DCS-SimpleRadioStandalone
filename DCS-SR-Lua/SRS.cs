using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Lua
{
    public sealed class SRS
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        SRS()
        {
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(Path.Combine([GetSRSPath(), "Client", "NLog.config"]));
            Logger.Info("SRS Lua Library loaded.");
        }

        public static readonly SRS Instance = new();

        static Native.Register[] registry = [
            State.CreateRegister("start_srs", Start_SRS),
            State.CreateRegister("get_srs_path", Get_SRS_Path),
            new()
        ];

        [UnmanagedCallersOnly(EntryPoint = "luaopen_srs")]
        public static int LuaOpen(IntPtr stateIn)
        {
            var lua = new State(stateIn);
            try
            {
                Logger.Trace("SRS LuaOpen for state {stateIn}", stateIn);
                lua.Register("srs", registry);

                // push constants.
                lua.Push(UpdaterChecker.VERSION);
                lua.SetField(-2, "VERSION");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error in open");
                lua.Push(e.Message);
                return Native.lua_error(lua.Handle);
            }
            return 1;
        }

        static string GetSRSPath()
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\DCS-SR-Standalone", "SRPathStandalone", "")?.ToString();
        }
        static int Start_SRS(IntPtr state)
        {
            var lua = new State(state);
            try
            {
                var host = lua.CheckString(1);
                Logger.Trace("start_srs({host})", host);
                if (IsRunning())
                {
                    Logger.Debug("SRS already running.");
                    lua.Push(false);
                    return 1;
                }


                var path = Path.Combine([GetSRSPath(), "Client"]);
                Logger.Info("Launching SRS at {path}", path);
                using var proc = Process.Start(new ProcessStartInfo
                {
                    WorkingDirectory = path,
                    FileName = Path.Combine([path, "SR-ClientRadio"]),
                    Arguments = $"-host={host}"
                });

                lua.Push(proc.StartTime.Ticks > 0);
            }
            catch (Exception e)
            {
                Logger.Error(e, "start_srs");
                lua.Push(e.Message);
                return Native.lua_error(lua.Handle);
            }
            
            return 1;
        }

        static bool IsRunning()
        {
            var instances = Process.GetProcessesByName("sr-clientradio");
            // Immediately dispose or we leak
            // https://github.com/mono/mono/issues/10143
            foreach (var instance in instances)
            {
                instance.Dispose();
            }
            return instances.Length > 0;
        }

        static int Get_SRS_Path(IntPtr state)
        {
            var lua = new State(state);
            try
            {
                Logger.Trace("get_srs_path()");
                var srsPath = GetSRSPath();
                Logger.Trace(srsPath);
                lua.Push(srsPath);
            }
            catch (Exception e)
            {
                Logger.Error(e, "get_srs_path");
                lua.Push(e.Message);
                return Native.lua_error(lua.Handle);
            }

            return 1;
        }
    }
}
