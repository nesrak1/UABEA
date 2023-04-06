using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class LoadModPackageDialog : Window
    {
        private bool builtTree;
        private TreeViewItem affectedBundles;
        private TreeViewItem affectedFiles;
        private InstallerPackageFile emip;
        private AssetsManager am;

        private ObservableCollection<LoadModPackageTreeFileInfo> filesItems;
        private HashSet<string> fileNames;

        public LoadModPackageDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnBaseFolder.Click += BtnBaseFolder_Click;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            treeView.DoubleTapped += TreeView_DoubleTapped;
            boxBaseFolder.TextChanged += BoxBaseFolder_TextChanged;
        }

        public LoadModPackageDialog(InstallerPackageFile emip, AssetsManager am) : this()
        {
            this.emip = emip;
            this.am = am;

            BuildTreeAssets();
        }

        private async void BtnBaseFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select base folder";

            string dir = await ofd.ShowAsync(this);

            if (dir != null && dir != string.Empty)
            {
                boxBaseFolder.Text = dir;
            }
        }

        private async void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var fileInsts = new List<AssetsFileInstance>();
            var replacerLists = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();

            foreach (LoadModPackageTreeFileInfo fileItem in affectedFiles.Items)
            {
                if (fileItem.selected && File.Exists(fileItem.fullPath))
                {
                    AssetsFileInstance fileInst = am.LoadAssetsFile(fileItem.fullPath, true);
                    fileInsts.Add(fileInst);

                    if (!replacerLists.ContainsKey(fileInst))
                        replacerLists[fileInst] = new List<AssetsReplacer>();

                    foreach (AssetsReplacer replacer in fileItem.assetDesc.replacers)
                    {
                        replacerLists[fileInst].Add(replacer);
                    }
                }
            }

            if (fileInsts.Count == 0)
            {
                await MessageBoxUtil.ShowDialog(this,
                    "Error", "Did not load any files. Did you select any (double click) or set the correct base path?");
                return;
            }

            if (!await LoadOrAskTypeData(fileInsts[0]))
            {
                Close(false);
                return;
            }

            InfoWindow info = new InfoWindow(am, fileInsts, false);
            foreach (KeyValuePair<AssetsFileInstance, List<AssetsReplacer>> kvp in replacerLists)
            {
                AssetsFileInstance fileInst = kvp.Key;
                List<AssetsReplacer> replacerList = kvp.Value;
                foreach (AssetsReplacer replacer in replacerList)
                {
                    info.Workspace.AddReplacer(fileInst, replacer);
                }
            }
            info.Show();

            //temporary hack
            Hide();

            info.Closed += (object? sender, EventArgs e) =>
            {
                Close(true);
            };
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void TreeView_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (treeView.SelectedItem is not LoadModPackageTreeFileInfo fileItem)
                return;

            fileItem.selected = !fileItem.selected;
            fileItem.Update(nameof(fileItem.DisplayText));
        }

        private void BoxBaseFolder_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateTree();
        }

        private void BuildTreeAssets()
        {
            builtTree = false;

            affectedBundles = CreateTreeItem("Affected bundles");
            affectedFiles = CreateTreeItem("Affected assets files");

            treeView.Items = new List<TreeViewItem>() { affectedBundles, affectedFiles };

            filesItems = new ObservableCollection<LoadModPackageTreeFileInfo>();
            fileNames = new HashSet<string>();

            foreach (var affectedFile in emip.affectedFiles)
            {
                if (!affectedFile.isBundle)
                {
                    string filePath = affectedFile.path;
                    if (!fileNames.Contains(filePath))
                    {
                        LoadModPackageTreeFileInfo newFileItem = new LoadModPackageTreeFileInfo(affectedFile);
                        filesItems.Add(newFileItem);
                        fileNames.Add(filePath);
                    }
                }
            }

            affectedFiles.Items = filesItems;

            builtTree = true;
        }

        private void UpdateTree()
        {
            if (!builtTree)
                return;

            string rootPath = boxBaseFolder.Text;
            foreach (LoadModPackageTreeFileInfo fileItem in affectedFiles.Items)
            {
                fileItem.UpdateRootPath(rootPath);
            }
        }

        private TreeViewItem CreateTreeItem(string text)
        {
            return new TreeViewItem() { Header = text };
        }

        //todo add this to a helper class
        private async Task<bool> LoadOrAskTypeData(AssetsFileInstance fileInst)
        {
            string uVer = fileInst.file.Metadata.UnityVersion;
            am.LoadClassDatabaseFromPackage(uVer);
            //if (am.LoadClassDatabaseFromPackage(uVer) == null)
            //{
            //    VersionWindow version = new VersionWindow(uVer, am.classPackage);
            //    var newFile = await version.ShowDialog<ClassDatabaseFile>(this);
            //    if (newFile == null)
            //        return false;
            //
            //    am.classFile = newFile;
            //}
            return true;
        }
    }

    public class LoadModPackageTreeFileInfo : INotifyPropertyChanged
    {
        public InstallerPackageAssetsDesc assetDesc;
        public string rootPath;
        public string relPath;
        public string fullPath;
        public bool selected;

        public string DisplayText { get => $"{fullPath} {(selected ? "(Selected)" : "")}"; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LoadModPackageTreeFileInfo(InstallerPackageAssetsDesc assetDesc)
        {
            this.assetDesc = assetDesc;
            relPath = assetDesc.path;
            rootPath = "";
            fullPath = relPath;
            selected = false;
        }

        public void UpdateRootPath(string rootPath)
        {
            this.rootPath = rootPath;

            string correctedFullPath = relPath;
            if (relPath.StartsWith(".\\"))
                correctedFullPath = relPath.Substring(2);

            if (IsPathRootedSafe(rootPath))
                fullPath = Path.Combine(rootPath, correctedFullPath);
            else
                fullPath = relPath;

            Update(nameof(DisplayText));
        }

        private bool IsPathRootedSafe(string path)
        {
            try
            {
                return Path.IsPathRooted(path);
            }
            catch
            {
                return false;
            }
        }

        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
