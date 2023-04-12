using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class MainWindow : Window
    {
        public BundleWorkspace Workspace { get; }
        public AssetsManager am { get => Workspace.am; }
        public BundleFileInstance BundleInst { get => Workspace.BundleInst; }

        //private Dictionary<string, BundleReplacer> newFiles;
        private bool changesUnsaved; // sets false after saving
        private bool changesMade; // stays true even after saving
        private bool ignoreCloseEvent;

        //public ObservableCollection<ComboBoxItem> comboItems;

        public MainWindow()
        {
            // has to happen BEFORE initcomponent
            Workspace = new BundleWorkspace();
            Initialized += MainWindow_Initialized;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            menuOpen.Click += MenuOpen_Click;
            menuLoadPackageFile.Click += MenuLoadPackageFile_Click;
            menuClose.Click += MenuClose_Click;
            menuSave.Click += MenuSave_Click;
            menuCompress.Click += MenuCompress_Click;
            menuExit.Click += MenuExit_Click;
            menuToggleDarkTheme.Click += MenuToggleDarkTheme_Click;
            menuToggleCpp2Il.Click += MenuToggleCpp2Il_Click;
            menuAbout.Click += MenuAbout_Click;
            btnExport.Click += BtnExport_Click;
            btnImport.Click += BtnImport_Click;
            btnRemove.Click += BtnRemove_Click;
            btnInfo.Click += BtnInfo_Click;
            btnExportAll.Click += BtnExportAll_Click;
            btnImportAll.Click += BtnImportAll_Click;
            btnRename.Click += BtnRename_Click;
            Closing += MainWindow_Closing;

            changesUnsaved = false;
            changesMade = false;
            ignoreCloseEvent = false;

            AddHandler(DragDrop.DropEvent, Drop);

            ThemeHandler.UseDarkTheme = ConfigurationManager.Settings.UseDarkTheme;
        }

        private async void MainWindow_Initialized(object? sender, EventArgs e)
        {
            string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
            {
                am.LoadClassPackage(classDataPath);
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "Missing classdata.tpk by exe.\nPlease make sure it exists.");
                Close();
                Environment.Exit(1);
            }
        }

        async void OpenFiles(string[] files)
        {
            string selectedFile = files[0];

            DetectedFileType fileType = AssetBundleDetector.DetectFileType(selectedFile);

            CloseAllFiles();

            // can you even have split bundles?
            if (fileType != DetectedFileType.Unknown)
            {
                if (selectedFile.EndsWith(".split0"))
                {
                    string? splitFilePath = await AskLoadSplitFile(selectedFile);
                    if (splitFilePath == null)
                        return;
                    else
                        selectedFile = splitFilePath;
                }
            }

            if (fileType == DetectedFileType.AssetsFile)
            {
                AssetsFileInstance fileInst = am.LoadAssetsFile(selectedFile, true);

                if (!await LoadOrAskTypeData(fileInst))
                    return;

                List<AssetsFileInstance> fileInstances = new List<AssetsFileInstance>();
                fileInstances.Add(fileInst);

                if (files.Length > 1)
                {
                    for (int i = 1; i < files.Length; i++)
                    {
                        string otherSelectedFile = files[i];
                        DetectedFileType otherFileType = AssetBundleDetector.DetectFileType(otherSelectedFile);
                        if (otherFileType == DetectedFileType.AssetsFile)
                        {
                            try
                            {
                                fileInstances.Add(am.LoadAssetsFile(otherSelectedFile, true));
                            }
                            catch
                            {
                                // no warning if the file didn't load but was detected as an assets file
                                // this is so you can select the entire _Data folder and any false positives
                                // don't message the user since it's basically a given
                            }
                        }
                    }
                }

                InfoWindow info = new InfoWindow(am, fileInstances, false);
                info.Show();
            }
            else if (fileType == DetectedFileType.BundleFile)
            {
                BundleFileInstance bundleInst = am.LoadBundleFile(selectedFile, false);

                if (AssetBundleUtil.IsBundleDataCompressed(bundleInst.file))
                {
                    AskLoadCompressedBundle(bundleInst);
                }
                else
                {
                    LoadBundle(bundleInst);
                }
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "This doesn't seem to be an assets file or bundle.");
            }
        }

        void Drop(object? sender, DragEventArgs e)
        {
            string[] files = e.Data.GetFileNames().ToArray();

            if (files == null || files.Length == 0)
                return;

            OpenFiles(files);
        }

        private async void MenuOpen_Click(object? sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open assets or bundle file";
            ofd.Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } };
            ofd.AllowMultiple = true;
            string[]? files = await ofd.ShowAsync(this);

            if (files == null || files.Length == 0)
                return;

            OpenFiles(files);
        }

        private async void MenuLoadPackageFile_Click(object? sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE Mod Installer Package", Extensions = new List<string>() { "emip" } }
            };

            string[]? fileList = await ofd.ShowAsync(this);

            if (fileList == null || fileList.Length == 0)
                return;

            string emipPath = fileList[0];

            if (emipPath != null && emipPath != string.Empty)
            {
                AssetsFileReader r = new AssetsFileReader(File.OpenRead(emipPath)); //todo close this
                InstallerPackageFile emip = new InstallerPackageFile();
                emip.Read(r);

                LoadModPackageDialog dialog = new LoadModPackageDialog(emip, am);
                await dialog.ShowDialog(this);
            }
        }

        private void MenuAbout_Click(object? sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog(this);
        }

        private async void MenuSave_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndSave();
        }

        private async void MenuCompress_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndCompress();
        }

        private async void MenuClose_Click(object? sender, RoutedEventArgs e)
        {
            await AskForSave();
            CloseAllFiles();
        }

        private async void BtnExport_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            BundleWorkspaceItem item = (BundleWorkspaceItem?)comboBox.SelectedItem;
            if (item == null)
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save as...";
            sfd.InitialFileName = item.Name;

            string? file = await sfd.ShowAsync(this);
            if (file == null)
                return;

            using FileStream fileStream = File.Open(file, FileMode.Create);

            Stream stream = item.Stream;
            stream.Position = 0;
            stream.CopyToCompat(fileStream, stream.Length);
        }

        private async void BtnImport_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst != null)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Open";

                string[] files = await ofd.ShowAsync(this);

                if (files == null || files.Length == 0)
                    return;

                string file = files[0];

                if (file == null)
                    return;

                ImportSerializedDialog dialog = new ImportSerializedDialog();
                bool isSerialized = await dialog.ShowDialog<bool>(this);

                byte[] fileBytes = File.ReadAllBytes(file);
                string fileName = Path.GetFileName(file);

                MemoryStream stream = new MemoryStream(fileBytes);
                Workspace.AddOrReplaceFile(stream, fileName, isSerialized);

                SetBundleControlsEnabled(true, true);
                changesUnsaved = true;
                changesMade = true;
            }
        }

        private async void BtnRemove_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst != null && comboBox.SelectedItem != null)
            {
                BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
                if (item == null)
                    return;

                string origName = item.OriginalName;
                string name = item.Name;
                item.IsRemoved = true;
                Workspace.RemovedFiles.Add(origName);
                Workspace.Files.Remove(item);
                Workspace.FileLookup.Remove(name);

                SetBundleControlsEnabled(true, Workspace.Files.Count > 0);

                changesUnsaved = true;
                changesMade = true;
            }
        }

        private async void BtnInfo_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
            if (item == null)
                return;

            string name = item.Name;

            AssetBundleFile bundleFile = BundleInst.file;

            Stream assetStream = item.Stream;

            DetectedFileType fileType = AssetBundleDetector.DetectFileType(new AssetsFileReader(assetStream), 0);
            assetStream.Position = 0;

            if (fileType == DetectedFileType.AssetsFile)
            {
                string assetMemPath = Path.Combine(BundleInst.path, name);
                AssetsFileInstance fileInst = am.LoadAssetsFile(assetStream, assetMemPath, true);

                if (!await LoadOrAskTypeData(fileInst))
                    return;

                if (BundleInst != null && fileInst.parentBundle == null)
                    fileInst.parentBundle = BundleInst;

                InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, true);
                info.Closing += InfoWindow_Closing;
                info.Show();
            }
            else
            {
                if (item.IsSerialized)
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "Error", "This doesn't seem to be a valid assets file, " +
                                 "although the asset is serialized. Maybe the " +
                                 "file got corrupted or is too new of a version?");
                }
                else
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "Error", "This doesn't seem to be a valid assets file. " +
                                 "If you want to export a non-assets file, " +
                                 "use Export.");
                }
            }
        }

        private async void BtnExportAll_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string? dir = await ofd.ShowAsync(this);

            if (dir == null || dir == string.Empty)
                return;

            for (int i = 0; i < BundleInst.file.BlockAndDirInfo.DirectoryInfos.Length; i++)
            {
                AssetBundleDirectoryInfo dirInf = BundleInst.file.BlockAndDirInfo.DirectoryInfos[i];

                string bunAssetName = dirInf.Name;
                string bunAssetPath = Path.Combine(dir, bunAssetName);

                // create dirs if bundle contains / in path
                if (bunAssetName.Contains("\\") || bunAssetName.Contains("/"))
                {
                    string bunAssetDir = Path.GetDirectoryName(bunAssetPath);
                    if (!Directory.Exists(bunAssetDir))
                    {
                        Directory.CreateDirectory(bunAssetDir);
                    }    
                }

                using FileStream fileStream = File.Open(bunAssetPath, FileMode.Create);

                AssetsFileReader bundleReader = BundleInst.file.DataReader;
                bundleReader.Position = dirInf.Offset;
                bundleReader.BaseStream.CopyToCompat(fileStream, dirInf.DecompressedSize);
            }
        }

        private async void BtnImportAll_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

        }

        private async void BtnRename_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
            if (item == null)
                return;

            // if we rename twice, the "original name" is the current name
            RenameWindow window = new RenameWindow(item.Name);
            string newName = await window.ShowDialog<string>(this);
            if (newName == string.Empty)
                return;

            Workspace.RenameFile(item.Name, newName);

            // reload the text in the selected item preview
            // why not just use propertychangeevent? it's because getting
            // events working and the fact that displaymemberpath isn't
            // supported means more trouble than it's worth. this hack is
            // good enough, despite being jank af.
            Workspace.Files.Add(null);
            comboBox.SelectedItem = null;
            comboBox.SelectedItem = item;
            Workspace.Files.Remove(null);
        }

        private void MenuExit_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void MenuToggleDarkTheme_Click(object? sender, RoutedEventArgs e)
        {
            ConfigurationManager.Settings.UseDarkTheme = !ConfigurationManager.Settings.UseDarkTheme;
            ThemeHandler.UseDarkTheme = ConfigurationManager.Settings.UseDarkTheme;
        }

        private async void MenuToggleCpp2Il_Click(object? sender, RoutedEventArgs e)
        {
            bool useCpp2Il = !ConfigurationManager.Settings.UseCpp2Il;
            ConfigurationManager.Settings.UseCpp2Il = useCpp2Il;

            await MessageBoxUtil.ShowDialog(this, "Note",
                $"Use Cpp2Il is set to: {useCpp2Il.ToString().ToLower()}");
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!changesUnsaved || ignoreCloseEvent)
            {
                e.Cancel = false;
                ignoreCloseEvent = false;
            }
            else
            {
                e.Cancel = true;
                ignoreCloseEvent = true;

                await AskForSave();
                Close(); // calling Close() triggers Closing() again
            }
        }

        private void InfoWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender == null)
                return;

            InfoWindow window = (InfoWindow)sender;

            if (window.Workspace.fromBundle && window.ChangedAssetsDatas != null)
            {
                List<Tuple<AssetsFileInstance, byte[]>> assetDatas = window.ChangedAssetsDatas;

                foreach (var tup in assetDatas)
                {
                    AssetsFileInstance fileInstance = tup.Item1;
                    byte[] assetData = tup.Item2;

                    // remember selected index, when we replace the file it unselects the combobox item
                    int comboBoxSelectedIndex = comboBox.SelectedIndex;

                    string assetName = Path.GetFileName(fileInstance.path);
                    Workspace.AddOrReplaceFile(new MemoryStream(assetData), assetName, true);
                    // unload it so the new version is reloaded when we reopen it
                    am.UnloadAssetsFile(fileInstance.path);

                    // reselect the combobox item
                    comboBox.SelectedIndex = comboBoxSelectedIndex;
                }

                if (assetDatas.Count > 0)
                {
                    changesUnsaved = true;
                    changesMade = true;
                }
            }
        }

        // todo, if stripped load from header (needed for adding new assets)
        private async Task<bool> LoadOrAskTypeData(AssetsFileInstance fileInst)
        {
            string uVer = fileInst.file.Metadata.UnityVersion;
            am.LoadClassDatabaseFromPackage(uVer);
            return true;
        }

        private async Task AskForLocationAndSave()
        {
            if (changesUnsaved && BundleInst != null)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";

                string? file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                if (Path.GetFullPath(file) == Path.GetFullPath(BundleInst.path))
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "File in use", "Since this file is already open in UABEA, you must pick a new file name (sorry!)");
                    return;
                }

                SaveBundle(BundleInst, file);
            }
        }

        private async Task AskForSave()
        {
            if (changesUnsaved && BundleInst != null)
            {
                MessageBoxResult choice = await MessageBoxUtil.ShowDialog(this,
                    "Changes made", "You've modified this file. Would you like to save?",
                    MessageBoxType.YesNo);
                if (choice == MessageBoxResult.Yes)
                {
                    await AskForLocationAndSave();
                }
            }
        }

        private async Task AskForLocationAndCompress()
        {
            if (BundleInst != null)
            {
                // temporary, maybe I should just write to a memory stream or smth
                // edit: looks like uabe just asks you to open a file instead of
                // using your currently opened one, so that may be the workaround
                if (changesMade)
                {
                    string messageBoxTest;
                    if (changesUnsaved)
                    {
                        messageBoxTest =
                            "You've modified this file, but you still haven't saved this bundle file to disk yet. If you want \n" +
                            "to compress the file with changes, please save this bundle now and open that file instead. \n" +
                            "Click Ok to compress the file without changes.";
                    }
                    else
                    {
                        messageBoxTest =
                            "You've modified this file, but only the old file before you made changes is open. If you want to compress the file with \n" +
                            "changes, please close this bundle and open the file you saved. Click Ok to compress the file without changes.";
                    }

                    MessageBoxResult continueWithChanges = await MessageBoxUtil.ShowDialog(
                        this, "Note", messageBoxTest,
                        MessageBoxType.OKCancel);

                    if (continueWithChanges == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";

                string? file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                if (Path.GetFullPath(file) == Path.GetFullPath(BundleInst.path))
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "File in use", "Since this file is already open in UABEA, you must pick a new file name (sorry!)");
                    return;
                }

                const string lz4Option = "LZ4";
                const string lzmaOption = "LZMA";
                const string cancelOption = "Cancel";
                string result = await MessageBoxUtil.ShowDialogCustom(
                    this, "Note", "What compression method do you want to use?\nLZ4: Faster but larger size\nLZMA: Slower but smaller size",
                    lz4Option, lzmaOption, cancelOption);

                AssetBundleCompressionType compType = result switch
                {
                    lz4Option => AssetBundleCompressionType.LZ4,
                    lzmaOption => AssetBundleCompressionType.LZMA,
                    _ => AssetBundleCompressionType.None
                };

                if (compType != AssetBundleCompressionType.None)
                {
                    ProgressWindow progressWindow = new ProgressWindow("Compressing...");

                    Thread thread = new Thread(new ParameterizedThreadStart(CompressBundle));
                    object[] threadArgs =
                    {
                        BundleInst,
                        file,
                        compType,
                        progressWindow.Progress
                    };
                    thread.Start(threadArgs);

                    await progressWindow.ShowDialog(this);
                }
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "Note", "Please open a bundle file before using compress.");
            }
        }

        private async Task<string?> AskLoadSplitFile(string selectedFile)
        {
            MessageBoxResult splitRes = await MessageBoxUtil.ShowDialog(this,
                "Split file detected", "This file ends with .split0. Create merged file?\n",
                MessageBoxType.YesNoCancel);

            if (splitRes == MessageBoxResult.Yes)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Select location for merged file";
                sfd.Directory = Path.GetDirectoryName(selectedFile);
                sfd.InitialFileName = Path.GetFileName(selectedFile.Substring(0, selectedFile.Length - ".split0".Length));
                string splitFilePath = await sfd.ShowAsync(this);

                if (splitFilePath == null || splitFilePath == string.Empty)
                    return null;

                using (FileStream mergeFile = File.Open(splitFilePath, FileMode.Create))
                {
                    int idx = 0;
                    string thisSplitFileNoNum = selectedFile.Substring(0, selectedFile.Length - 1);
                    string thisSplitFileNum = selectedFile;
                    while (File.Exists(thisSplitFileNum))
                    {
                        using (FileStream thisSplitFile = File.OpenRead(thisSplitFileNum))
                        {
                            thisSplitFile.CopyTo(mergeFile);
                        }

                        idx++;
                        thisSplitFileNum = $"{thisSplitFileNoNum}{idx}";
                    };
                }
                return splitFilePath;
            }
            else if (splitRes == MessageBoxResult.No)
            {
                return selectedFile;
            }
            else //if (splitRes == MessageBoxResult.Cancel)
            {
                return null;
            }
        }

        private async void AskLoadCompressedBundle(BundleFileInstance bundleInst)
        {
            string decompSize = Extensions.GetFormattedByteSize(GetBundleDataDecompressedSize(bundleInst.file));

            const string fileOption = "File";
            const string memoryOption = "Memory";
            const string cancelOption = "Cancel";
            string result = await MessageBoxUtil.ShowDialogCustom(
                this, "Note", "This bundle is compressed. Decompress to file or memory?\nSize: " + decompSize,
                fileOption, memoryOption, cancelOption);

            if (result == fileOption)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";
                sfd.Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } };

                string? savePath;
                while (true)
                {
                    savePath = await sfd.ShowAsync(this);

                    if (savePath == "" || savePath == null)
                        return;

                    if (Path.GetFullPath(savePath) == Path.GetFullPath(bundleInst.path))
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "File in use", "Since this file is already open in UABEA, you must pick a new file name (sorry!)");
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                DecompressToFile(bundleInst, savePath);
            }
            else if (result == memoryOption)
            {
                // for lz4 block reading
                if (bundleInst.file.DataIsCompressed)
                {
                    DecompressToMemory(bundleInst);
                }
            }
            else //if (result == cancelOption || result == closeOption)
            {
                return;
            }

            LoadBundle(bundleInst);
        }

        private void DecompressToFile(BundleFileInstance bundleInst, string savePath)
        {
            AssetBundleFile bundle = bundleInst.file;

            FileStream bundleStream = File.Open(savePath, FileMode.Create);
            bundle.Unpack(new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream));

            bundle.Close();
            bundleInst.file = newBundle;
        }

        private void DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream));

            bundle.Close();
            bundleInst.file = newBundle;
        }

        private void LoadBundle(BundleFileInstance bundleInst)
        {
            Workspace.Reset(bundleInst);

            comboBox.Items = Workspace.Files;
            comboBox.SelectedIndex = 0;

            lblFileName.Text = bundleInst.name;

            SetBundleControlsEnabled(true, Workspace.Files.Count > 0);
        }

        private void SaveBundle(BundleFileInstance bundleInst, string path)
        {
            List<BundleReplacer> replacers = Workspace.GetReplacers();
            using (FileStream fs = File.Open(path, FileMode.Create))
            using (AssetsFileWriter w = new AssetsFileWriter(fs))
            {
                bundleInst.file.Write(w, replacers.ToList());
            }
            changesUnsaved = false;
        }

        private void CompressBundle(object? args)
        {
            object[] argsArr = (object[])args!;

            var bundleInst = (BundleFileInstance)argsArr[0];
            var path = (string)argsArr[1];
            var compType = (AssetBundleCompressionType)argsArr[2];
            var progress = (IAssetBundleCompressProgress)argsArr[3];

            using (FileStream fs = File.Open(path, FileMode.Create))
            using (AssetsFileWriter w = new AssetsFileWriter(fs))
            {
                bundleInst.file.Pack(bundleInst.file.Reader, w, compType, true, progress);
            }
        }

        private void CloseAllFiles()
        {
            //newFiles.Clear();
            changesUnsaved = false;
            changesMade = false;

            am.UnloadAllAssetsFiles(true);
            am.UnloadAllBundleFiles();

            SetBundleControlsEnabled(false, true);

            Workspace.Reset(null);

            lblFileName.Text = "No file opened.";
        }

        private void SetBundleControlsEnabled(bool enabled, bool hasAssets = false)
        {
            // buttons that should be enabled only if there are assets they can interact with
            if (hasAssets)
            {
                btnExport.IsEnabled = enabled;
                btnRemove.IsEnabled = enabled;
                btnRename.IsEnabled = enabled;
                btnInfo.IsEnabled = enabled;
                btnExportAll.IsEnabled = enabled;
            }

            // always enable / disable no matter if there's assets or not
            comboBox.IsEnabled = enabled;
            btnImport.IsEnabled = enabled;
            btnImportAll.IsEnabled = enabled;
        }

        private long GetBundleDataDecompressedSize(AssetBundleFile bundleFile)
        {
            long totalSize = 0;
            foreach (AssetBundleDirectoryInfo dirInf in bundleFile.BlockAndDirInfo.DirectoryInfos)
            {
                totalSize += dirInf.DecompressedSize;
            }
            return totalSize;
        }
    }
}
