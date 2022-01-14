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
        private TreeView treeView;
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
            treeView = this.FindControl<TreeView>("treeView");
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

            AssetTypeValueField baseField = workspace.GetBaseField(cont);
            TreeViewItem baseItem = CreateTreeItem($"{baseField.GetFieldType()} {baseField.GetName()}");

            TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
            baseItem.Items = new List<TreeViewItem>() { arrayIndexTreeItem };
            treeView.Items = new List<TreeViewItem>() { baseItem };
            SetTreeItemEvents(baseItem, cont.FileInstance, cont.PathId, baseField);
        }

        private void TreeView_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)treeView.SelectedItem;
            item.IsExpanded = !item.IsExpanded;
        }

        private void MenuVisitAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)treeView.SelectedItem;
            if (item != null && item.Tag != null)
            {
                TreeViewItemInfo info = (TreeViewItemInfo)item.Tag;
                win.SelectAsset(info.fromFile, info.fromPathId);
            }
        }

        private void DataWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            treeView.Items = null;
        }

        private TreeViewItem CreateTreeItem(string text)
        {
            return new TreeViewItem() { Header = text };
        }

        //lazy load tree items. avalonia is really slow to load if
        //we just throw everything in the treeview at once
        private void SetTreeItemEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetTypeValueField field)
        {
            item.Tag = new TreeViewItemInfo(fromFile, fromPathId);
            //avalonia's treeviews have no Expanded event so this is all we can do
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(isExpanded =>
            {
                TreeViewItemInfo itemInfo = (TreeViewItemInfo)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again
                    TreeLoad(fromFile, field, fromPathId, item);
                }
            });
        }

        private void SetPPtrEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetContainer cont)
        {
            item.Tag = new TreeViewItemInfo(fromFile, fromPathId);
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(isExpanded =>
            {
                TreeViewItemInfo itemInfo = (TreeViewItemInfo)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again

                    if (cont != null)
                    {
                        AssetTypeValueField baseField = workspace.GetBaseField(cont);
                        TreeViewItem baseItem = CreateTreeItem($"{baseField.GetFieldType()} {baseField.GetName()}");

                        TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
                        baseItem.Items = new List<TreeViewItem>() { arrayIndexTreeItem };
                        item.Items = new List<TreeViewItem>() { baseItem };
                        SetTreeItemEvents(baseItem, cont.FileInstance, fromPathId, baseField);
                    }
                    else
                    {
                        item.Items = new List<TreeViewItem>() { CreateTreeItem("[null asset]") };
                    }
                }
            });
        }

        private void TreeLoad(AssetsFileInstance fromFile, AssetTypeValueField assetField, long fromPathId, TreeViewItem treeItem)
        {
            if (assetField.childrenCount == 0) return;

            int arrayIdx = 0;
            List<TreeViewItem> items = new List<TreeViewItem>(assetField.childrenCount + 1);

            AssetTypeTemplateField assetFieldTemplate = assetField.GetTemplateField();
            bool isArray = assetFieldTemplate.isArray;

            if (isArray)
            {
                int size = assetField.GetValue().AsArray().size;
                AssetTypeTemplateField sizeTemplate = assetFieldTemplate.children[0];
                TreeViewItem arrayIndexTreeItem = CreateTreeItem($"{sizeTemplate.type} {sizeTemplate.name} = {size}");
                items.Add(arrayIndexTreeItem);
            }

            foreach (AssetTypeValueField childField in assetField.children)
            {
                if (childField == null) return;
                string value = "";
                if (childField.GetValue() != null)
                {
                    EnumValueTypes evt = childField.GetValue().GetValueType();
                    string quote = "";
                    if (evt == EnumValueTypes.String) quote = "\"";
                    if (1 <= (int)evt && (int)evt <= 12)
                    {
                        value = $" = {quote}{childField.GetValue().AsString()}{quote}";
                    }
                    if (evt == EnumValueTypes.Array ||
                        evt == EnumValueTypes.ByteArray)
                    {
                        value = $" (size {childField.childrenCount})";
                    }
                }

                if (isArray)
                {
                    TreeViewItem arrayIndexTreeItem = CreateTreeItem($"{arrayIdx}");
                    items.Add(arrayIndexTreeItem);

                    TreeViewItem childTreeItem = CreateTreeItem($"{childField.GetFieldType()} {childField.GetName()}{value}");
                    arrayIndexTreeItem.Items = new List<TreeViewItem>() { childTreeItem };

                    if (childField.childrenCount > 0)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new List<TreeViewItem>() { dummyItem };
                        SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                    }

                    arrayIdx++;
                }
                else
                {
                    TreeViewItem childTreeItem = CreateTreeItem($"{childField.GetFieldType()} {childField.GetName()}{value}");
                    items.Add(childTreeItem);

                    if (childField.childrenCount > 0)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new List<TreeViewItem>() { dummyItem };
                        SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                    }
                }
            }

            string templateFieldType = assetField.templateField.type;
            if (templateFieldType.StartsWith("PPtr<") && templateFieldType.EndsWith(">"))
            {
                var fileIdField = assetField.Get("m_FileID");
                var pathIdField = assetField.Get("m_PathID");
                bool pptrValid = !fileIdField.IsDummy() && !pathIdField.IsDummy();

                if (pptrValid)
                {
                    int fileId = fileIdField.GetValue().AsInt();
                    long pathId = pathIdField.GetValue().AsInt64();

                    AssetContainer cont = workspace.GetAssetContainer(fromFile, fileId, pathId, false);

                    TreeViewItem childTreeItem = CreateTreeItem($"[view asset]");
                    items.Add(childTreeItem);

                    TreeViewItem dummyItem = CreateTreeItem("Loading...");
                    childTreeItem.Items = new List<TreeViewItem>() { dummyItem };
                    SetPPtrEvents(childTreeItem, fromFile, pathId, cont);
                }
            }

            treeItem.Items = items;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private class TreeViewItemInfo
        {
            public bool loaded;
            public AssetsFileInstance fromFile;
            public long fromPathId;

            public TreeViewItemInfo(AssetsFileInstance fromFile, long fromPathId)
            {
                this.loaded = false;
                this.fromFile = fromFile;
                this.fromPathId = fromPathId;
            }
        }
    }
}
