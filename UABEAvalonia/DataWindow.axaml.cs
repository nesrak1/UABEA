using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace UABEAvalonia
{
    public partial class DataWindow : Window
    {
        private InfoWindow win;
        private AssetWorkspace workspace;

        public DataWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            Closing += DataWindow_Closing;
        }

        public DataWindow(InfoWindow win, AssetWorkspace workspace, AssetContainer cont) : this()
        {
            this.win = win;
            this.workspace = workspace;

            SetWindowTitle(workspace, cont);

            treeView.Init(win, workspace);
            treeView.LoadComponent(cont);
        }

        private void SetWindowTitle(AssetWorkspace workspace, AssetContainer cont)
        {
            Extensions.GetUABENameFast(workspace, cont, false, out string assetName, out string typeName);
            if (assetName == "Unnamed asset")
                Title += $": {typeName} ({cont.FileInstance.name}/{cont.PathId})";
            else
                Title += $": {typeName} {assetName} ({cont.FileInstance.name}/{cont.PathId})";
        }

        private void DataWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            treeView.Items = null;
        }
    }
}
