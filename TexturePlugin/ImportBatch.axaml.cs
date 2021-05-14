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
using UABEAvalonia;

namespace TexturePlugin
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

        public ImportBatch(AssetWorkspace workspace, List<AssetExternal> selection, string directory) : this()
        {
            this.workspace = workspace;
            this.directory = directory;

            List<string> filesInDir = Directory.GetFiles(directory, "*.png").ToList();
            List<BatchImportDataGridItem> gridItems = new List<BatchImportDataGridItem>();
            foreach (AssetExternal ext in selection)
            {
                AssetTypeValueField texBaseField = ext.instance.GetBaseField();
                BatchImportDataGridItem gridItem = new BatchImportDataGridItem()
                {
                    Description = texBaseField.Get("m_Name").GetValue().AsString(),
                    File = Path.GetFileName(ext.file.path),
                    PathID = ext.info.index,
                    ext = ext
                };
                string endWith = gridItem.GetMatchName(".png");
                List<string> matchingFiles = filesInDir.Where(f => f.EndsWith(endWith)).Select(f => Path.GetFileName(f)).ToList();
                gridItem.matchingFiles = matchingFiles;
                gridItem.selectedIndex = matchingFiles.Count > 0 ? 0 : -1;
                gridItems.Add(gridItem);
            }
            dataGrid.Items = gridItems;
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem != null && dataGrid.SelectedItem is BatchImportDataGridItem gridItem)
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

        private void BoxMatchingFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem != null && dataGrid.SelectedItem is BatchImportDataGridItem gridItem)
            {
                if (boxMatchingFiles.SelectedIndex != -1 && !ignoreListEvents)
                    gridItem.selectedIndex = boxMatchingFiles.SelectedIndex;
            }
        }

        private void BtnOk_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (BatchImportDataGridItem gridItem in dataGrid.Items)
            {
                if (gridItem.matchingFiles.Count <= 0)
                    continue;

                string selectedFile = gridItem.matchingFiles[gridItem.selectedIndex];
                string selectedFilePath = Path.Combine(directory, selectedFile);

                AssetTypeValueField baseField = gridItem.ext.instance.GetBaseField();
                TextureFormat fmt = (TextureFormat)baseField.Get("m_TextureFormat").GetValue().AsInt();

                byte[] encImageBytes = TextureImportExport.ImportPng(selectedFilePath, fmt, out int width, out int height);

                AssetTypeValueField m_StreamData = baseField.Get("m_StreamData");
                m_StreamData.Get("offset").GetValue().Set(0);
                m_StreamData.Get("size").GetValue().Set(0);
                m_StreamData.Get("path").GetValue().Set("");

                baseField.Get("m_TextureFormat").GetValue().Set((int)fmt);

                baseField.Get("m_Width").GetValue().Set(width);
                baseField.Get("m_Height").GetValue().Set(height);

                AssetTypeValueField image_data = baseField.Get("image data");
                image_data.GetValue().type = EnumValueTypes.ByteArray;
                image_data.templateField.valueType = EnumValueTypes.ByteArray;
                AssetTypeByteArray byteArray = new AssetTypeByteArray()
                {
                    size = (uint)encImageBytes.Length,
                    data = encImageBytes
                };
                image_data.GetValue().Set(byteArray);
            }

            Close(true);
        }

        private void BtnCancel_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class BatchImportDataGridItem : INotifyPropertyChanged
    {
        public string Description { get; set; }
        public string File { get; set; }
        public long PathID { get; set; }

        public AssetExternal ext;
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
