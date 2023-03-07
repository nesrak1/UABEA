using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class ConfigurationManager
    {
        public const string CONFIG_FILENAME = "config.json";
        public static ConfigurationSettings Settings { get; }
        static ConfigurationManager()
        {
            if (!File.Exists(CONFIG_FILENAME))
            {
                Settings = new ConfigurationSettings()
                {
                    UseDarkTheme = false,
                    UseCpp2Il = true
                };
            }
            else
            {
                string configText = File.ReadAllText(CONFIG_FILENAME);
                Settings = JsonConvert.DeserializeObject<ConfigurationSettings>(configText) ?? new ConfigurationSettings();
            }
        }

        public static void SaveConfig()
        {
            if (Settings != null) // ConfigLoaded
            {
                string configText = JsonConvert.SerializeObject(Settings);
                File.WriteAllText(CONFIG_FILENAME, configText);
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
