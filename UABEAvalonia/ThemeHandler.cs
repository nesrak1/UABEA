using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public static class ThemeHandler
    {
        private static bool _useDarkTheme;
        public static bool UseDarkTheme
        {
            get
            {
                return _useDarkTheme;
            }
            set
            {
                if (Application.Current == null)
                    return;

                Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
                _useDarkTheme = value;
            }
        }
    }
}
