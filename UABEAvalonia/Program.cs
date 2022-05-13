using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace UABEAvalonia
{
    class Program
    {
        //https://stackoverflow.com/a/37146916
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(int dwProcessId);

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            bool usesConsole = false;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                usesConsole = AttachConsole(-1);

                if (usesConsole)
                {
                    (int Left, int Top) = Console.GetCursorPosition();
                    Console.SetCursorPosition(0, Top);
                    Console.Write(new string(' ', Left));
                    Console.SetCursorPosition(0, Top);
                }
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                //outputs fine to console already with dotnet in my testing
                usesConsole = true;
            }

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UABEAExceptionHandler);

            if (args.Length > 0)
            {
                CommandLineHandler.CLHMain(args);
            }
            else
            {
                if (usesConsole)
                    CommandLineHandler.PrintHelp();
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        public static void UABEAExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception ex)
            {
                File.WriteAllText("uabeacrash.log", ex.ToString());
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
