using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using UABEAvalonia.Plugins;

namespace UABEAvalonia
{
    public partial class PluginWindow : Window
    {
        private Window win;
        private AssetWorkspace workspace;
        private List<AssetContainer> selection;

        List<UABEAPluginMenuInfo> plugInfs;

        public PluginWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public PluginWindow(Window win, AssetWorkspace workspace, List<AssetContainer> selection, PluginManager plugLoader) : this()
        {
            this.win = win;
            this.workspace = workspace;
            this.selection = selection;

            plugInfs = plugLoader.GetPluginsThatSupport(workspace.am, selection);
            boxPluginList.Items = plugInfs;
        }

        private async void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var menuPlugInf = boxPluginList.SelectedItem as UABEAPluginMenuInfo;

            if (menuPlugInf == null)
            {
                Close(false);
                return;
            }

            var plugOpt = menuPlugInf.pluginOpt;
            await plugOpt.ExecutePlugin(win, workspace, selection);
            Close(true);
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
