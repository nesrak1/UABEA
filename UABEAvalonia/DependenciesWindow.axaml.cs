using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using UABEAvalonia.Plugins;

namespace UABEAvalonia
{
    public class DependenciesWindow : Window
    {
        //controls
        private ListBox boxDependenciesList;
        private ComboBox cbxFiles;
        private Button btnOk;
		
        private AssetWorkspace workspace;

        public DependenciesWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            boxDependenciesList = this.FindControl<ListBox>("boxDependenciesList");
            cbxFiles = this.FindControl<ComboBox>("cbxFiles");
            btnOk = this.FindControl<Button>("btnOk");
            //generated events
            btnOk.Click += BtnOk_Click;
        }

        public DependenciesWindow(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            List<DependencyComboBoxItem> comboBoxFiles = new List<DependencyComboBoxItem>();
            comboBoxFiles.Add(new DependencyComboBoxItem("All files", null));

            for (int i = 0; i < workspace.LoadedFiles.Count; i++)
            {
                AssetsFileInstance file = workspace.LoadedFiles[i];
                comboBoxFiles.Add(new DependencyComboBoxItem(file.name, file));
            }

            cbxFiles.Items = comboBoxFiles;
			
			List<string> dependencyNames = new List<string>();
            for (int i = 0; i < workspace.LoadedFiles.Count; i++)
            {
                dependencyNames.Add($"{i} - {workspace.LoadedFiles[i].name}");
            }
			
            boxDependenciesList.Items = dependencyNames;
        }

        private async void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(true);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private class DependencyComboBoxItem
        {
            public string text;
            public AssetsFileInstance file;

            public DependencyComboBoxItem(string text, AssetsFileInstance? file)
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
