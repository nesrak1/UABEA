using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;

namespace UABEAvalonia
{
    public class InfoWindow : Window
    {
        //controls
        private MenuItem menuAdd;
        private MenuItem menuSave;
        private MenuItem menuCreateStandaloneInstaller;
        private MenuItem menuCreatePackageFile;
        private MenuItem menuClose;
        private MenuItem menuSearchByName;
        private MenuItem menuContinueSearch;
        private MenuItem menuGoToAsset;
        private MenuItem menuDependencies;
        private MenuItem menuContainers;
        private Button btnViewData;
        private Button btnExportRaw;
        private Button btnExportDump;
        private Button btnPlugin;
        private Button btnImportRaw;
        private Button btnImportDump;
        private Button btnRemove;
        private DataGrid dataGrid;
        private TextBox boxName;
        private TextBox boxPathId;
        private TextBox boxFileId;
        private TextBox boxType;

        //todo, rework all this
        public AssetWorkspace Workspace { get; }
        public AssetsManager am { get => Workspace.am; }
        public AssetsFileInstance assetsFile { get => Workspace.mainFile; }
        public bool fromBundle { get => Workspace.fromBundle; }

        public string AssetsFileName { get => Workspace.AssetsFileName; }
        public byte[] FinalAssetData { get; set; }

        private ObservableCollection<AssetInfoDataGridItem> dataGridItems;

        private PluginManager pluginManager;

        //for preview
        public InfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            menuAdd = this.FindControl<MenuItem>("menuAdd");
            menuSave = this.FindControl<MenuItem>("menuSave");
            menuCreateStandaloneInstaller = this.FindControl<MenuItem>("menuCreateStandaloneInstaller");
            menuCreatePackageFile = this.FindControl<MenuItem>("menuCreatePackageFile");
            menuClose = this.FindControl<MenuItem>("menuClose");
            menuSearchByName = this.FindControl<MenuItem>("menuSearchByName");
            menuContinueSearch = this.FindControl<MenuItem>("menuContinueSearch");
            menuGoToAsset = this.FindControl<MenuItem>("menuGoToAsset");
            menuDependencies = this.FindControl<MenuItem>("menuDependencies");
            menuContainers = this.FindControl<MenuItem>("menuContainers");
            btnViewData = this.FindControl<Button>("btnViewData");
            btnExportRaw = this.FindControl<Button>("btnExportRaw");
            btnExportDump = this.FindControl<Button>("btnExportDump");
            btnPlugin = this.FindControl<Button>("btnPlugin");
            btnImportRaw = this.FindControl<Button>("btnImportRaw");
            btnImportDump = this.FindControl<Button>("btnImportDump");
            btnRemove = this.FindControl<Button>("btnRemove");
            dataGrid = this.FindControl<DataGrid>("dataGrid");
            boxName = this.FindControl<TextBox>("boxName");
            boxPathId = this.FindControl<TextBox>("boxPathId");
            boxFileId = this.FindControl<TextBox>("boxFileId");
            boxType = this.FindControl<TextBox>("boxType");
            //generated events
            menuSave.Click += MenuSave_Click;
            menuCreatePackageFile.Click += MenuCreatePackageFile_Click;
            menuClose.Click += MenuClose_Click;
            btnViewData.Click += BtnViewData_Click;
            btnExportRaw.Click += BtnExportRaw_Click;
            btnExportDump.Click += BtnExportDump_Click;
            btnImportRaw.Click += BtnImportRaw_Click;
            btnImportDump.Click += BtnImportDump_Click;
            btnPlugin.Click += BtnPlugin_Click;
            dataGrid.SelectionChanged += DataGrid_SelectionChanged;
        }

        public InfoWindow(AssetsManager assetsManager, AssetsFileInstance assetsFile, string name, bool fromBundle) : this()
        {
            Workspace = new AssetWorkspace(assetsManager, assetsFile, fromBundle, name);
            Workspace.ItemUpdated += Workspace_ItemUpdated;

            MakeDataGridItems();
            dataGrid.Items = dataGridItems;

            pluginManager = new PluginManager();
            pluginManager.LoadPluginsInDirectory("plugins");

            //this.DataContext = this;
        }

        private async void BtnViewData_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            if (gridItem.Size > 500000)
            {
                var bigFileBox = MessageBoxManager
                    .GetMessageBoxCustomWindow(new MessageBoxCustomParams
                    {
                        Style = Style.Windows,
                        ContentHeader = "Warning",
                        ContentMessage = "The asset you are about to open is very big and may take a lot of time and memory.",
                        ButtonDefinitions = new[] {
                            new ButtonDefinition {Name = "Continue anyway", Type = ButtonType.Colored},
                            new ButtonDefinition {Name = "Cancel", Type = ButtonType.Default}
                        }
                    });
                string result = await bigFileBox.Show();
                if (result == "Cancel")
                    return;
            }

            AssetTypeValueField field = GetSelectedFieldReplaced();
            DataWindow data = new DataWindow(field);
            data.Show();
        }

        private async void BtnExportRaw_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save As";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Raw Unity Asset", Extensions = new List<string>() { "dat" } }
            };
            string file = await sfd.ShowAsync(this);

            if (file != null && file != string.Empty)
            {
                using (FileStream fs = File.OpenWrite(file))
                {
                    AssetImportExport dumper = new AssetImportExport();
                    dumper.DumpRawAsset(fs, assetsFile.file, GetSelectedInfo());
                }
            }
        }

        private async void BtnExportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save As";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE text dump", Extensions = new List<string>() { "txt" } },
                new FileDialogFilter() { Name = "UABE json dump", Extensions = new List<string>() { "json" } }
            };

            string file = await sfd.ShowAsync(this);

            if (file != null && file != string.Empty)
            {
                if (file.EndsWith(".json"))
                {
                    await MessageBoxUtil.ShowDialog(this, "Not implemented", "There's no json dump support yet, sorry. Exporting as .txt anyway.");
                    file = file.Substring(0, file.Length - 5) + ".txt";
                }

                using (FileStream fs = File.OpenWrite(file))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    AssetImportExport dumper = new AssetImportExport();
                    dumper.DumpTextAsset(sw, GetSelectedBaseField());
                }
            }
        }

        private async void BtnImportRaw_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Raw Unity Asset", Extensions = new List<string>() { "dat" } }
            };

            string[] fileList = await ofd.ShowAsync(this);
            if (fileList.Length == 0)
                return;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                using (FileStream fs = File.OpenRead(file))
                {
                    AssetFileInfoEx selectedInfo = GetSelectedInfo();
                    long selectedId = selectedInfo.index;

                    AssetImportExport importer = new AssetImportExport();
                    byte[] bytes = importer.ImportRawAsset(fs);

                    AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(assetsFile.file, selectedInfo, bytes);
                    Workspace.AddReplacer(Workspace.mainFile, replacer, new MemoryStream(bytes));
                    //newAssets[selectedId] = replacer;
                    //newAssetDatas[selectedId] = new MemoryStream(bytes);

                    SetSelectedFieldModified();
                }
            }
        }

        private async void BtnImportDump_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "UABE text dump", Extensions = new List<string>() { "txt" } },
                new FileDialogFilter() { Name = "UABE json dump", Extensions = new List<string>() { "json" } }
            };

            string[] fileList = await ofd.ShowAsync(this);
            if (fileList.Length == 0)
                return;

            string file = fileList[0];

            if (file != null && file != string.Empty)
            {
                if (file.EndsWith(".json"))
                {
                    await MessageBoxUtil.ShowDialog(this, "Not implemented", "There's no json dump support yet, sorry.");
                    return;
                }

                using (FileStream fs = File.OpenRead(file))
                using (StreamReader sr = new StreamReader(fs))
                {
                    AssetFileInfoEx selectedInfo = GetSelectedInfo();
                    long selectedId = selectedInfo.index;

                    AssetImportExport importer = new AssetImportExport();
                    byte[]? bytes = importer.ImportTextAsset(sr);

                    if (bytes == null)
                    {
                        await MessageBoxUtil.ShowDialog(this, "Parse error", "Something went wrong when reading the dump file.");
                        return;
                    }

                    AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(assetsFile.file, selectedInfo, bytes);
                    Workspace.AddReplacer(Workspace.mainFile, replacer, new MemoryStream(bytes));

                    SetSelectedFieldModified();
                }
            }
        }

        private async void BtnPlugin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            List<AssetExternal> exts = GetSelectedExternalsReplaced(true);
            PluginWindow plug = new PluginWindow(this, Workspace, exts, pluginManager);
            await plug.ShowDialog(this);
        }

        private async void MenuSave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveFile();
            ClearModified();
            Workspace.Modified = false;
        }

        private async void MenuCreatePackageFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ModMakerDialog dialog = new ModMakerDialog(Workspace);
            await dialog.ShowDialog(this);
        }

        private async void MenuClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (Workspace.Modified)
            {
                ButtonResult choice = await MessageBoxUtil.ShowDialog(this, "Changes made", "You've modified this file. Would you like to save?",
                                                                      ButtonEnum.YesNo);
                if (choice == ButtonResult.Yes)
                {
                    await SaveFile();
                }
            }
            CloseFile();
        }

        private async Task SaveFile()
        {
            List<AssetsReplacer> newAssetsList = Workspace.NewAssets.Values.ToList();

            if (fromBundle)
            {
                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    assetsFile.file.Write(w, 0, newAssetsList, 0);
                    FinalAssetData = ms.ToArray();
                }
            }
            else
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save as...";

                string file = await sfd.ShowAsync(this);

                if (file == null)
                    return;

                using (FileStream fs = File.OpenWrite(file))
                using (AssetsFileWriter w = new AssetsFileWriter(fs))
                {
                    assetsFile.file.Write(w, 0, newAssetsList, 0);
                }
            }
        }

        private void CloseFile()
        {
            am.files.Remove(assetsFile);
            assetsFile.file.reader.Close();
            Close();
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var gridItem = (AssetInfoDataGridItem)dataGrid.SelectedItem;
            boxName.Text = gridItem.Name;
            boxPathId.Text = gridItem.PathID.ToString();
            boxFileId.Text = gridItem.FileID.ToString();
            boxType.Text = $"0x{gridItem.TypeID:X8} ({gridItem.Type})";
        }

        private ObservableCollection<AssetInfoDataGridItem> MakeDataGridItems()
        {
            dataGridItems = new ObservableCollection<AssetInfoDataGridItem>();

            LoadAllAssetsWithDeps();

            foreach (AssetExternal ext in Workspace.LoadedAssets)
            {
                AssetsFile thisFile = ext.file.file;
                AssetFileInfoEx thisInfo = ext.info;
                bool usingTypeTree = ext.file.file.typeTree.hasTypeTree;

                string name;
                string container;
                string type;
                int fileId;
                long pathId;
                int size;
                string modified;

                ClassDatabaseType cldbType = AssetHelper.FindAssetClassByID(am.classFile, thisInfo.curFileType);
                name = AssetHelper.GetAssetNameFast(thisFile, am.classFile, thisInfo); //handles both cldb and typetree
                container = string.Empty;
                fileId = Workspace.LoadedFiles.IndexOf(ext.file); //todo faster lookup THIS IS JUST FOR TESTING
                pathId = thisInfo.index;
                size = (int)thisInfo.curFileSize;
                modified = "";

                if (usingTypeTree)
                {
                    Type_0D ttType = thisFile.typeTree.unity5Types[thisInfo.curFileTypeOrIndex];
                    if (ttType.typeFieldsEx.Length != 0)
                    {
                        type = ttType.typeFieldsEx[0].GetTypeString(ttType.stringTable);
                    }
                    else
                    {
                        if (cldbType != null)
                            type = cldbType.name.GetString(am.classFile);
                        else
                            type = $"0x{thisInfo.curFileType:X8}";
                    }
                }
                else
                {
                    if (cldbType != null)
                        type = cldbType.name.GetString(am.classFile);
                    else
                        type = $"0x{thisInfo.curFileType:X8}";
                }

                if (thisInfo.curFileType == 0x01)
                {
                    name = $"GameObject {name}";
                }
                if (name == string.Empty)
                {
                    name = "Unnamed asset";
                }

                var item = new AssetInfoDataGridItem()
                {
                    Name = name,
                    Container = container,
                    Type = type,
                    TypeID = thisInfo.curFileType,
                    FileID = fileId,
                    PathID = pathId,
                    Size = size,
                    Modified = modified
                };

                dataGridItems.Add(item);
            }
            return dataGridItems;
        }

        private void LoadAllAssetsWithDeps()
        {
            HashSet<string> fileNames = new HashSet<string>();
            RecurseGetAllAssets(assetsFile, Workspace.LoadedAssets, Workspace.LoadedFiles, fileNames);
        }

        private void RecurseGetAllAssets(AssetsFileInstance fromFile, List<AssetExternal> exts, List<AssetsFileInstance> files, HashSet<string> fileNames)
        {
            files.Add(fromFile);
            fileNames.Add(fromFile.path);

            foreach (AssetFileInfoEx info in fromFile.table.assetFileInfo)
            {
                exts.Add(am.GetExtAsset(fromFile, 0, info.index, true));
            }

            for (int i = 0; i < fromFile.dependencies.Count; i++)
            {
                AssetsFileInstance dep = fromFile.GetDependency(am, i);
                if (dep == null)
                    continue;
                string depPath = dep.path.ToLower();
                if (!fileNames.Contains(depPath))
                {
                    RecurseGetAllAssets(dep, exts, files, fileNames);
                }
                else
                {
                    continue;
                }
            }
        }

        private async Task<bool> FailIfNothingSelected()
        {
            if (dataGrid.SelectedItem == null)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                {
                    Style = Style.Windows,
                    ContentHeader = "Note",
                    ContentMessage = "No item selected."
                }).ShowDialog(this);
                return true;
            }
            return false;
        }

        private AssetInfoDataGridItem GetSelectedGridItem()
        {
            return (AssetInfoDataGridItem)dataGrid.SelectedItem;
        }

        private List<AssetInfoDataGridItem> GetSelectedGridItems()
        {
            return dataGrid.SelectedItems.Cast<AssetInfoDataGridItem>().ToList();
        }

        private AssetFileInfoEx GetSelectedInfo()
        {
            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            return am.GetExtAsset(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID).info;
        }

        private List<AssetFileInfoEx> GetSelectedInfos()
        {
            List<AssetInfoDataGridItem> gridItems = GetSelectedGridItems();
            List<AssetFileInfoEx> infos = new List<AssetFileInfoEx>();
            foreach (var gridItem in gridItems)
            {
                infos.Add(am.GetExtAsset(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID).info);
            }
            return infos;
        }

        private AssetTypeValueField GetSelectedBaseField()
        {
            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            return am.GetExtAsset(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID).instance.GetBaseField();
        }

        private List<AssetTypeValueField> GetSelectedBaseFields()
        {
            List<AssetInfoDataGridItem> gridItems = GetSelectedGridItems();
            List<AssetTypeValueField> fields = new List<AssetTypeValueField>();
            foreach (var gridItem in gridItems)
            {
                fields.Add(am.GetExtAsset(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID).instance.GetBaseField());
            }
            return fields;
        }

        private AssetExternal GetSelectedExternalReplaced(bool onlyInfo = false)
        {
            AssetInfoDataGridItem gridItem = GetSelectedGridItem();

            AssetID assetId = new AssetID(Workspace.mainFile.path, gridItem.PathID);
            if (Workspace.NewAssetDatas.ContainsKey(assetId))
            {
                return am.GetExtAssetNewData(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID, Workspace.NewAssetDatas[assetId], onlyInfo);
            }
            else
            {
                return am.GetExtAsset(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID, onlyInfo);
            }
        }

        private List<AssetExternal> GetSelectedExternalsReplaced(bool onlyInfo = false)
        {
            List<AssetInfoDataGridItem> gridItems = GetSelectedGridItems();
            List<AssetExternal> exts = new List<AssetExternal>();
            foreach (var gridItem in gridItems)
            {
                AssetID assetId = new AssetID(Workspace.mainFile.path, gridItem.PathID);
                if (Workspace.NewAssetDatas.ContainsKey(assetId))
                {
                    exts.Add(am.GetExtAssetNewData(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID, Workspace.NewAssetDatas[assetId], onlyInfo));
                }
                else
                {
                    exts.Add(am.GetExtAsset(Workspace.LoadedFiles[gridItem.FileID], 0, gridItem.PathID, onlyInfo));
                }
            }
            return exts;
        }

        private AssetTypeValueField GetSelectedFieldReplaced()
        {
            return GetSelectedExternalReplaced().instance.GetBaseField();
        }

        private List<AssetTypeValueField> GetSelectedFieldsReplaced()
        {
            List<AssetExternal> exts = GetSelectedExternalsReplaced();
            List<AssetTypeValueField> fields = new List<AssetTypeValueField>();
            foreach (var ext in exts)
            {
                fields.Add(ext.instance.GetBaseField());
            }
            return fields;
        }

        private void SetSelectedFieldModified()
        {
            AssetInfoDataGridItem gridItem = GetSelectedGridItem();
            gridItem.Modified = "*";
            gridItem.Update();
        }

        private void SetFieldModified(AssetInfoDataGridItem gridItem)
        {
            gridItem.Modified = "*";
            gridItem.Update();
        }

        private void ClearModified()
        {
            foreach (AssetInfoDataGridItem gridItem in dataGrid.Items)
            {
                if (gridItem.Modified != "")
                {
                    gridItem.Modified = "";
                    gridItem.Update();
                }
            }
        }

        private void Workspace_ItemUpdated(AssetID updatedAssetId)
        {
            var gridItem = dataGridItems.FirstOrDefault(i => i.PathID == updatedAssetId.pathID);
            if (gridItem != null)
                SetFieldModified(gridItem);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class AssetInfoDataGridItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Container { get; set; }
        public string Type { get; set; }
        public uint TypeID { get; set; }
        public int FileID { get; set; }
        public long PathID { get; set; }
        public int Size { get; set; }
        public string Modified { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        //ultimate lazy
        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
