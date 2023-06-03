using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public partial class RenameWindow : Window
    {
        public RenameWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
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
    }
}
