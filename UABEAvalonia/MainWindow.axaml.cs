using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace UABEAvalonia
{
    public class MainWindow : Window
    {
        //controls
        private MenuItem menuOpen;
        private MenuItem menuLoadPackageFile;
        private MenuItem menuClose;
        private MenuItem menuSave;
        private MenuItem menuCompress;
        private MenuItem menuCreateStandaloneInstaller;
        private MenuItem menuCreatePackageFile;
        private MenuItem menuExit;
        private MenuItem menuEditTypeDatabase;
        private MenuItem menuEditTypePackage;
        private MenuItem menuAbout;
        private TextBlock lblFileName;
        private ComboBox comboBox;
        private Button btnExport;
        private Button btnImport;
        private Button btnInfo;

        private AssetsManager am;
        private BundleFileInstance bundleInst;

        private Dictionary<string, BundleReplacer> newFiles;

        public ObservableCollection<ComboBoxItem> comboItems;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            menuOpen = this.FindControl<MenuItem>("menuOpen");
            menuLoadPackageFile = this.FindControl<MenuItem>("menuLoadPackageFile");
            menuClose = this.FindControl<MenuItem>("menuClose");
            menuSave = this.FindControl<MenuItem>("menuSave");
            menuCompress = this.FindControl<MenuItem>("menuCompress");
            menuCreateStandaloneInstaller = this.FindControl<MenuItem>("menuCreateStandaloneInstaller");
            menuCreatePackageFile = this.FindControl<MenuItem>("menuCreatePackageFile");
            menuExit = this.FindControl<MenuItem>("menuExit");
            menuEditTypeDatabase = this.FindControl<MenuItem>("menuEditTypeDatabase");
            menuEditTypePackage = this.FindControl<MenuItem>("menuEditTypePackage");
            menuAbout = this.FindControl<MenuItem>("menuAbout");
            lblFileName = this.FindControl<TextBlock>("lblFileName");
            comboBox = this.FindControl<ComboBox>("comboBox");
            btnExport = this.FindControl<Button>("btnExport");
            btnImport = this.FindControl<Button>("btnImport");
            btnInfo = this.FindControl<Button>("btnInfo");
            //generated events
            menuOpen.Click += MenuOpen_Click;
            menuAbout.Click += MenuAbout_Click;
            btnExport.Click += BtnExport_Click;
            btnImport.Click += BtnImport_Click;
            btnInfo.Click += BtnInfo_Click;

            //todo, designer tries to run this of course so we need some way
            //to detect designer vs actual program, this works for now but
            //will silently fail if classdata is missing
            am = new AssetsManager();
            if (File.Exists("classdata.tpk"))
            {
                am.LoadClassPackage("classdata.tpk");
            }

            newFiles = new Dictionary<string, BundleReplacer>();
        }

        private async void MenuOpen_Click(object? sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open assets or bundle file";
            ofd.Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } };
            string[] files = await ofd.ShowAsync(this);

            if (files.Length > 0)
            {
                string selectedFile = files[0];

                DetectedFileType fileType = AssetBundleDetector.DetectFileType(selectedFile);

                if (fileType == DetectedFileType.AssetsFile)
                {
                    string assetName = Path.GetFileNameWithoutExtension(selectedFile);

                    AssetsFileInstance fileInst = am.LoadAssetsFile(selectedFile, true);
                    am.LoadClassDatabaseFromPackage(fileInst.file.typeTree.unityVersion);

                    InfoWindow info = new InfoWindow(am, fileInst, assetName, false);
                    info.Show();
                }
                else if (fileType == DetectedFileType.BundleFile)
                {
                    bundleInst = am.LoadBundleFile(selectedFile, false);
                    //don't pester user to decompress if it's only the header that is compressed
                    if (AssetBundleUtil.IsBundleDataCompressed(bundleInst.file))
                    {
                        AskLoadCompressedBundle(bundleInst);
                    }
                    else
                    {
                        if ((bundleInst.file.bundleHeader6.flags & 0x3F) != 0) //header is compressed (most likely)
                            DecompressToMemory(bundleInst);
                        LoadBundle(bundleInst);
                    }
                }
            }
        }

        private async void AskLoadCompressedBundle(BundleFileInstance bundleInst)
        {
            const string fileOption = "File";
            const string memoryOption = "Memory";
            const string cancelOption = "Cancel";
            string result = await MessageBoxUtil.ShowDialogCustom(
                this, "Note", "This bundle is compressed. Decompress to file or memory?",
                fileOption, memoryOption, cancelOption);

            if (result == fileOption)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";
                sfd.Filters = new List<FileDialogFilter>() { new FileDialogFilter() { Name = "All files", Extensions = new List<string>() { "*" } } };
                string savePath = await sfd.ShowAsync(this);

                if (savePath == null)
                    return;

                DecompressToFile(bundleInst, savePath);
            }
            else if (result == memoryOption)
            {
                DecompressToMemory(bundleInst);
            }
            else if (result == cancelOption)
            {
                return;
            }

            LoadBundle(bundleInst);
        }

        private void DecompressToFile(BundleFileInstance bundleInst, string savePath)
        {
            AssetBundleFile bundle = bundleInst.file;

            FileStream bundleStream = File.Open(savePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            bundleInst.file = newBundle;
            bundleInst.path = savePath;
        }

        private void DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            bundleInst.file = newBundle;
        }

        private void LoadBundle(BundleFileInstance bundleInst)
        {
            var infos = bundleInst.file.bundleInf6.dirInf;
            comboItems = new ObservableCollection<ComboBoxItem>();
            for (int i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                comboItems.Add(new ComboBoxItem()
                {
                    Content = info.name,
                    Tag = i
                });
            }
            comboBox.Items = comboItems;
            comboBox.SelectedIndex = 0;

            lblFileName.Text = bundleInst.name;
        }

        private void MenuAbout_Click(object? sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog(this);
        }

        private async void BtnExport_Click(object? sender, RoutedEventArgs e)
        {
            if (bundleInst != null && comboBox.SelectedItem != null)
            {
                int index = (int)((ComboBoxItem)comboBox.SelectedItem).Tag;

                string bunAssetName = bundleInst.file.bundleInf6.dirInf[index].name;
                byte[] assetData = BundleHelper.LoadAssetDataFromBundle(bundleInst.file, index);

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";

                string file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                File.WriteAllBytes(file, assetData);
            }
        }

        private async void BtnImport_Click(object? sender, RoutedEventArgs e)
        {
            if (bundleInst != null)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Open";

                string[] files = await ofd.ShowAsync(this);

                if (files.Length == 0)
                    return;

                string file = files[0];

                if (file == null)
                    return;

                byte[] fileBytes = File.ReadAllBytes(file);
                string fileName = Path.GetFileName(file);

                newFiles[fileName] = AssetImportExport.CreateBundleReplacer(fileName, true, fileBytes);

                //todo handle overwriting
                comboItems.Add(new ComboBoxItem()
                {
                    Content = fileName,
                    Tag = comboItems.Count
                });
                comboBox.SelectedIndex = comboItems.Count - 1;
            }
        }

        private void BtnInfo_Click(object? sender, RoutedEventArgs e)
        {
            if (bundleInst != null && comboBox.SelectedItem != null)
            {
                int index = (int)((ComboBoxItem)comboBox.SelectedItem).Tag;

                string bunAssetName = bundleInst.file.bundleInf6.dirInf[index].name;
                byte[] assetData = BundleHelper.LoadAssetDataFromBundle(bundleInst.file, index);
                MemoryStream assetStream = new MemoryStream(assetData);

                AssetsFileInstance fileInst = am.LoadAssetsFile(bundleInst, assetStream, bundleInst.path, true);
                am.LoadClassDatabaseFromPackage(fileInst.file.typeTree.unityVersion);
                InfoWindow info = new InfoWindow(am, fileInst, bunAssetName, true);
                info.Closing += InfoWindowClosing;
                info.Show();
            }
        }

        private void InfoWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender == null)
                return;

            InfoWindow window = (InfoWindow)sender;
            string assetName = window.AssetsFileName;
            byte[] assetData = window.FinalAssetData;
            if (assetData == null) return;
            
            BundleReplacer replacer = AssetImportExport.CreateBundleReplacer(assetName, true, assetData);
            newFiles[assetName] = replacer;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
