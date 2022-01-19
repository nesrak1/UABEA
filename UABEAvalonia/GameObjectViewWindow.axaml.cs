using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.IO;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class GameObjectViewWindow : Window
    {
        //controls
        private TreeView gameObjectTreeView;
        private AssetDataTreeView componentTreeView;
        private MenuItem menuVisitAsset;
        private ComboBox cbxFiles;
        private Button btnExpand;
        private Button btnCollapse;

        private InfoWindow win;
        private AssetWorkspace workspace;

        public GameObjectViewWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            gameObjectTreeView = this.FindControl<TreeView>("gameObjectTreeView");
            componentTreeView = this.FindControl<AssetDataTreeView>("componentTreeView");
            menuVisitAsset = this.FindControl<MenuItem>("menuVisitAsset");
            cbxFiles = this.FindControl<ComboBox>("cbxFiles");
            btnExpand = this.FindControl<Button>("btnExpand");
            btnCollapse = this.FindControl<Button>("btnCollapse");
            //generated events
            gameObjectTreeView.SelectionChanged += GameObjectTreeView_SelectionChanged;
            menuVisitAsset.Click += MenuVisitAsset_Click;
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;
            btnExpand.Click += BtnExpand_Click;
            btnCollapse.Click += BtnCollapse_Click;
        }

        public GameObjectViewWindow(InfoWindow win, AssetWorkspace workspace) : this()
        {
            this.win = win;
            this.workspace = workspace;

            componentTreeView.Init(workspace);
            PopulateFilesComboBox();
            PopulateHierarchyTreeView();
        }

        private void GameObjectTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            object? selectedItemObj = e.AddedItems[0];
            if (selectedItemObj == null)
                return;

            TreeViewItem selectedItem = (TreeViewItem)selectedItemObj;
            if (selectedItem.Tag == null)
                return;

            AssetContainer gameObjectCont = (AssetContainer)selectedItem.Tag;
            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            AssetTypeValueField components = gameObjectBf.Get("m_Component").Get("Array");

            componentTreeView.Reset();

            foreach (AssetTypeValueField data in components.GetChildrenList())
            {
                AssetTypeValueField component = data.Get("component");
                AssetContainer componentCont = workspace.GetAssetContainer(gameObjectCont.FileInstance, component, false);
                componentTreeView.LoadComponent(componentCont);
            }
        }

        private void MenuVisitAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)componentTreeView.SelectedItem;
            if (item != null && item.Tag != null)
            {
                AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;
                win.SelectAsset(info.fromFile, info.fromPathId);
            }
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            PopulateHierarchyTreeView();
        }

        private void BtnExpand_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (componentTreeView.SelectedItem != null && componentTreeView.SelectedItem is TreeViewItem treeItem)
            {
                componentTreeView.ExpandAllChildren(treeItem);
            }
        }

        private void BtnCollapse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (componentTreeView.SelectedItem != null && componentTreeView.SelectedItem is TreeViewItem treeItem)
            {
                componentTreeView.CollapseAllChildren(treeItem);
            }
        }

        private void PopulateFilesComboBox()
        {
            AvaloniaList<object> comboBoxItems = (AvaloniaList<object>)cbxFiles.Items;
            foreach (AssetsFileInstance fileInstance in workspace.LoadedFiles)
            {
                ComboBoxItem comboItem = new ComboBoxItem()
                {
                    Content = fileInstance.name,
                    Tag = fileInstance
                };
                comboBoxItems.Add(comboItem);
            }
            cbxFiles.SelectedIndex = 0;
        }

        private void PopulateHierarchyTreeView()
        {
            ComboBoxItem? selectedComboItem = (ComboBoxItem?)cbxFiles.SelectedItem;
            if (selectedComboItem == null)
                return;

            AssetsFileInstance? fileInstance = (AssetsFileInstance?)selectedComboItem.Tag;
            if (fileInstance == null)
                return;

            //clear treeview
            gameObjectTreeView.Items = new AvaloniaList<object>();

            foreach (var asset in workspace.LoadedAssets)
            {
                AssetContainer assetCont = asset.Value;

                if (assetCont.FileInstance == fileInstance && assetCont.ClassId == (uint)AssetClassID.Transform)
                {
                    AssetTypeValueField transformBf = workspace.GetBaseField(assetCont);
                    AssetTypeValueField transformFatherBf = transformBf.Get("m_Father");
                    long pathId = transformFatherBf.Get("m_PathID").GetValue().AsInt64();
                    //is root GameObject
                    if (pathId == 0)
                    {
                        LoadGameObjectTreeItem(assetCont, transformBf, null);
                    }
                }
            }
        }

        private void LoadGameObjectTreeItem(AssetContainer transformCont, AssetTypeValueField transformBf, TreeViewItem? parentTreeItem)
        {
            TreeViewItem treeItem = new TreeViewItem();

            AssetTypeValueField gameObjectRef = transformBf.Get("m_GameObject");
            AssetContainer gameObjectCont = workspace.GetAssetContainer(transformCont.FileInstance, gameObjectRef, false);

            if (gameObjectCont == null)
                return;

            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            string name = gameObjectBf.Get("m_Name").GetValue().AsString();

            treeItem.Header = name;
            treeItem.Tag = gameObjectCont;

            AssetTypeValueField children = transformBf.Get("m_Children").Get("Array");
            foreach (AssetTypeValueField child in children.GetChildrenList())
            {
                AssetContainer childTransformCont = workspace.GetAssetContainer(transformCont.FileInstance, child, false);
                AssetTypeValueField childTransformBf = workspace.GetBaseField(childTransformCont);
                LoadGameObjectTreeItem(childTransformCont, childTransformBf, treeItem);
            }

            AvaloniaList<object> parentItems;
            if (parentTreeItem == null)
            {
                parentItems = (AvaloniaList<object>)gameObjectTreeView.Items;
            }
            else
            {
                parentItems = (AvaloniaList<object>)parentTreeItem.Items;
            }
            parentItems.Add(treeItem);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
