using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public partial class GoToAssetDialog : Window
    {
        private AssetWorkspace workspace;

        public GoToAssetDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            boxPathId.KeyDown += BoxPathId_KeyDown;
        }

        public GoToAssetDialog(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            int index = 0;
            List<string> loadedFiles = new List<string>();
            foreach (AssetsFileInstance inst in workspace.LoadedFiles)
            {
                loadedFiles.Add($"{index++} - {Path.GetFileName(inst.path)}");
            }
            ddFileId.Items = loadedFiles;
            ddFileId.SelectedIndex = 0;
            boxPathId.Text = "1"; //todo get last id (including new assets)
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ReturnAssetToGoTo();
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(null);
        }

        private void BoxPathId_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                ReturnAssetToGoTo();
            }
        }

        private async void ReturnAssetToGoTo()
        {
            int fileId = ddFileId.SelectedIndex; //hopefully in order
            string pathIdText = boxPathId.Text;

            if (fileId < 0)
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "File was invalid.");
                return;
            }

            if (!long.TryParse(pathIdText, out long pathId))
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "Path ID was invalid.");
                return;
            }

            AssetPPtr pptr = new AssetPPtr(fileId, pathId);

            Close(pptr);
        }
    }
}
