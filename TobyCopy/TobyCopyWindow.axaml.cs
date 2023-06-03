using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.IO;
using UABEAvalonia;

namespace TobyCopy
{
    public partial class TobyCopyWindow : Window
    {
        private AssetsManager manager;
        private BundleFileInstance bun;

        public TobyCopyWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnPickFile.Click += BtnPickFile_Click;
            btnLoadBundle.Click += BtnLoadBundle_Click;

            manager = new AssetsManager();
        }

        private void UpdateTreeView()
        {
            int type = boxFilterType.SelectedIndex;
            treeBundleItem.Items.Clear();
            if (type == 0) // container (tree)
            {

            }
            else if (type == 1) // container (flat)
            {

            }
            else if (type == 2) // asset type
            {

            }
            else if (type == 3) // asset name
            {

            }
            else if (type == 4) // path id
            {

            }
        }

        private async void BtnPickFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open bundle file",
                FileTypeFilter = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("All types (*.*)") { Patterns = new List<string>() { "*.*" } }
                }
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length > 0)
            {
                boxSelectedFilePath.Text = selectedFilePaths[0];
            }
        }

        private async void BtnLoadBundle_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            manager.UnloadAll();

            string path = boxSelectedFilePath.Text;
            if (!File.Exists(path))
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "File does not exist.");
                return;
            }

            DetectedFileType fileType = FileTypeDetector.DetectFileType(path);
            if (fileType != DetectedFileType.BundleFile)
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "Only bundles are supported at the moment.");
                return;
            }

            try
            {
                bun = manager.LoadBundleFile(path);
            }
            catch (System.Exception ex)
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "Failed to load bundle file: " + ex.Message);
                return;
            }
        }
    }
}
