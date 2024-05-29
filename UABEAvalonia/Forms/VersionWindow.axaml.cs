using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;

namespace UABEAvalonia
{
    public partial class VersionWindow : Window
    {
        public VersionWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnYes_Click;
            btnCancel.Click += BtnNo_Click;
        }

        public VersionWindow(string ver) : this()
        {
            boxVer.Text = ver;
        }

        private async void BtnYes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string returnText = boxVer.Text ?? string.Empty;
            try
            {
                _ = new UnityVersion(returnText);
            }
            catch
            {
                await MessageBoxUtil.ShowDialog(this, "Error", "Invalid version string. Example: 2019.4.1f1");
                return;
            }
            Close(returnText);
        }

        private void BtnNo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(string.Empty);
        }
    }
}
