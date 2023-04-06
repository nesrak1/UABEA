using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace UABEAvalonia
{
    public partial class ExportBatchChooseType : Window
    {
        public ExportBatchChooseType()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(((ComboBoxItem)comboFileType.SelectedItem).Content.ToString());
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
