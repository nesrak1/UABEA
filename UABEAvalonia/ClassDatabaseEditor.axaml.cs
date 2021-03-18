using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace UABEAvalonia
{
    public class ClassDatabaseEditor : Window
    {
        public ClassDatabaseEditor()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
