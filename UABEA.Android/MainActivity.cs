using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace UABEA.Mobile;

[Activity(
    Label = "UABEA Mobile",
    Theme = "@style/UABEATheme",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation |
                           ConfigChanges.ScreenSize |
                           ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity<App>
{
}
