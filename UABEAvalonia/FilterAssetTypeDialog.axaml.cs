using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace UABEAvalonia
{
    public partial class FilterAssetTypeDialog : Window
    {
        private bool reallyClosing;

        public FilterAssetTypeDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            Closing += FilterAssetTypeDialog_Closing;
            selectBtn.Click += SelectBtn_Click;
            deselectBtn.Click += DeselectBtn_Click;
        }

        public FilterAssetTypeDialog(HashSet<AssetClassID> filteredOutTypeIds) : this()
        {
            List<FilterAssetListItem> listItems = new List<FilterAssetListItem>();

            AssetClassID[] ids = Enum.GetValues<AssetClassID>();
            foreach (AssetClassID id in ids)
            {
                listItems.Add(new FilterAssetListItem(id, !filteredOutTypeIds.Contains(id)));
            }

            listItems.Sort((x, y) => x.Type.ToString().CompareTo(y.Type.ToString()));
            listBox.Items = listItems;
        }

        private void FilterAssetTypeDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (reallyClosing)
                return;

            reallyClosing = true;

            HashSet<AssetClassID> filteredOutTypeIds = new HashSet<AssetClassID>();
            foreach (FilterAssetListItem item in listBox.Items)
            {
                if (!item.Enabled)
                {
                    filteredOutTypeIds.Add(item.Type);
                }
            }
            Close(filteredOutTypeIds);
        }

        private void SelectBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (FilterAssetListItem item in listBox.Items)
            {
                item.Enabled = true;
                item.Update("Enabled");
            }
        }

        private void DeselectBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (FilterAssetListItem item in listBox.Items)
            {
                item.Enabled = false;
                item.Update("Enabled");
            }
        }
    }

    public class FilterAssetListItem : INotifyPropertyChanged
    {
        public bool Enabled { get; set; }
        public AssetClassID Type { get; set; }

        public FilterAssetListItem(AssetClassID type, bool enabled)
        {
            Type = type;
            Enabled = enabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Update(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
