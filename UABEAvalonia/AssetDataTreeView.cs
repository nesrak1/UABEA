using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Reactive;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace UABEAvalonia
{
    public class AssetDataTreeView : TreeView
    {
        private AssetWorkspace workspace;
        private InfoWindow win;

        private AvaloniaList<TreeViewItem> ListItems => (AvaloniaList<TreeViewItem>)Items;

        private static SolidColorBrush PrimNameBrushDark = SolidColorBrush.Parse("#569cd6");
        private static SolidColorBrush PrimNameBrushLight = SolidColorBrush.Parse("#0000ff");
        private static SolidColorBrush TypeNameBrushDark = SolidColorBrush.Parse("#4ec9b0");
        private static SolidColorBrush TypeNameBrushLight = SolidColorBrush.Parse("#2b91af");
        private static SolidColorBrush StringBrushDark = SolidColorBrush.Parse("#d69d85");
        private static SolidColorBrush StringBrushLight = SolidColorBrush.Parse("#a31515");
        private static SolidColorBrush ValueBrushDark = SolidColorBrush.Parse("#b5cea8");
        private static SolidColorBrush ValueBrushLight = SolidColorBrush.Parse("#5b2da8");

        private MenuItem menuEditAsset;
        private MenuItem menuVisitAsset;
        private MenuItem menuExpandSel;
        private MenuItem menuCollapseSel;

        private SolidColorBrush PrimNameBrush
        {
            get
            {
                return ThemeHandler.UseDarkTheme
                    ? PrimNameBrushDark
                    : PrimNameBrushLight;
            }
        }
        private SolidColorBrush TypeNameBrush
        {
            get
            {
                return ThemeHandler.UseDarkTheme
                    ? TypeNameBrushDark
                    : TypeNameBrushLight;
            }
        }
        private SolidColorBrush StringBrush
        {
            get
            {
                return ThemeHandler.UseDarkTheme
                    ? StringBrushDark
                    : StringBrushLight;
            }
        }
        private SolidColorBrush ValueBrush
        {
            get
            {
                return ThemeHandler.UseDarkTheme
                    ? ValueBrushDark
                    : ValueBrushLight;
            }
        }

        public AssetDataTreeView() : base()
        {
            menuEditAsset = new MenuItem() { Header = "Edit Asset" };
            menuVisitAsset = new MenuItem() { Header = "Visit Asset" };
            menuExpandSel = new MenuItem() { Header = "Expand Selection" };
            menuCollapseSel = new MenuItem() { Header = "Collapse Selection" };

            DoubleTapped += AssetDataTreeView_DoubleTapped;
            menuEditAsset.Click += MenuEditAsset_Click;
            menuVisitAsset.Click += MenuVisitAsset_Click;
            menuExpandSel.Click += MenuExpandSel_Click;
            menuCollapseSel.Click += MenuCollapseSel_Click;

            ContextMenu = new ContextMenu();
            ContextMenu.Items = new AvaloniaList<MenuItem>()
            {
                menuEditAsset,
                menuVisitAsset,
                menuExpandSel,
                menuCollapseSel
            };
        }

        private async void MenuEditAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)SelectedItem;
                if (item.Tag != null)
                {
                    AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;

                    AssetContainer? cont = workspace.GetAssetContainer(info.fromFile, 0, info.fromPathId, false);
                    if (cont == null || !cont.HasValueField)
                    {
                        return;
                    }

                    bool saved = await win.ShowEditAssetWindow(cont);
                    if (saved)
                    {
                        await MessageBoxUtil.ShowDialog(win, "Note", "Asset updated. Changes will be shown next time you open this asset.");
                    }
                }
            }
        }

        private void AssetDataTreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)SelectedItem;
                item.IsExpanded = !item.IsExpanded;
            }
        }

        private void MenuVisitAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)SelectedItem;
                if (item != null && item.Tag != null)
                {
                    AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;
                    win.SelectAsset(info.fromFile, info.fromPathId);
                }
            }
        }

        private void MenuExpandSel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                ExpandAllChildren((TreeViewItem)SelectedItem);
            }
        }

        private void MenuCollapseSel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                CollapseAllChildren((TreeViewItem)SelectedItem);
            }
        }

        public void Init(InfoWindow win, AssetWorkspace workspace)
        {
            this.workspace = workspace;
            this.win = win;
            Reset();
        }

        public void Reset()
        {
            Items = new AvaloniaList<TreeViewItem>();
        }

        public void LoadComponent(AssetContainer container)
        {
            if (workspace == null)
                return;

            AssetTypeValueField? baseField = workspace.GetBaseField(container);

            if (baseField == null)
            {
                TreeViewItem errorItem0 = CreateTreeItem("Asset failed to deserialize.");
                TreeViewItem errorItem1 = CreateTreeItem("The file version may be too new for");
                TreeViewItem errorItem2 = CreateTreeItem("this tpk or the file format is custom.");
                errorItem0.Items = new List<TreeViewItem>() { errorItem1, errorItem2 };
                ListItems.Add(errorItem0);
                return;
            }

            string baseItemString = $"{baseField.TypeName} {baseField.FieldName}";
            if (container.ClassId == (uint)AssetClassID.MonoBehaviour || container.ClassId < 0)
            {
                string monoName = Extensions.GetMonoBehaviourNameFast(workspace, container);
                if (monoName != null)
                {
                    baseItemString += $" ({monoName})";
                }
            }

            TreeViewItem baseItem = CreateTreeItem(baseItemString);

            TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
            baseItem.Items = new AvaloniaList<TreeViewItem>() { arrayIndexTreeItem };
            ListItems.Add(baseItem);

            SetTreeItemEvents(baseItem, container.FileInstance, container.PathId, baseField);
            baseItem.IsExpanded = true;
        }

        public void ExpandAllChildren(TreeViewItem treeItem)
        {
            string? text = null;
            if (treeItem.Header is string header)
            {
                text = header;
            }
            else if (treeItem.Header is TextBlock rtb)
            {
                text = rtb.Text;
            }

            if (text != "[view asset]")
            {
                treeItem.IsExpanded = true;

                foreach (TreeViewItem treeItemChild in treeItem.Items)
                {
                    ExpandAllChildren(treeItemChild);
                }
            }
        }

        public void CollapseAllChildren(TreeViewItem treeItem)
        {
            string? text = null;
            if (treeItem.Header is string header)
            {
                text = header;
            }
            else if (treeItem.Header is TextBlock rtb)
            {
                text = rtb.Text;
            }

            if (text != "[view asset]")
            {
                foreach (TreeViewItem treeItemChild in treeItem.Items)
                {
                    CollapseAllChildren(treeItemChild);
                }

                treeItem.IsExpanded = false;
            }
        }

        private TreeViewItem CreateTreeItem(string text)
        {
            return new TreeViewItem() { Header = text };
        }

        private TreeViewItem CreateColorTreeItem(string typeName, string fieldName)
        {
            return CreateColorTreeItem(typeName, fieldName, string.Empty, string.Empty);
        }

        private TreeViewItem CreateColorTreeItem(string typeName, string fieldName, string middle, string value)
        {
            bool isString = value.StartsWith("\"");

            TextBlock tb = new TextBlock();

            bool primitiveType = AssetTypeValueField.GetValueTypeByTypeName(typeName) != AssetValueType.None;

            Span span1 = new Span()
            {
                Foreground = primitiveType ? PrimNameBrush : TypeNameBrush
            };
            Bold bold1 = new Bold();
            bold1.Inlines.Add(typeName);
            span1.Inlines.Add(bold1);
            tb.Inlines!.Add(span1);

            Bold bold2 = new Bold();
            bold2.Inlines.Add($" {fieldName}");
            tb.Inlines.Add(bold2);

            tb.Inlines.Add(middle);

            if (value != string.Empty)
            {
                Span span2 = new Span()
                {
                    Foreground = isString ? StringBrush : ValueBrush
                };
                Bold bold3 = new Bold();
                bold3.Inlines.Add(value);
                span2.Inlines.Add(bold3);
                tb.Inlines.Add(span2);
            }

            return new TreeViewItem() { Header = tb };
        }

        //lazy load tree items. avalonia is really slow to load if
        //we just throw everything in the treeview at once
        private void SetTreeItemEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetTypeValueField field)
        {
            item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
            //avalonia's treeviews have no Expanded event so this is all we can do
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(new AnonymousObserver<bool>(isExpanded =>
            {
                AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again
                    TreeLoad(fromFile, field, fromPathId, item);
                }
            }));
        }

        private void SetPPtrEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetContainer cont)
        {
            item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(new AnonymousObserver<bool>(isExpanded =>
            {
                AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again

                    if (cont != null)
                    {
                        AssetTypeValueField baseField = workspace.GetBaseField(cont);
                        TreeViewItem baseItem = CreateTreeItem($"{baseField.TypeName} {baseField.FieldName}");

                        TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
                        baseItem.Items = new AvaloniaList<TreeViewItem>() { arrayIndexTreeItem };
                        item.Items = new AvaloniaList<TreeViewItem>() { baseItem };
                        SetTreeItemEvents(baseItem, cont.FileInstance, fromPathId, baseField);
                    }
                    else
                    {
                        item.Items = new AvaloniaList<TreeViewItem>() { CreateTreeItem("[null asset]") };
                    }
                }
            }));
        }

        private void TreeLoad(AssetsFileInstance fromFile, AssetTypeValueField assetField, long fromPathId, TreeViewItem treeItem)
        {
            List<AssetTypeValueField> children;
            if (assetField.Value != null && assetField.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
                children = assetField.AsManagedReferencesRegistry.references.Select(r => r.data).ToList();
            else
                children = assetField.Children;

            if (children.Count == 0) return;

            int arrayIdx = 0;
            AvaloniaList<TreeViewItem> items = new AvaloniaList<TreeViewItem>(children.Count + 1);

            AssetTypeTemplateField assetFieldTemplate = assetField.TemplateField;
            bool isArray = assetFieldTemplate.IsArray;

            if (isArray)
            {
                int size = assetField.AsArray.size;
                AssetTypeTemplateField sizeTemplate = assetFieldTemplate.Children[0];
                TreeViewItem arrayIndexTreeItem = CreateColorTreeItem(sizeTemplate.Type, sizeTemplate.Name, " = ", size.ToString());
                items.Add(arrayIndexTreeItem);
            }

            foreach (AssetTypeValueField childField in children)
            {
                if (childField == null) return;
                string middle = "";
                string value = "";
                if (childField.Value != null)
                {
                    AssetValueType evt = childField.Value.ValueType;
                    string quote = "";
                    if (evt == AssetValueType.String) quote = "\"";
                    if (1 <= (int)evt && (int)evt <= 12)
                    {
                        middle = " = ";
                        value = $"{quote}{childField.AsString}{quote}";
                    }
                    if (evt == AssetValueType.Array)
                    {
                        middle = $" (size {childField.Children.Count})";
                    }
                    else if (evt == AssetValueType.ByteArray)
                    {
                        byte[] bytes = childField.AsByteArray;
                        int byteArraySize = childField.AsByteArray.Length;
                        middle = $" (size {byteArraySize}) = ";

                        const int MAX_PREVIEW_BYTES = 20;
                        int previewSize = Math.Min(byteArraySize, MAX_PREVIEW_BYTES);

                        StringBuilder valueBuilder = new StringBuilder();
                        for (int i = 0; i < previewSize; i++)
                        {
                            if (i == 0)
                            {
                                valueBuilder.Append(bytes[i].ToString("X2"));
                            }
                            else
                            {
                                valueBuilder.Append(" " + bytes[i].ToString("X2"));
                            }
                        }

                        if (byteArraySize > MAX_PREVIEW_BYTES)
                        {
                            valueBuilder.Append(" ...");
                        }

                        value = valueBuilder.ToString();
                    }
                }

                bool hasChildren = childField.Children.Count > 0;

                if (isArray)
                {
                    TreeViewItem arrayIndexTreeItem = CreateTreeItem($"{arrayIdx}");
                    items.Add(arrayIndexTreeItem);

                    TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value);
                    arrayIndexTreeItem.Items = new AvaloniaList<TreeViewItem>() { childTreeItem };

                    if (hasChildren)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new AvaloniaList<TreeViewItem>() { dummyItem };
                        SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                    }

                    arrayIdx++;
                }
                else
                {
                    TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value);
                    items.Add(childTreeItem);

                    if (childField.Value != null && childField.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
                    {
                        ManagedReferencesRegistry registry = childField.AsManagedReferencesRegistry;

                        if (registry.version == 1 || registry.version == 2)
                        {
                            TreeViewItem versionItem = CreateColorTreeItem("int", "version", " = ", registry.version.ToString());
                            TreeViewItem refIdsItem = CreateColorTreeItem("vector", "RefIds");
                            TreeViewItem refIdsArrayItem = CreateColorTreeItem("Array", "Array", $" (size {registry.references.Count})", "");

                            AvaloniaList<TreeViewItem> refObjItems = new AvaloniaList<TreeViewItem>();

                            foreach (AssetTypeReferencedObject refObj in registry.references)
                            {
                                AssetTypeReference typeRef = refObj.type;

                                TreeViewItem refObjItem = CreateColorTreeItem("ReferencedObject", "data");

                                TreeViewItem managedTypeItem = CreateColorTreeItem("ReferencedManagedType", "type");
                                managedTypeItem.Items = new AvaloniaList<TreeViewItem>
                                {
                                    CreateColorTreeItem("string", "class", " = ", $"\"{typeRef.ClassName}\""),
                                    CreateColorTreeItem("string", "ns", " = ", $"\"{typeRef.Namespace}\""),
                                    CreateColorTreeItem("string", "asm", " = ", $"\"{typeRef.AsmName}\"")
                                };

                                TreeViewItem refObjectItem = CreateColorTreeItem("ReferencedObjectData", "data");

                                TreeViewItem dummyItem = CreateTreeItem("Loading...");
                                refObjectItem.Items = new AvaloniaList<TreeViewItem> { dummyItem };
                                SetTreeItemEvents(refObjectItem, fromFile, fromPathId, refObj.data);

                                if (registry.version == 1)
                                {
                                    refObjItem.Items = new AvaloniaList<TreeViewItem>
                                    {
                                        managedTypeItem,
                                        refObjectItem
                                    };
                                }
                                else if (registry.version == 2)
                                {
                                    refObjItem.Items = new AvaloniaList<TreeViewItem>
                                    {
                                        CreateColorTreeItem("SInt64", "rid", " = ", refObj.rid.ToString()),
                                        managedTypeItem,
                                        refObjectItem
                                    };
                                }

                                refObjItems.Add(refObjItem);
                            }

                            refIdsArrayItem.Items = refObjItems;

                            refIdsItem.Items = new AvaloniaList<TreeViewItem>
                            {
                                refIdsArrayItem
                            };

                            childTreeItem.Items = new AvaloniaList<TreeViewItem>
                            {
                                versionItem,
                                refIdsItem
                            };
                        }
                        else
                        {
                            TreeViewItem errorTreeItem = CreateTreeItem($"[unsupported registry version {registry.version}]");
                            childTreeItem.Items = new AvaloniaList<TreeViewItem> { errorTreeItem };
                        }
                    }
                    
                    if (hasChildren)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new AvaloniaList<TreeViewItem> { dummyItem };
                        SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                    }
                }
            }

            string templateFieldType = assetField.TypeName;
            if (templateFieldType.StartsWith("PPtr<") && templateFieldType.EndsWith(">"))
            {
                var fileIdField = assetField["m_FileID"];
                var pathIdField = assetField["m_PathID"];
                bool pptrValid = !fileIdField.IsDummy && !pathIdField.IsDummy;

                if (pptrValid)
                {
                    int fileId = fileIdField.AsInt;
                    long pathId = pathIdField.AsLong;

                    AssetContainer cont = workspace.GetAssetContainer(fromFile, fileId, pathId, true);

                    TreeViewItem childTreeItem = CreateTreeItem("[view asset]");
                    items.Add(childTreeItem);

                    TreeViewItem dummyItem = CreateTreeItem("Loading...");
                    childTreeItem.Items = new AvaloniaList<TreeViewItem> { dummyItem };
                    SetPPtrEvents(childTreeItem, fromFile, pathId, cont);
                }
            }

            treeItem.Items = items;
        }
    }

    public class AssetDataTreeViewItem
    {
        public bool loaded;
        public AssetsFileInstance fromFile;
        public long fromPathId;

        public AssetDataTreeViewItem(AssetsFileInstance fromFile, long fromPathId)
        {
            this.loaded = false;
            this.fromFile = fromFile;
            this.fromPathId = fromPathId;
        }
    }
}
