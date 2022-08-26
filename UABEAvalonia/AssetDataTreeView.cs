using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class AssetDataTreeView : TreeView
    {
        private AssetWorkspace workspace;

        private AvaloniaList<TreeViewItem> ListItems => (AvaloniaList<TreeViewItem>)Items;
        private SolidColorBrush PrimNameBrush = SolidColorBrush.Parse("#569cd6");
        private SolidColorBrush TypeNameBrush = SolidColorBrush.Parse("#4ec9b0");
        private SolidColorBrush StringBrush = SolidColorBrush.Parse("#d69d85");
        private SolidColorBrush ValueBrush = SolidColorBrush.Parse("#b5cea8");

        public void Init(AssetWorkspace workspace)
        {
            this.workspace = workspace;
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

            AssetTypeValueField baseField = workspace.GetBaseField(container);

            string baseItemString = $"{baseField.TypeName} {baseField.FieldName}";
            if (container.ClassId == (uint)AssetClassID.MonoBehaviour)
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
            if (treeItem.Header is string header)
            {
                if (header != "[view asset]")
                {
                    treeItem.IsExpanded = true;

                    foreach (TreeViewItem treeItemChild in treeItem.Items)
                    {
                        ExpandAllChildren(treeItemChild);
                    }
                }
            }
        }

        public void CollapseAllChildren(TreeViewItem treeItem)
        {
            if (treeItem.Header is string header)
            {
                if (header != "[view asset]")
                {
                    foreach (TreeViewItem treeItemChild in treeItem.Items)
                    {
                        CollapseAllChildren(treeItemChild);
                    }

                    treeItem.IsExpanded = false;
                }
            }
        }

        private TreeViewItem CreateTreeItem(string text)
        {
            return new TreeViewItem() { Header = text };
        }

        private TreeViewItem CreateColorTreeItem(string typeName, string fieldName)
        {
            RichTextBlock tb = new RichTextBlock();

            Span span1 = new Span()
            {
                Foreground = TypeNameBrush,/*,
                FontWeight = FontWeight.Bold*/
            };
            Bold bold1 = new Bold();
            bold1.Inlines.Add(typeName);
            span1.Inlines.Add(bold1);
            tb.Inlines.Add(span1);

            Bold bold2 = new Bold();
            bold2.Inlines.Add($" {fieldName}");
            tb.Inlines.Add(bold2);

            /*
			<Span Foreground="#4ec9b0">
				<Bold>TypeName</Bold></Span>
			<Bold>. fieldName = .</Bold>
			<Span Foreground="#d69d85">
				<Bold>"hi"</Bold>
			</Span> 
            */

            return new TreeViewItem()
            {
                Header = tb
            };
        }

        private TreeViewItem CreateColorTreeItem(string typeName, string fieldName, string middle, string value)
        {
            bool isString = value.StartsWith("\"");

            RichTextBlock tb = new RichTextBlock();

            bool primitiveType = AssetTypeValueField.GetValueTypeByTypeName(typeName) != AssetValueType.None;

            Span span1 = new Span()
            {
                Foreground = primitiveType ? PrimNameBrush : TypeNameBrush
            };
            Bold bold1 = new Bold();
            bold1.Inlines.Add(typeName);
            span1.Inlines.Add(bold1);
            tb.Inlines.Add(span1);

            Bold bold2 = new Bold();
            bold2.Inlines.Add($" {fieldName}");
            tb.Inlines.Add(bold2);

            tb.Inlines.Add(middle);

            if (value != "")
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
            expandObs.Subscribe(isExpanded =>
            {
                AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again
                    TreeLoad(fromFile, field, fromPathId, item);
                }
            });
        }

        private void SetPPtrEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetContainer cont)
        {
            item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(isExpanded =>
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
            });
        }

        private void TreeLoad(AssetsFileInstance fromFile, AssetTypeValueField assetField, long fromPathId, TreeViewItem treeItem)
        {
            if (assetField.Children.Count == 0) return;

            int arrayIdx = 0;
            AvaloniaList<TreeViewItem> items = new AvaloniaList<TreeViewItem>(assetField.Children.Count + 1);

            AssetTypeTemplateField assetFieldTemplate = assetField.TemplateField;
            bool isArray = assetFieldTemplate.IsArray;

            if (isArray)
            {
                int size = assetField.AsArray.size;
                AssetTypeTemplateField sizeTemplate = assetFieldTemplate.Children[0];
                TreeViewItem arrayIndexTreeItem = CreateColorTreeItem(sizeTemplate.Type, sizeTemplate.Name, " = ", size.ToString());
                items.Add(arrayIndexTreeItem);
            }

            foreach (AssetTypeValueField childField in assetField)
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
                        middle = $" (size {childField.AsByteArray.Length})";
                    }
                }

                if (isArray)
                {
                    TreeViewItem arrayIndexTreeItem = CreateTreeItem($"{arrayIdx}");
                    items.Add(arrayIndexTreeItem);

                    TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value);
                    arrayIndexTreeItem.Items = new AvaloniaList<TreeViewItem>() { childTreeItem };

                    if (childField.Children.Count > 0)
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

                    if (childField.Children.Count > 0)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new AvaloniaList<TreeViewItem>() { dummyItem };
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
                    childTreeItem.Items = new AvaloniaList<TreeViewItem>() { dummyItem };
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
