using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnOk_Click;
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}