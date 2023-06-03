using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Core;
using Avalonia.Interactivity;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UABEAvalonia
{
    public partial class AssetsFileInfoWindow : Window
    {
        private AssetWorkspace workspace;
        private ClassDatabaseFile cldb;
        private AssetsManager am;
        private AssetsFileInstance activeFile;
        private bool allMode;

        public AssetsFileInfoWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            SetupTypeTreePageEvents();
            SetupDepsPageEvents();
        }

        public AssetsFileInfoWindow(AssetWorkspace workspace, AssetsFileInfoWindowStartTab startTab) : this()
        {
            this.workspace = workspace;
            cldb = workspace.am.ClassDatabase;
            am = workspace.am;
            activeFile = workspace.LoadedFiles[0];
            allMode = true;

            tabControl.SelectedIndex = (int)startTab;

            SetupFilesComboBox();

            FillInfo();
        }

        private void SetupFilesComboBox()
        {
            dependencyMap = new Dictionary<AssetsFileInstance, List<AssetsFileExternal>>();
            dependenciesModified = new HashSet<AssetsFileInstance>();

            List<DependencyComboBoxItem> comboBoxFiles = new List<DependencyComboBoxItem>();
            DependencyComboBoxItem allFilesItem = new DependencyComboBoxItem("First or all files", null);
            comboBoxFiles.Add(allFilesItem);

            for (int i = 0; i < workspace.LoadedFiles.Count; i++)
            {
                AssetsFileInstance file = workspace.LoadedFiles[i];
                comboBoxFiles.Add(new DependencyComboBoxItem(file.name, file));

                List<AssetsFileExternal> dependencies = new List<AssetsFileExternal>();
                for (int j = 0; j < file.file.Metadata.Externals.Count; j++)
                {
                    dependencies.Add(file.file.Metadata.Externals[j]);
                }

                dependencyMap[file] = dependencies;
            }

            cbxFiles.Items = comboBoxFiles;
            cbxFiles.SelectedItem = allFilesItem;
        }

        private void FillInfo()
        {
            FillGeneralInfo();
            FillTypeTreeInfo();
            FillDependenciesInfo();
            FillScriptInfo();
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            DependencyComboBoxItem? selectedItem = (DependencyComboBoxItem?)cbxFiles.SelectedItem;
            AssetsFileInstance? selectedFile = selectedItem?.file;
            if (selectedFile == null)
            {
                activeFile = workspace.LoadedFiles[0];
                allMode = true;
            }
            else
            {
                activeFile = selectedFile;
                allMode = false;
            }

            FillInfo();
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(new Dictionary<AssetsFileInstance, AssetsFileChangeTypes>());
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            Dictionary<AssetsFileInstance, AssetsFileChangeTypes> changedFiles = new();
            
            HandleDepsSaving(changedFiles);

            Close(changedFiles);
        }
    }

    public enum AssetsFileInfoWindowStartTab
    {
        General,
        TypeTree,
        Dependencies,
        Script
    }
}
