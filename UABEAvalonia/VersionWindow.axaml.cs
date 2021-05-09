using AssetsTools.NET;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Linq;

namespace UABEAvalonia
{
    public partial class VersionWindow : Window
    {
        //controls
        private TextBlock infoLbl;
        private ListBox boxVersionList;
        private Button btnOk;
        private Button btnCancel;

        List<ClassFileInfo> classTypes;

        public VersionWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            infoLbl = this.FindControl<TextBlock>("infoLbl");
            boxVersionList = this.FindControl<ListBox>("boxVersionList");
            btnOk = this.FindControl<Button>("btnOk");
            btnCancel = this.FindControl<Button>("btnCancel");
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public VersionWindow(string uVer, ClassDatabasePackage cpkg) : this()
        {
            classTypes = new List<ClassFileInfo>();
            foreach (ClassDatabaseFile cldb in cpkg.files)
            {
                classTypes.Add(new ClassFileInfo(cldb));
            }
            boxVersionList.Items = classTypes;

            infoLbl.Text = $"There is no type database for {uVer}.\nPlease choose the closest version.";
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var classTypeInf = boxVersionList.SelectedItem as ClassFileInfo;

            if (classTypeInf == null)
            {
                Close(null);
                return;
            }

            Close(classTypeInf.cldb);
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

    public class ClassFileInfo
    {
        public ClassDatabaseFile cldb;
        public string name;
        public ClassFileInfo(ClassDatabaseFile cldb)
        {
            this.cldb = cldb;

            string[] unityVersions = cldb.header.unityVersions;
            string? nonWildcardVersion = unityVersions.FirstOrDefault(v => !v.EndsWith(".*"));
            if (nonWildcardVersion != null)
                name = nonWildcardVersion;
            else
                name = unityVersions[0];
        }

        public override string ToString()
        {
            return name;
        }
    }
}
