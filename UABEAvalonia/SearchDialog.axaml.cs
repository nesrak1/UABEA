using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public partial class SearchDialog : Window
    {
        //controls
        private TextBox boxName;
        private RadioButton rdoSearchUp;
        private RadioButton rdoSearchDown;
        private CheckBox chkCaseSensitive;
        private Button btnOk;
        private Button btnCancel;

        public SearchDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            boxName = this.FindControl<TextBox>("boxName");
            rdoSearchUp = this.FindControl<RadioButton>("rdoSearchUp");
            rdoSearchDown = this.FindControl<RadioButton>("rdoSearchDown");
            chkCaseSensitive = this.FindControl<CheckBox>("chkCaseSensitive");
            btnOk = this.FindControl<Button>("btnOk");
            btnCancel = this.FindControl<Button>("btnCancel");
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (boxName.Text != null && boxName.Text != string.Empty)
                Close(new SearchDialogResult(true, boxName.Text, rdoSearchDown.IsChecked ?? false, chkCaseSensitive.IsChecked ?? false));
            else
                Close(new SearchDialogResult(false));
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(new SearchDialogResult(false));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
