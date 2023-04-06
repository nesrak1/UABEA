using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public partial class ImportSerializedDialog : Window
    {
        public ImportSerializedDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnYes.Click += BtnYes_Click;
            btnNo.Click += BtnNo_Click;
        }

        private void BtnYes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(true);
        }

        private void BtnNo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
