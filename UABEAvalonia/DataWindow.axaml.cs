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
    public class DataWindow : Window
    {
        //controls
        private AssetDataTreeView treeView;
        private MenuItem menuVisitAsset;

        private InfoWindow win;
        private AssetWorkspace workspace;

        public DataWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            treeView = this.FindControl<AssetDataTreeView>("treeView");
            menuVisitAsset = this.FindControl<MenuItem>("menuVisitAsset");
            //generated events
            treeView.DoubleTapped += TreeView_DoubleTapped;
            menuVisitAsset.Click += MenuVisitAsset_Click;
            Closing += DataWindow_Closing;
        }

        public DataWindow(InfoWindow win, AssetWorkspace workspace, AssetContainer cont) : this()
        {
            this.win = win;
            this.workspace = workspace;

            treeView.Init(workspace);
            treeView.LoadComponent(cont);
        }

        private void TreeView_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (treeView.SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)treeView.SelectedItem;
                item.IsExpanded = !item.IsExpanded;
            }
        }

        private void MenuVisitAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)treeView.SelectedItem;
            if (item != null && item.Tag != null)
            {
                AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;
                win.SelectAsset(info.fromFile, info.fromPathId);
            }
        }

        private void DataWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            treeView.Items = null;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
