using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace UABEAvalonia
{
    public partial class ImportBatch : Window
    {
        //controls
        private DataGrid dataGrid;
        private ListBox boxMatchingFiles;
        private Button btnOk;
        private Button btnCancel;

        private AssetWorkspace workspace;
        private string directory;
        private bool ignoreListEvents;

        public ImportBatch()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            dataGrid = this.FindControl<DataGrid>("dataGrid");
            boxMatchingFiles = this.FindControl<ListBox>("boxMatchingFiles");
            btnOk = this.FindControl<Button>("btnOk");
            btnCancel = this.FindControl<Button>("btnCancel");
            //generated events
            dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            boxMatchingFiles.SelectionChanged += BoxMatchingFiles_SelectionChanged;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;

            ignoreListEvents = false;
        }

        public ImportBatch(AssetWorkspace workspace, List<AssetExternal> selection, string directory, string extension) : this()
        {
            this.workspace = workspace;
            this.directory = directory;

            List<string> filesInDir = Directory.GetFiles(directory, "*" + extension).ToList();
            List<ImportBatchDataGridItem> gridItems = new List<ImportBatchDataGridItem>();
            foreach (AssetExternal ext in selection)
            {
                AssetFileInfoEx info = ext.info;

                string assetName = AssetHelper.GetAssetNameFast(ext.file.file, workspace.am.classFile, ext.info);
                if (info.curFileType == 0x01)
                {
                    assetName = $"GameObject {assetName}";
                }
                else if (info.curFileType == 0x72)
                {
                    if (assetName == string.Empty)
                        assetName = $"MonoBehaviour";
                    else
                        assetName = $"MonoBehaviour {assetName}";
                }
                if (assetName == string.Empty)
                {
                    assetName = "Unnamed asset";
                }

                ImportBatchDataGridItem gridItem = new ImportBatchDataGridItem()
                {
                    importInfo = new ImportBatchInfo()
                    {
                        assetName = assetName,
                        assetFile = Path.GetFileName(ext.file.path),
                        pathId = ext.info.index,
                        ext = ext
                    }
                };
                string endWith = gridItem.GetMatchName(extension);
                List<string> matchingFiles = filesInDir.Where(f => f.EndsWith(endWith)).Select(f => Path.GetFileName(f)).ToList();
                gridItem.matchingFiles = matchingFiles;
                gridItem.selectedIndex = matchingFiles.Count > 0 ? 0 : -1;
                gridItems.Add(gridItem);
            }
            dataGrid.Items = gridItems;
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem != null && dataGrid.SelectedItem is ImportBatchDataGridItem gridItem)
            {
                boxMatchingFiles.Items = gridItem.matchingFiles;
                if (gridItem.selectedIndex != -1)
                {
                    //there's gotta be a better way to do this .-. oh well
                    ignoreListEvents = true;
                    boxMatchingFiles.SelectedIndex = gridItem.selectedIndex;
                    ignoreListEvents = false;
                }
            }
        }

        private void BoxMatchingFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem != null && dataGrid.SelectedItem is ImportBatchDataGridItem gridItem)
            {
                if (boxMatchingFiles.SelectedIndex != -1 && !ignoreListEvents)
                    gridItem.selectedIndex = boxMatchingFiles.SelectedIndex;
            }
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            List<ImportBatchInfo> importInfos = new List<ImportBatchInfo>();
            foreach (ImportBatchDataGridItem gridItem in dataGrid.Items)
            {
                if (gridItem.selectedIndex != -1)
                {
                    ImportBatchInfo importInfo = gridItem.importInfo;
                    importInfo.importFile = Path.Combine(directory, gridItem.matchingFiles[gridItem.selectedIndex]);
                    importInfos.Add(importInfo);
                }
            }

            Close(importInfos);
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(null);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class ImportBatchInfo
    {
        public AssetExternal ext;
        public string importFile;
        public string assetName;
        public string assetFile;
        public long pathId;
    }

    public class ImportBatchDataGridItem : INotifyPropertyChanged
    {
        public ImportBatchInfo importInfo;

        public string Description { get => importInfo.assetName; }
        public string File { get => importInfo.assetFile; }
        public long PathID { get => importInfo.pathId; }

        public List<string> matchingFiles;
        public int selectedIndex;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string GetMatchName(string ext)
        {
            return $"-{File}-{PathID}{ext}";
        }
        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
