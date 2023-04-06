using AssetsTools.NET;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.IO;
using AssetsTools.NET.Extra;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using System.ComponentModel;

namespace UABEAvalonia
{
    public partial class ModMakerDialog : Window
    {
        private bool isBundle;
        private bool builtTree;
        private TreeViewItem affectedBundles;
        private TreeViewItem affectedFiles;
        private AssetWorkspace assetWs;
        private Stream importedEmipStream;
        //private BundleWorkspace bundleWs;

        private ObservableCollection<ModMakerTreeFileInfo> filesItems;
        private Dictionary<string, ModMakerTreeFileInfo> fileToTvi;

        public ModMakerDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnBaseFolder.Click += BtnBaseFolder_Click;
            btnImport.Click += BtnImport_Click;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            boxBaseFolder.TextChanged += BoxBaseFolder_TextChanged;
        }

        //for assets files
        public ModMakerDialog(AssetWorkspace workspace) : this()
        {
            assetWs = workspace;
            isBundle = false;

            BuildTreeAssets();
        }

        //for assets files in bundles
        //public ModMakerDialog(BundleWorkspace workspace) : this()
        //{
        //
        //}

        private void BuildTreeAssets()
        {
            builtTree = false;

            affectedBundles = CreateTreeItem("Affected bundles");
            affectedFiles = CreateTreeItem("Affected assets files");

            treeView.Items = new List<TreeViewItem>() { affectedBundles, affectedFiles };

            string rootPath = boxBaseFolder.Text;

            filesItems = new ObservableCollection<ModMakerTreeFileInfo>();
            fileToTvi = new Dictionary<string, ModMakerTreeFileInfo>();

            foreach (var newAsset in assetWs.NewAssets)
            {
                string file = newAsset.Key.fileName;
                if (!fileToTvi.ContainsKey(file))
                {
                    ModMakerTreeFileInfo newFileItem = new ModMakerTreeFileInfo(file, rootPath);
                    filesItems.Add(newFileItem);
                    fileToTvi.Add(file, newFileItem);
                }

                ModMakerTreeFileInfo fileItem = fileToTvi[file];

                var obsItems = fileItem.Replacers;
                obsItems.Add(new ModMakerTreeReplacerInfo(newAsset.Key, newAsset.Value));
            }

            affectedFiles.Items = filesItems;

            builtTree = true;
        }

        private void UpdateTree()
        {
            if (!builtTree)
                return;

            if (!isBundle)
            {
                string rootPath = boxBaseFolder.Text;
                foreach (ModMakerTreeFileInfo fileItem in affectedFiles.Items)
                {
                    fileItem.UpdateRootPath(rootPath);
                }
            }
            else
            {
                //todo
            }
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

        private TreeViewItem CreateTreeItem(string text)
        {
            return new TreeViewItem() { Header = text };
        }

        private void BuildEmip(string path)
        {
            InstallerPackageFile emip = new InstallerPackageFile
            {
                magic = "EMIP",
                includesCldb = false,
                modName = boxModName.Text ?? "",
                modCreators = boxCredits.Text ?? "",
                modDescription = boxDesc.Text ?? ""
            };

            emip.affectedFiles = new List<InstallerPackageAssetsDesc>();

            foreach (ModMakerTreeFileInfo file in affectedFiles.Items)
            {
                //hack pls fix thx
                string filePath = file.relPath;
                InstallerPackageAssetsDesc desc = new InstallerPackageAssetsDesc()
                {
                    isBundle = false,
                    path = filePath
                };
                desc.replacers = new List<object>();
                foreach (ModMakerTreeReplacerInfo change in file.Replacers)
                {
                    desc.replacers.Add(change.assetsReplacer);
                }
                emip.affectedFiles.Add(desc);
            }

            using (FileStream fs = File.Open(path, FileMode.Create))
            using (AssetsFileWriter writer = new AssetsFileWriter(fs))
            {
                emip.Write(writer);
            }
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

        private async void BtnImport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE Mod Installer Package", Extensions = new List<string>() { "emip" } }
            };

            string[] fileList = await ofd.ShowAsync(this);

            if (fileList == null || fileList.Length == 0)
                return;

            string emipPath = fileList[0];

            if (emipPath != null && emipPath != string.Empty)
            {
                OpenFolderDialog ofdBase = new OpenFolderDialog();
                ofdBase.Title = "Select base folder";

                string rootPath = await ofdBase.ShowAsync(this);

                if (rootPath != null && rootPath != string.Empty)
                {
                    InstallerPackageFile impEmip = new InstallerPackageFile();

                    if (importedEmipStream != null && importedEmipStream.CanRead)
                        importedEmipStream.Close();

                    importedEmipStream = File.OpenRead(emipPath);
                    AssetsFileReader r = new AssetsFileReader(importedEmipStream);
                    impEmip.Read(r);

                    boxModName.Text = impEmip.modName;
                    boxCredits.Text = impEmip.modCreators;
                    boxDesc.Text = impEmip.modDescription;

                    foreach (InstallerPackageAssetsDesc affectedFile in impEmip.affectedFiles)
                    {
                        if (!affectedFile.isBundle)
                        {
                            string file = Path.GetFullPath(affectedFile.path, rootPath);
                            if (!fileToTvi.ContainsKey(file))
                            {
                                ModMakerTreeFileInfo newFileItem = new ModMakerTreeFileInfo(file, rootPath);
                                filesItems.Add(newFileItem);
                                fileToTvi.Add(file, newFileItem);
                            }

                            ModMakerTreeFileInfo fileItem = fileToTvi[file];

                            foreach (AssetsReplacer replacer in affectedFile.replacers)
                            {
                                AssetID assetId = new AssetID(file, replacer.GetPathID());

                                var obsItems = fileItem.Replacers;
                                obsItems.Add(new ModMakerTreeReplacerInfo(assetId, replacer));
                            }
                        }
                    }
                }
            }
        }

        private async void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE Mod Installer Package", Extensions = new List<string>() { "emip" } }
            };
            string path = await sfd.ShowAsync(this);

            if (path != null && path != string.Empty)
            {
                BuildEmip(path);

                if (importedEmipStream != null && importedEmipStream.CanRead)
                    importedEmipStream.Close();

                Close(true);
            }
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void BoxBaseFolder_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateTree();
        }
    }

    public class ModMakerTreeFileInfo : INotifyPropertyChanged
    {
        public string rootPath;
        public string fullPath;
        public string relPath; //this could probably be a prop but whatever it's already here

        public ObservableCollection<ModMakerTreeReplacerInfo> Replacers { get; }
        public string DisplayText { get => relPath; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ModMakerTreeFileInfo(string fullPath)
        {
            Replacers = new ObservableCollection<ModMakerTreeReplacerInfo>();
            this.fullPath = fullPath;
            rootPath = "";
            relPath = fullPath;
        }

        public ModMakerTreeFileInfo(string fullPath, string rootPath)
        {
            Replacers = new ObservableCollection<ModMakerTreeReplacerInfo>();
            this.fullPath = fullPath;
            this.rootPath = rootPath;
            this.relPath = "";

            UpdateRootPath(rootPath);
        }

        public void UpdateRootPath(string rootPath)
        {
            this.rootPath = rootPath;

            if (IsPathRootedSafe(rootPath))
                relPath = Path.GetRelativePath(rootPath, fullPath);
            else
                relPath = fullPath;

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

    public class ModMakerTreeReplacerInfo
    {
        public bool isBundle;
        public AssetID assetId;
        public AssetsReplacer assetsReplacer;
        //public BundleReplacer bundleReplacer;

        public string DisplayText { get => ToString(); }

        public ModMakerTreeReplacerInfo(AssetID assetId, AssetsReplacer assetsReplacer)
        {
            isBundle = false;
            this.assetId = assetId;
            this.assetsReplacer = assetsReplacer;
        }

        public override string ToString()
        {
            if (!isBundle)
            {
                if (assetsReplacer is AssetsRemover)
                    return $"Remove path id {assetsReplacer.GetPathID()}";
                else //if (replacer is AssetsReplacerFromMemory || replacer is AssetsReplacerFromStream)
                    return $"Replace path id {assetsReplacer.GetPathID()}";
            }
            else
            {
                //todo
                return "no u";
            }
        }
    }
}
