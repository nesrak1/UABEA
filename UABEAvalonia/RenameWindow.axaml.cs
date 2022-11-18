using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public partial class RenameWindow : Window
    {
        //controls
        private Button btnOk;
        private Button btnCancel;
        private TextBox boxOrig;
        private TextBox boxNew;

        public RenameWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            btnOk = this.FindControl<Button>("btnOk")!;
            btnCancel = this.FindControl<Button>("btnCancel")!;
            boxOrig = this.FindControl<TextBox>("boxOrig")!;
            boxNew = this.FindControl<TextBox>("boxNew")!;
            //generated events
            btnOk.Click += BtnYes_Click;
            btnCancel.Click += BtnNo_Click;
        }
        
        public RenameWindow(string name) : this()
        {
            boxOrig.Text = name;
            boxNew.Text = name;
        }

        private void BtnYes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string returnText = boxNew.Text ?? string.Empty; // thanks avalonia
            Close(returnText);
        }

        private void BtnNo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(string.Empty);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
