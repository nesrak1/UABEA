using AssetsTools.NET.Extra;
using AssetsTools.NET;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class AssetsFileInfoWindow
    {
        private void SetupTypeTreePageEvents()
        {
            lstTypeTreeType.SelectionChanged += TypeTreeTypeList_SelectionChanged;
        }

        private void TypeTreeTypeList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            TypeTreeListItem? item = (TypeTreeListItem?)lstTypeTreeType.SelectedItem;
            if (item != null)
            {
                FillTypeTreeTypeInfo(item.type);
            }
        }

        private void FillTypeTreeInfo()
        {
            AssetsFile afile = activeFile.file;
            AssetsFileMetadata meta = afile.Metadata;

            if (!meta.TypeTreeEnabled)
            {
                treeTypeTreeNode.ItemsSource = new List<string>()
                {
                    "There is no type tree data available."
                };
            }
            else
            {
                treeTypeTreeNode.ItemsSource = new List<string>()
                {
                    "Select a type to show the type tree data."
                };
            }

            List<TypeTreeListItem> typeListItems = new List<TypeTreeListItem>();
            AddTypeTreeListItems(typeListItems, meta.TypeTreeTypes, false);
            AddTypeTreeListItems(typeListItems, meta.RefTypes, true);

            lstTypeTreeType.ItemsSource = typeListItems;
        }

        private void AddTypeTreeListItems(List<TypeTreeListItem> into, List<TypeTreeType> types, bool refType)
        {
            foreach (TypeTreeType type in types)
            {
                string typeName;
                if (type.Nodes == null || type.Nodes.Count == 0)
                {
                    ClassDatabaseType dbType = cldb.FindAssetClassByID(type.TypeId);
                    typeName = cldb.GetString(dbType.Name);
                }
                else
                {
                    TypeTreeNode baseField = type.Nodes[0];
                    typeName = baseField.GetTypeString(type.StringBuffer);
                }

                string scriptIndexStr = string.Empty;
                if (type.ScriptTypeIndex != 0xffff)
                {
                    scriptIndexStr = $"/{type.ScriptTypeIndex:d4}";
                }

                string refInfo = refType ? " REF" : string.Empty;
                into.Add(new TypeTreeListItem($"{typeName} (0x{type.TypeId:x}{scriptIndexStr}){refInfo}", type));
            }
        }

        private void FillTypeTreeTypeInfo(TypeTreeType type)
        {
            AssetsFile afile = activeFile.file;
            AssetsFileMetadata meta = afile.Metadata;

            if (type.Nodes == null || type.Nodes.Count == 0)
            {
                ClassDatabaseType cldt = cldb.FindAssetClassByID(type.TypeId);
                boxTypeTreeType.Text = cldb.GetString(cldt.Name);
            }
            else
            {
                TypeTreeNode baseField = type.Nodes[0];
                boxTypeTreeType.Text = baseField.GetTypeString(type.StringBuffer);
            }

            boxTypeTreeTypeId.Text = $"{type.TypeId} (0x{type.TypeId:x})";
            if (type.ScriptTypeIndex != 0xffff)
            {
                string scriptName;
                try
                {
                    scriptName = AssetHelper.GetAssetsFileScriptInfo(am, activeFile, type.ScriptTypeIndex).ClassName;
                }
                catch
                {
                    scriptName = "UNKNOWN";
                }

                boxTypeTreeScriptId.Text = $"{type.ScriptTypeIndex} ({scriptName})";
            }
            else
            {
                boxTypeTreeScriptId.Text = string.Empty;
            }

            if (!type.TypeHash.IsZero())
                boxTypeTreeHash.Text = type.TypeHash.ToString();
            else
                boxTypeTreeHash.Text = string.Empty;

            if (!type.ScriptIdHash.IsZero())
                boxTypeTreeMonoHash.Text = type.ScriptIdHash.ToString();
            else
                boxTypeTreeMonoHash.Text = string.Empty;

            if (meta.TypeTreeEnabled)
                FillTypeTreeNodeTree(type);
        }

        private void FillTypeTreeNodeTree(TypeTreeType type)
        {
            var treeViewItems = new List<TreeViewItem>();
            var treeNodeItemsStack = new List<ObservableCollection<TreeViewItem>>();

            TypeTreeNode baseField = type.Nodes[0];
            TreeViewItem rootNode = MakeTreeViewItem(TypeFieldToString(baseField, type), baseField);
            treeViewItems.Add(rootNode);
            treeNodeItemsStack.Add((ObservableCollection<TreeViewItem>)rootNode.ItemsSource!);

            for (int i = 1; i < type.Nodes.Count; i++)
            {
                TypeTreeNode field = type.Nodes[i];
                ObservableCollection<TreeViewItem> parentNodeItems = treeNodeItemsStack[field.Level - 1];
                TreeViewItem node = MakeTreeViewItem(TypeFieldToString(field, type), field);

                parentNodeItems?.Add(node);

                node.Tag = field;
                if (treeNodeItemsStack.Count > field.Level)
                    treeNodeItemsStack[field.Level] = (ObservableCollection<TreeViewItem>)node.ItemsSource!;
                else
                    treeNodeItemsStack.Add((ObservableCollection<TreeViewItem>)node.ItemsSource!);
            }

            treeTypeTreeNode.ItemsSource = treeViewItems;
        }

        private TreeViewItem MakeTreeViewItem(string header, object tag)
        {
            return new TreeViewItem() { Header = header, Tag = tag, ItemsSource = new ObservableCollection<TreeViewItem>() };
        }

        private string TypeFieldToString(TypeTreeNode node, TypeTreeType type)
        {
            string stringTable = type.StringBuffer;
            return $"{node.GetTypeString(stringTable)} {node.GetNameString(stringTable)}";
        }

        private class TypeTreeListItem
        {
            public string text;
            public TypeTreeType type;

            public TypeTreeListItem(string text, TypeTreeType type)
            {
                this.text = text;
                this.type = type;
            }

            public override string ToString()
            {
                return text;
            }
        }
    }
}
