using System;
using Microsoft.Win32;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server
{
    public enum ServerSettingType
    {
        COALITION_AUDIO_SECURITY = 0,
        SPECTATORS_AUDIO_DISABLED = 1,
        CLIENT_EXPORT_ENABLED = 2,
        LOS_ENABLED = 3,
        DISTANCE_ENABLED = 4,
        IRL_RADIO_TX = 5
    }

    public class ServerSettings
    {
        public static readonly string REG_PATH = "HKEY_CURRENT_USER\\SOFTWARE\\DCS-SimpleRadioStandalone";

        private static ServerSettings instance;

        public ServerSettings()
        {
            ServerSetting = new string[Enum.GetValues(typeof(ServerSettingType)).Length];

            foreach (ServerSettingType set in Enum.GetValues(typeof(ServerSettingType)))
            {
                ServerSetting[(int) set] = ReadSetting(set);
            }
        }

        public string[] ServerSetting { get; }

        public static ServerSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ServerSettings();
                }
                return instance;
            }
        }

        public string ReadSetting(ServerSettingType settingType)
        {
            try
            {
                var setting = (string) Registry.GetValue(REG_PATH,
                    settingType + "_setting",
                    "");
                return setting;
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public void WriteSetting(ServerSettingType settingType, string setting)
        {
            try
            {
                Registry.SetValue(REG_PATH,
                    settingType + "_setting",
                    setting);

                ServerSetting[(int) settingType] = setting;
            }
            catch (Exception ex)
            {
            }
        }
    }
}