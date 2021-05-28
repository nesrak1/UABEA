using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
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

        private AssetWorkspace workspace;

        public DataWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            treeView = this.FindControl<TreeView>("treeView");
            this.Closing += DataWindow_Closing;
        }

        public DataWindow(AssetWorkspace workspace, AssetExternal ext) : this()
        {
            this.workspace = workspace;

            AssetTypeValueField baseField = ext.instance.GetBaseField();
            TreeViewItem baseItem = CreateTreeItem($"{baseField.GetFieldType()} {baseField.GetName()}");

            TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
            baseItem.Items = new List<TreeViewItem>() { arrayIndexTreeItem };
            treeView.Items = new List<TreeViewItem>() { baseItem };
            SetTreeItemEvents(baseItem, ext.file, baseField);
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
        private void SetTreeItemEvents(TreeViewItem item, AssetsFileInstance fromFile, AssetTypeValueField field)
        {
            item.Tag = false;
            //avalonia's treeviews have no Expanded event so this is all we can do
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(isExpanded =>
            {
                if (isExpanded && !(bool)item.Tag)
                {
                    item.Tag = true; //don't load this again
                    TreeLoad(fromFile, field, item);
                }
            });
        }

        private void SetPPtrEvents(TreeViewItem item, AssetsFileInstance fromFile, AssetExternal ext)
        {
            item.Tag = false;
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(isExpanded =>
            {
                if (isExpanded && !(bool)item.Tag)
                {
                    item.Tag = true; //don't load this again

                    AssetTypeValueField baseField = workspace.GetExtAssetReplaced(ext.file, 0, ext.info.index).instance.GetBaseField();
                    TreeViewItem baseItem = CreateTreeItem($"{baseField.GetFieldType()} {baseField.GetName()}");

                    TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
                    baseItem.Items = new List<TreeViewItem>() { arrayIndexTreeItem };
                    item.Items = new List<TreeViewItem>() { baseItem };
                    SetTreeItemEvents(baseItem, ext.file, baseField);
                }
            });
        }

        private void TreeLoad(AssetsFileInstance fromFile, AssetTypeValueField assetField, TreeViewItem treeItem)
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
                        SetTreeItemEvents(childTreeItem, fromFile, childField);
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
                        SetTreeItemEvents(childTreeItem, fromFile, childField);
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

                    AssetExternal ext = workspace.am.GetExtAsset(fromFile, fileId, pathId, false);

                    TreeViewItem childTreeItem = CreateTreeItem($"[view asset]");
                    items.Add(childTreeItem);

                    TreeViewItem dummyItem = CreateTreeItem("Loading...");
                    childTreeItem.Items = new List<TreeViewItem>() { dummyItem };
                    SetPPtrEvents(childTreeItem, fromFile, ext);
                }
            }

            treeItem.Items = items;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
