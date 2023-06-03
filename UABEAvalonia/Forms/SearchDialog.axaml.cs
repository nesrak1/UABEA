using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public partial class SearchDialog : Window
    {
        public SearchDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            boxName.KeyDown += BoxName_KeyDown;
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ReturnAssetToSearch();
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(new SearchDialogResult(false));
        }

        private void BoxName_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                ReturnAssetToSearch();
            }
        }

        private void ReturnAssetToSearch()
        {
            if (boxName.Text != null && boxName.Text != string.Empty)
                Close(new SearchDialogResult(true, boxName.Text, rdoSearchDown.IsChecked ?? false, chkCaseSensitive.IsChecked ?? false));
            else
                Close(new SearchDialogResult(false));
        }
    }
    public class SearchDialogResult
    {
        public bool ok;
        public string text;
        public bool isDown;
        public bool caseSensitive;
        public SearchDialogResult(bool ok)
        {
            this.ok = ok;
            this.text = "";
            this.isDown = false;
        }
        public SearchDialogResult(bool ok, string text, bool isDown, bool caseSensitive)
        {
            this.ok = ok;
            this.text = text;
            this.isDown = isDown;
            this.caseSensitive = caseSensitive;
        }
    }
}
