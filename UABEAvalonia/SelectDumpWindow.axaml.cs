using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;

namespace UABEAvalonia
{
    public partial class SelectDumpWindow : Window
    {
        public static List<string> ALL_EXTENSIONS = new List<string>() { "json", "txt" };

        public SelectDumpWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public SelectDumpWindow(bool hideAnyOption) : this()
        {
            anyItem.IsVisible = !hideAnyOption;
        }

        private void BtnOk_Click(object? sender, RoutedEventArgs e)
        {
            ComboBoxItem? selectedItem = (ComboBoxItem?)comboBox.SelectedItem;
            if (selectedItem == null)
            {
                Close(null);
                return;
            }

            string? shortName = (string?)selectedItem.Tag;
            if (shortName == null)
            {
                Close(null);
                return;
            }

            Close(shortName);
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
