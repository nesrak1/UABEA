using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public class About : Window
    {
        private Button btnOk;
        public About()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated items
            btnOk = this.FindControl<Button>("btnOk");
            //generated events
            btnOk.Click += BtnOk_Click;
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}