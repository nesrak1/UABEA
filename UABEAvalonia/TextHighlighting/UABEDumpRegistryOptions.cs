using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace UABEAvalonia.TextHighlighting
{
    internal class UABEDumpRegistryOptions : IRegistryOptions
    {
        const string GrammarPrefix = "TextMateSharp.Grammars.Resources.Grammars.";
        const string ThemesPrefix = "TextMateSharp.Grammars.Resources.Themes.";

        private ThemeName _defaultTheme;

        public UABEDumpRegistryOptions(ThemeName defaultTheme)
        {
            _defaultTheme = defaultTheme;
        }

        public IRawTheme GetDefaultTheme()
        {
            return LoadTheme(_defaultTheme);
        }

        public IRawGrammar GetGrammar(string scopeName)
        {
            Assembly assembly = typeof(UABEDumpRegistryOptions).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream("UABEAvalonia.Grammars.utxt.syntaxes.utxt.tmLanguage.json");
            if (stream == null)
            {
                throw new Exception("Couldn't read utxt grammar!");
            }

            IRawGrammar dbg = GrammarReader.ReadGrammarSync(new StreamReader(stream));
            string scope = dbg.GetScopeName();
            return dbg;
        }

        public ICollection<string> GetInjections(string scopeName)
        {
            return null;
        }

        public IRawTheme GetTheme(string scopeName)
        {
            Assembly assembly = typeof(RegistryOptions).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(ThemesPrefix + scopeName.Replace("./", string.Empty));
            if (stream == null)
            {
                return null;
            }

            using (StreamReader reader = new StreamReader(stream))
            {
                return ThemeReader.ReadThemeSync(reader);
            }
        }

        public IRawTheme LoadTheme(ThemeName name)
        {
            return GetTheme(GetThemeFile(name));
        }

        private static string GetThemeFile(ThemeName name)
        {
            return name switch
            {
                ThemeName.Abbys => "abyss-color-theme.json",
                ThemeName.Dark => "dark_vs.json",
                ThemeName.DarkPlus => "dark_plus.json",
                ThemeName.DimmedMonokai => "dimmed-monokai-color-theme.json",
                ThemeName.KimbieDark => "kimbie-dark-color-theme.json",
                ThemeName.Light => "light_vs.json",
                ThemeName.LightPlus => "light_plus.json",
                ThemeName.Monokai => "monokai-color-theme.json",
                ThemeName.QuietLight => "quietlight-color-theme.json",
                ThemeName.Red => "Red-color-theme.json",
                ThemeName.SolarizedDark => "solarized-dark-color-theme.json",
                ThemeName.SolarizedLight => "solarized-light-color-theme.json",
                ThemeName.TomorrowNightBlue => "tomorrow-night-blue-color-theme.json",
                ThemeName.HighContrastLight => "hc_light.json",
                ThemeName.HighContrastDark => "hc_black.json",
                _ => throw new KeyNotFoundException("Not a valid theme!"),
            };
        }
    }
}
