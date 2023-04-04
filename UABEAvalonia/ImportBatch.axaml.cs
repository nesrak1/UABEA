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
        private AssetWorkspace workspace;
        private string directory;
        private bool ignoreListEvents;

        public ImportBatch()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            boxMatchingFiles.SelectionChanged += BoxMatchingFiles_SelectionChanged;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;

            ignoreListEvents = false;
        }

        public ImportBatch(AssetWorkspace workspace, List<AssetContainer> selection, string directory, List<string> extensions) : this()
        {
            this.workspace = workspace;
            this.directory = directory;

            bool anyExtension = extensions.Contains("*");

            List<string> filesInDir;
            if (!anyExtension)
                filesInDir = Extensions.GetFilesInDirectory(directory, extensions);
            else
                filesInDir = Directory.GetFiles(directory).ToList();

            List<ImportBatchDataGridItem> gridItems = new List<ImportBatchDataGridItem>();
            foreach (AssetContainer cont in selection)
            {
                Extensions.GetUABENameFast(workspace, cont, true, out string assetName, out string _);

                ImportBatchDataGridItem gridItem = new ImportBatchDataGridItem()
                {
                    importInfo = new ImportBatchInfo()
                    {
                        assetName = assetName,
                        assetFile = Path.GetFileName(cont.FileInstance.path),
                        pathId = cont.PathId,
                        cont = cont
                    }
                };

                List<string> matchingFiles;
                
                if (!anyExtension)
                    matchingFiles = filesInDir
                        .Where(f => extensions.Any(x => f.EndsWith(gridItem.GetMatchName(x))))
                        .Select(f => Path.GetFileName(f)).ToList();
                else
                    matchingFiles = filesInDir
                        .Where(f => Extensions.GetFilePathWithoutExtension(f).EndsWith(gridItem.GetMatchName("*")))
                        .Select(f => Path.GetFileName(f)).ToList();

                gridItem.matchingFiles = matchingFiles;
                gridItem.selectedIndex = matchingFiles.Count > 0 ? 0 : -1;
                if (gridItem.matchingFiles.Count > 0)
                {
                    gridItems.Add(gridItem);
                }
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
    }

    public class ImportBatchInfo
    {
        public AssetContainer cont;
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
            if (ext != "*")
                return $"-{File}-{PathID}.{ext}";
            else
                return $"-{File}-{PathID}";
        }
        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
