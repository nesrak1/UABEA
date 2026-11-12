using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace UABEA.Mobile;

public sealed class App : Avalonia.Application
{
    public static MainView? CurrentMainView { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Default;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            CurrentMainView = new MainView();
            singleViewLifetime.MainView = CurrentMainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}