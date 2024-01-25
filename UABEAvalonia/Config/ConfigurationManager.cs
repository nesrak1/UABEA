using Newtonsoft.Json;
using System;
using System.IO;

namespace UABEAvalonia
{
    public static class ConfigurationManager
    {
        public const string CONFIG_FILENAME = "config.json";
        public static ConfigurationSettings Settings { get; }
        static ConfigurationManager()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
            if (!File.Exists(configPath))
            {
                Settings = new ConfigurationSettings()
                {
                    UseDarkTheme = false,
                    UseCpp2Il = true
                };
            }
            else
            {
                string configText = File.ReadAllText(configPath);
                Settings = JsonConvert.DeserializeObject<ConfigurationSettings>(configText) ?? new ConfigurationSettings();
            }
        }

        public static void SaveConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
            if (Settings != null) // ConfigLoaded
            {
                string configText = JsonConvert.SerializeObject(Settings);
                File.WriteAllText(configPath, configText);
            }
        }
    }

    public class ConfigurationSettings
    {
        private bool _useDarkTheme;
        public bool UseDarkTheme
        {
            get => _useDarkTheme;
            set
            {
                _useDarkTheme = value;
                ConfigurationManager.SaveConfig();
            }
        }

        private bool _useCpp2Il;
        public bool UseCpp2Il
        {
            get => _useCpp2Il;
            set
            {
                _useCpp2Il = value;
                ConfigurationManager.SaveConfig();
            }
        }
    }
}
