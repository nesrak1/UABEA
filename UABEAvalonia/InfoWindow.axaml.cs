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

namespace UABEAvalonia
{
    public class InfoWindow : Window
    {
        private AssetsManager am;
        private AssetsFileInstance assetsFile;
        private bool fromBundle;
        //controls
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
        public MenuItem menuSave;
        public MenuItem menuClose;

        public string AssetsFileName { get; private set; }
        public byte[] FinalAssetData { get; private set; }

        private ObservableCollection<AssetInfoDataGridItem> dataGridItems;
        
        private Dictionary<long, AssetsReplacer> newAssets;
        private Dictionary<long, MemoryStream> newAssetDatas; //for preview in uabe
        private bool modified;

        //for preview
        public InfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
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
            menuSave = this.FindControl<MenuItem>("menuSave");
            menuClose = this.FindControl<MenuItem>("menuClose");
            //generated events
            btnViewData.Click += BtnViewData_Click;
            btnExportRaw.Click += BtnExportRaw_Click;
            btnExportDump.Click += BtnExportDump_Click;
            btnImportRaw.Click += BtnImportRaw_Click;
            btnImportDump.Click += BtnImportDump_Click;
            dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            menuSave.Click += MenuSave_Click;
            menuClose.Click += MenuClose_Click;
        }

        public InfoWindow(AssetsManager assetsManager, AssetsFileInstance assetsFile, string name, bool fromBundle) : this()
        {
            this.am = assetsManager;
            this.assetsFile = assetsFile;
            this.fromBundle = fromBundle;
            AssetsFileName = name;

            MakeDataGridItems();
            dataGrid.Items = dataGridItems;

            newAssets = new Dictionary<long, AssetsReplacer>();
            newAssetDatas = new Dictionary<long, MemoryStream>();
            modified = false;

            this.DataContext = this;
        }

        private async void BtnViewData_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await FailIfNothingSelected())
                return;

            AssetInfoDataGridItem gridItem = GetGridItem();
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
                new FileDialogFilter() { Name = "UABE xml dump", Extensions = new List<string>() { "xml" } },
                new FileDialogFilter() { Name = "UABE json dump", Extensions = new List<string>() { "json" } }
            };

            string file = await sfd.ShowAsync(this);

            if (file != null && file != string.Empty)
            {
                AssetImportExport dumper = new AssetImportExport();
                if (file.EndsWith(".json"))
                {
                    await MessageBoxUtil.ShowDialog(this, "Not implemented", "There's no json dump support yet, sorry. Exporting as .txt anyway.");
                    file = file.Substring(0, file.Length - 5) + ".txt";
                    using (FileStream fs = File.OpenWrite(file))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        dumper.DumpTextAsset(sw, GetSelectedField());
                    }
                }
                else if (file.EndsWith(".xml"))
                {
                   dumper.DumpXmlAsset(file, GetSelectedField());
                }
                else {
                    using (FileStream fs = File.OpenWrite(file))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        dumper.DumpTextAsset(sw, GetSelectedField());
                    }
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
                    newAssets[selectedId] = replacer;
                    newAssetDatas[selectedId] = new MemoryStream(bytes);

                    SetSelectedFieldModified();
                    modified = true;
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
                    newAssets[selectedId] = replacer;
                    newAssetDatas[selectedId] = new MemoryStream(bytes);

                    SetSelectedFieldModified();
                    modified = true;
                }
            }
        }

        private async void MenuSave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveFile();
            modified = false;
        }

        private async void MenuClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (modified)
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
            List<AssetsReplacer> newAssetsList = newAssets.Values.ToList();

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

            bool usingTypeTree = assetsFile.file.typeTree.hasTypeTree;
            foreach (AssetFileInfoEx info in assetsFile.table.assetFileInfo)
            {
                string name;
                string container;
                string type;
                int fileId;
                long pathId;
                int size;
                string modified;

                ClassDatabaseType cldbType = AssetHelper.FindAssetClassByID(am.classFile, info.curFileType);
                name = AssetHelper.GetAssetNameFast(assetsFile.file, am.classFile, info); //handles both cldb and typetree
                container = string.Empty;
                fileId = 0;
                pathId = info.index;
                size = (int)info.curFileSize;
                modified = "";

                if (usingTypeTree)
                {
                    Type_0D ttType = assetsFile.file.typeTree.unity5Types[info.curFileTypeOrIndex];
                    if (ttType.typeFieldsEx.Length != 0)
                    {
                        type = ttType.typeFieldsEx[0].GetTypeString(ttType.stringTable);
                    }
                    else
                    {
                        if (cldbType != null)
                            type = cldbType.name.GetString(am.classFile);
                        else
                            type = $"0x{info.curFileType:X8}";
                    }
                }
                else
                {
                    if (cldbType != null)
                        type = cldbType.name.GetString(am.classFile);
                    else
                        type = $"0x{info.curFileType:X8}";
                }

                if (info.curFileType == 0x01)
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
                    TypeID = info.curFileType,
                    FileID = fileId,
                    PathID = pathId,
                    Size = size,
                    Modified = modified
                };

                dataGridItems.Add(item);
            }
            return dataGridItems;
        }

        public void UpdateGridItems()
        {
            //I'm guessing there's some bug in avalonia
            //that's keeping existing items from drawing.
            //no matter what I try, whether Items binding
            //or invalidate methods or whatever, the item
            //never updates. so for now, just resize the
            //window a little bit, then scroll it off the
            //screen and you should see the item updated
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

        private AssetInfoDataGridItem GetGridItem()
        {
            return (AssetInfoDataGridItem)dataGrid.SelectedItem;
        }

        private AssetFileInfoEx GetSelectedInfo()
        {
            AssetInfoDataGridItem gridItem = GetGridItem();
            return am.GetExtAsset(assetsFile, gridItem.FileID, gridItem.PathID).info;
        }

        private AssetTypeValueField GetSelectedField()
        {
            AssetInfoDataGridItem gridItem = GetGridItem();
            return am.GetExtAsset(assetsFile, gridItem.FileID, gridItem.PathID).instance.GetBaseField();
        }

        private AssetTypeValueField GetSelectedFieldReplaced()
        {
            AssetInfoDataGridItem gridItem = GetGridItem();
            long id = gridItem.PathID;
            if (newAssetDatas.ContainsKey(id))
            {
                return am.GetExtAssetNewData(assetsFile, gridItem.FileID, gridItem.PathID, newAssetDatas[id]).instance.GetBaseField();
            }
            else
            {
                return am.GetExtAsset(assetsFile, gridItem.FileID, gridItem.PathID).instance.GetBaseField();
            }
        }

        private void SetSelectedFieldModified()
        {
            AssetInfoDataGridItem gridItem = GetGridItem();
            gridItem.Modified = "*";
            //because avalonia won't let us update manually .-.
            dataGridItems.Add(new AssetInfoDataGridItem());
            dataGridItems.RemoveAt(dataGridItems.Count - 1);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class AssetInfoDataGridItem
    {
        public string Name { get; set; }
        public string Container { get; set; }
        public string Type { get; set; }
        public uint TypeID { get; set; }
        public int FileID { get; set; }
        public long PathID { get; set; }
        public int Size { get; set; }
        public string Modified { get; set; }
    }
}
