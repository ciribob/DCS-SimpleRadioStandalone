using System;
using Microsoft.Win32;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    public enum SettingType
    {
        RadioEffects = 0,
        Radio1Channel = 1,
        Radio2Channel = 2,
        Radio3Channel = 3,
        RadioSwitchIsPTT = 4,
        IntercomChannel = 5,
    }


    public class Settings
    {
        private static Settings _instance;

        public Settings()
        {
            UserSettings = new string[Enum.GetValues(typeof(SettingType)).Length];

            foreach (SettingType set in Enum.GetValues(typeof(SettingType)))
            {
                UserSettings[(int) set] = ReadSetting(set);
            }
        }

        public string[] UserSettings { get; }

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Settings();
                }
                return _instance;
            }
        }


        public string ReadSetting(SettingType settingType)
        {
            try
            {
                var setting = (string) Registry.GetValue(InputConfiguration.RegPath,
                    settingType + "_setting",
                    "");
                return setting;
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public void WriteSetting(SettingType settingType, string setting)
        {
            try
            {
                Registry.SetValue(InputConfiguration.RegPath,
                    settingType + "_setting",
                    setting);

                UserSettings[(int) settingType] = setting;
            }
            catch (Exception ex)
            {
            }
        }
    }
}