using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;

namespace UABEAvalonia
{
    public partial class ScriptsWindow : Window
    {
        private AssetWorkspace workspace;

        public ScriptsWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnCancel.Click += BtnCancel_Click;
            btnOk.Click += BtnOk_Click;
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;
        }

        public ScriptsWindow(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            List<ScriptComboBoxItem> comboBoxFiles = new List<ScriptComboBoxItem>();

            for (int i = 0; i < workspace.LoadedFiles.Count; i++)
            {
                AssetsFileInstance file = workspace.LoadedFiles[i];
                comboBoxFiles.Add(new ScriptComboBoxItem(file.name, file));
            }

            cbxFiles.Items = comboBoxFiles;
            cbxFiles.SelectedItem = comboBoxFiles[0];

            UpdateListBox();
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(new HashSet<AssetsFileInstance>());
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            Close(new HashSet<AssetsFileInstance>());
        }

        private void UpdateListBox()
        {
            if (cbxFiles.SelectedItem == null)
                return;

            AssetsFileInstance? selectedFile = GetSelectedAssetsFile();
            if (selectedFile == null)
                return;

            List<string> items = new List<string>();
            List<AssetPPtr> scriptTypes = selectedFile.file.Metadata.ScriptTypes;
            for (int i = 0; i < scriptTypes.Count; i++)
            {
                AssetPPtr pptr = scriptTypes[i];
                AssetTypeValueField? scriptBf = workspace.GetBaseField(selectedFile, pptr.FileId, pptr.PathId);
                if (scriptBf == null)
                    continue;

                string nameSpace = scriptBf["m_Namespace"].AsString;
                string className = scriptBf["m_ClassName"].AsString;

                string fullName;
                if (nameSpace != "")
                    fullName = $"{nameSpace}.{className}";
                else
                    fullName = className;

                items.Add($"{i} - {fullName}");
            }

            boxScriptsList.Items = items;
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateListBox();
        }

        private AssetsFileInstance? GetSelectedAssetsFile()
        {
            ScriptComboBoxItem? selectedItem = (ScriptComboBoxItem?)cbxFiles.SelectedItem;
            AssetsFileInstance? selectedFile = selectedItem?.file;

            return selectedFile;
        }

        private class ScriptComboBoxItem
        {
            public string text;
            public AssetsFileInstance? file;

            public ScriptComboBoxItem(string text, AssetsFileInstance? file)
            {
                this.text = text;
                this.file = file;
            }

            public override string ToString()
            {
                return text;
            }
        }
    }
}
