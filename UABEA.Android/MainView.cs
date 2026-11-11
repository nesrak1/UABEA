using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace UABEA.Mobile;

public sealed class MainView : UserControl
{
    private readonly TextBlock _status;
    private readonly StackPanel _items;
    private AssetsManager? _assetsManager;

    public MainView()
    {
        var root = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(18),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var title = new TextBlock
        {
            Text = "UABEA Mobile",
            FontSize = 27,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#56C3F2")
        };

        var subtitle = new TextBlock
        {
            Text = "Unity Assets & AssetBundle Explorer",
            FontSize = 13,
            Foreground = Brush.Parse("#AAB8C2")
        };

        var openButton = new Button
        {
            Content = "Open Unity file",
            MinHeight = 52,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush.Parse("#56C3F2"),
            Foreground = Brush.Parse("#101820"),
            FontWeight = FontWeight.Bold
        };
        openButton.Click += OpenFile_Click;

        _status = new TextBlock
        {
            Text = "Select an AssetBundle or serialized assets file to begin.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#E5EDF2")
        };

        _items = new StackPanel { Spacing = 7 };

        var listFrame = new Border
        {
            Background = Brush.Parse("#17232D"),
            BorderBrush = Brush.Parse("#2C3E4B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                Content = _items,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        root.Children.Add(title);
        root.Children.Add(subtitle);
        root.Children.Add(openButton);
        root.Children.Add(_status);
        root.Children.Add(listFrame);

        Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Background = Brush.Parse("#101820");
    }

    private async void OpenFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return;

            var selected = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Open Unity asset or bundle",
                    AllowMultiple = false
                });

            var file = selected.FirstOrDefault();
            if (file is null)
                return;

            _status.Text = $"Reading {file.Name}...";
            _items.Children.Clear();

            var localPath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}_{Path.GetFileName(file.Name)}");

            await using (var source = await file.OpenReadAsync())
            await using (var destination = File.Create(localPath))
            {
                await source.CopyToAsync(destination);
            }

            var manager = new AssetsManager();
            var signature = new byte[7];

            await using (var headerStream = File.OpenRead(localPath))
            {
                _ = await headerStream.ReadAsync(signature.AsMemory(0, signature.Length));
            }

            var header = Encoding.ASCII.GetString(signature);
            if (header is "UnityFS" or "UnityRaw" or "UnityW")
            {
                var bundle = manager.LoadBundleFile(localPath, false);
                _assetsManager = manager;

                var entries = bundle.file.BlockAndDirInfo.DirectoryInfos;
                _status.Text = $"Bundle opened: {file.Name}\nEntries: {entries.Count}";

                foreach (var entry in entries)
                {
                    _items.Children.Add(CreateEntry(
                        entry.Name,
                        $"{entry.DecompressedSize:N0} bytes"));
                }
            }
            else
            {
                var assetsFile = manager.LoadAssetsFile(localPath, false);
                _assetsManager = manager;

                var assets = assetsFile.file.AssetInfos;
                _status.Text = $"Serialized file opened: {file.Name}\nAssets: {assets.Count}";

                foreach (var asset in assets.Take(500))
                {
                    _items.Children.Add(CreateEntry(
                        $"Path ID {asset.PathId}",
                        $"Type ID: {asset.GetTypeId(assetsFile.file)}"));
                }

                if (assets.Count > 500)
                {
                    _items.Children.Add(new TextBlock
                    {
                        Text = $"Showing first 500 of {assets.Count} assets.",
                        Foreground = Brush.Parse("#AAB8C2"),
                        Margin = new Thickness(4, 10)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open this file:\n{ex.Message}";
            _items.Children.Clear();
            _assetsManager = null;
        }
    }

    private static Control CreateEntry(string title, string detail)
    {
        return new Border
        {
            Background = Brush.Parse("#202F3A"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = detail,
                        FontSize = 12,
                        Foreground = Brush.Parse("#AAB8C2")
                    }
                }
            }
        };
    }
}
