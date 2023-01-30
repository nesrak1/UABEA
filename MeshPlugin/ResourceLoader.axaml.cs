using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MeshPlugin
{
    public partial class ResourceLoader : Window
    {
        private Button btnOk;
        public ResourceLoader()
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
        private async void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
