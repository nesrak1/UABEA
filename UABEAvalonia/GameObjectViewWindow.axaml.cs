using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class GameObjectViewWindow : Window
    {
        private InfoWindow win;
        private AssetWorkspace workspace;

        private bool ignoreDropdownEvent;
        private AssetContainer? selectedGo;
        private TreeViewItem? selectedTreeItem;

        public GameObjectViewWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            gameObjectTreeView.SelectionChanged += GameObjectTreeView_SelectionChanged;
            gameObjectTreeView.DoubleTapped += GameObjectTreeView_DoubleTapped;
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;
        }

        public GameObjectViewWindow(InfoWindow win, AssetWorkspace workspace) : this()
        {
            this.win = win;
            this.workspace = workspace;

            ignoreDropdownEvent = true;

            componentTreeView.Init(win, workspace);
            PopulateFilesComboBox();
            PopulateHierarchyTreeView();
        }

        public GameObjectViewWindow(InfoWindow win, AssetWorkspace workspace, AssetContainer selectedGo) : this()
        {
            this.win = win;
            this.workspace = workspace;
            this.selectedGo = selectedGo;

            ignoreDropdownEvent = true;

            componentTreeView.Init(win, workspace);
            PopulateFilesComboBox();
            PopulateHierarchyTreeView();

            if (selectedTreeItem != null)
            {
                TreeViewItem curItem = selectedTreeItem;
                while (curItem.Parent is TreeViewItem)
                {
                    curItem = (TreeViewItem)curItem.Parent;
                    curItem.IsExpanded = true;
                }
                gameObjectTreeView.SelectedItem = selectedTreeItem;
            }
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
            AssetTypeValueField components = gameObjectBf["m_Component"]["Array"];

            componentTreeView.Reset();

            foreach (AssetTypeValueField data in components)
            {
                AssetTypeValueField component = data[data.Children.Count - 1];
                AssetContainer componentCont = workspace.GetAssetContainer(gameObjectCont.FileInstance, component, false);
                componentTreeView.LoadComponent(componentCont);
            }
        }

        private void GameObjectTreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (gameObjectTreeView.SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)gameObjectTreeView.SelectedItem;
                item.IsExpanded = !item.IsExpanded;
            }
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // this event happens after the constructor
            // is called, so this is the only way to do it
            if (ignoreDropdownEvent)
            {
                ignoreDropdownEvent = false;
                return;
            }

            PopulateHierarchyTreeView();
        }

        private void PopulateFilesComboBox()
        {
            foreach (AssetsFileInstance fileInstance in workspace.LoadedFiles)
            {
                ComboBoxItem comboItem = new ComboBoxItem()
                {
                    Content = fileInstance.name,
                    Tag = fileInstance
                };
                cbxFiles.Items?.Add(comboItem);
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

            // clear treeview
            gameObjectTreeView.Items = new AvaloniaList<object>();

            foreach (var asset in workspace.LoadedAssets)
            {
                AssetContainer assetCont = asset.Value;

                AssetClassID assetType = (AssetClassID)assetCont.ClassId;
                bool isTransformType = assetType == AssetClassID.Transform || assetType == AssetClassID.RectTransform;

                if (assetCont.FileInstance == fileInstance && isTransformType)
                {
                    AssetTypeValueField transformBf = workspace.GetBaseField(assetCont);
                    AssetTypeValueField transformFatherBf = transformBf["m_Father"];
                    long pathId = transformFatherBf["m_PathID"].AsLong;
                    // is root GameObject
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

            AssetTypeValueField gameObjectRef = transformBf["m_GameObject"];
            AssetContainer gameObjectCont = workspace.GetAssetContainer(transformCont.FileInstance, gameObjectRef, false);

            if (gameObjectCont == null)
                return;

            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            string name = gameObjectBf["m_Name"].AsString;

            treeItem.Header = name;
            treeItem.Tag = gameObjectCont;

            AssetTypeValueField children = transformBf["m_Children"]["Array"];
            foreach (AssetTypeValueField child in children)
            {
                AssetContainer childTransformCont = workspace.GetAssetContainer(transformCont.FileInstance, child, false);
                AssetTypeValueField childTransformBf = workspace.GetBaseField(childTransformCont);
                LoadGameObjectTreeItem(childTransformCont, childTransformBf, treeItem);
            }

            if (parentTreeItem == null)
            {
                gameObjectTreeView.Items?.Add(treeItem);
            }
            else
            {
                parentTreeItem.Items?.Add(treeItem);
            }

            if (selectedGo != null)
            {
                if (gameObjectCont.FileInstance == selectedGo.FileInstance && gameObjectCont.PathId == selectedGo.PathId)
                {
                    selectedTreeItem = treeItem;
                }
            }
        }
    }
}
