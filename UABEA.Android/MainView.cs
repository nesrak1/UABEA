using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET;
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
    private readonly TextBox _searchBox;
    private readonly Button _backButton;

    private AssetsManager? _assetsManager;
    private BundleFileInstance? _currentBundle;
    private AssetsFileInstance? _currentAssets;
    private string _currentFileName = "";
    private BrowserPage _currentPage = BrowserPage.Home;

    private enum BrowserPage
    {
        Home,
        Bundle,
        Assets,
        EntryDetails,
        AssetDetails
    }

    public MainView()
    {
        var root = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(16),
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

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };

        var openButton = new Button
        {
            Content = "Open Unity file",
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush.Parse("#56C3F2"),
            Foreground = Brush.Parse("#101820"),
            FontWeight = FontWeight.Bold
        };
        openButton.Click += OpenFile_Click;

        _backButton = new Button
        {
            Content = "Back",
            MinHeight = 48,
            IsVisible = false,
            Background = Brush.Parse("#263946"),
            Foreground = Brushes.White
        };
        _backButton.Click += (_, _) => GoBack();

        Grid.SetColumn(openButton, 0);
        Grid.SetColumn(_backButton, 1);
        toolbar.Children.Add(openButton);
        toolbar.Children.Add(_backButton);

        _searchBox = new TextBox
        {
            Watermark = "Search files or assets...",
            MinHeight = 44,
            Padding = new Thickness(12, 8),
            Background = Brush.Parse("#17232D"),
            Foreground = Brushes.White,
            BorderBrush = Brush.Parse("#2C3E4B"),
            CornerRadius = new CornerRadius(8),
            IsVisible = false
        };
        _searchBox.TextChanged += (_, _) => RefreshCurrentList();

        _status = new TextBlock
        {
            Text = "Open a Unity AssetBundle or serialized assets file.",
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
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                Content = _items,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        root.Children.Add(title);
        root.Children.Add(subtitle);
        root.Children.Add(toolbar);
        root.Children.Add(_searchBox);
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
            _searchBox.Text = "";
            _searchBox.IsVisible = false;

            var localPath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}_{Path.GetFileName(file.Name)}");

            await using (var source = await file.OpenReadAsync())
            await using (var destination = File.Create(localPath))
            {
                await source.CopyToAsync(destination);
            }

            _assetsManager = new AssetsManager();
            _currentBundle = null;
            _currentAssets = null;
            _currentFileName = file.Name;

            var signature = new byte[8];
            await using (var headerStream = File.OpenRead(localPath))
            {
                _ = await headerStream.ReadAsync(signature.AsMemory(0, signature.Length));
            }

            var header = Encoding.ASCII.GetString(signature);
            if (header.StartsWith("UnityFS", StringComparison.Ordinal) ||
                header.StartsWith("UnityRaw", StringComparison.Ordinal) ||
                header.StartsWith("UnityWeb", StringComparison.Ordinal))
            {
                _currentBundle = _assetsManager.LoadBundleFile(localPath, false);
                ShowBundleContents();
            }
            else if (AssetsFile.IsAssetsFile(localPath))
            {
                _currentAssets = _assetsManager.LoadAssetsFile(localPath, false);
                ShowAssetsContents();
            }
            else
            {
                _status.Text = $"Unsupported or unrecognized Unity file: {file.Name}";
                _items.Children.Add(CreateInfoRow(
                    "Unrecognized file",
                    "The file header is not a supported Unity bundle or serialized assets file."));
                _currentPage = BrowserPage.Home;
                UpdateNavigation();
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open this file:\n{ex.Message}";
            _items.Children.Clear();
            _currentBundle = null;
            _currentAssets = null;
            _currentPage = BrowserPage.Home;
            UpdateNavigation();
        }
    }

    private void ShowBundleContents()
    {
        if (_currentBundle is null)
            return;

        _currentPage = BrowserPage.Bundle;
        _searchBox.IsVisible = true;
        _searchBox.Watermark = "Search bundle entries...";
        var entries = _currentBundle.file.BlockAndDirInfo.DirectoryInfos;
        _status.Text = $"Bundle: {_currentFileName}\nEntries: {entries.Count}";
        UpdateNavigation();
        RefreshCurrentList();
    }

    private void ShowAssetsContents()
    {
        if (_currentAssets is null)
            return;

        _currentPage = BrowserPage.Assets;
        _searchBox.IsVisible = true;
        _searchBox.Watermark = "Search by Path ID or Type ID...";
        var assets = _currentAssets.file.AssetInfos;
        _status.Text = $"Assets file: {_currentAssets.name}\nAssets: {assets.Count}";
        UpdateNavigation();
        RefreshCurrentList();
    }

    private void RefreshCurrentList()
    {
        if (_items is null)
            return;

        _items.Children.Clear();
        var query = (_searchBox.Text ?? "").Trim();

        if (_currentPage == BrowserPage.Bundle && _currentBundle is not null)
        {
            var entries = _currentBundle.file.BlockAndDirInfo.DirectoryInfos;
            foreach (var entry in entries)
            {
                if ((entry.Flags & 0x02) != 0)
                    continue;

                if (!Matches(entry.Name, query) &&
                    !Matches(entry.DecompressedSize.ToString(), query))
                    continue;

                var isSerialized = (entry.Flags & 0x04) != 0;
                var kind = isSerialized ? "Serialized assets file" : GetEntryKind(entry.Name);
                _items.Children.Add(CreateActionRow(
                    entry.Name,
                    $"{kind} • {FormatSize(entry.DecompressedSize)}",
                    () => OpenBundleEntry(entry.Name)));
            }

            if (_items.Children.Count == 0)
                AddEmptyMessage("No entries match this search.");
        }
        else if (_currentPage == BrowserPage.Assets && _currentAssets is not null)
        {
            var assets = _currentAssets.file.AssetInfos;
            foreach (var asset in assets)
            {
                var typeId = asset.GetTypeId(_currentAssets.file);
                var pathText = asset.PathId.ToString();
                var typeText = typeId.ToString();

                if (!Matches(pathText, query) && !Matches(typeText, query))
                    continue;

                _items.Children.Add(CreateActionRow(
                    $"Path ID: {asset.PathId}",
                    $"Type ID: {typeId} • {FormatSize(asset.ByteSize)}",
                    () => ShowAssetDetails(asset)));
            }

            if (_items.Children.Count == 0)
                AddEmptyMessage("No assets match this search.");
        }
    }

    private void OpenBundleEntry(string entryName)
    {
        if (_currentBundle is null || _assetsManager is null)
            return;

        try
        {
            var entries = _currentBundle.file.BlockAndDirInfo.DirectoryInfos;
            var index = entries.FindIndex(x => string.Equals(x.Name, entryName, StringComparison.Ordinal));
            if (index < 0)
                return;

            var entry = entries[index];
            if ((entry.Flags & 0x04) != 0)
            {
                _currentAssets = _assetsManager.LoadAssetsFileFromBundle(_currentBundle, index, false);
                ShowAssetsContents();
                return;
            }

            _currentPage = BrowserPage.EntryDetails;
            _searchBox.IsVisible = false;
            _status.Text = $"Bundle entry: {entry.Name}";
            _items.Children.Clear();
            UpdateNavigation();

            AddDetail("Name", entry.Name);
            AddDetail("Kind", GetEntryKind(entry.Name));
            AddDetail("Size", FormatSize(entry.DecompressedSize));
            AddDetail("Offset", entry.Offset.ToString("N0") + " bytes");
            AddDetail("Serialized", "No");
            AddDetail("Note", "This entry is stored as a raw bundle resource. Its bytes are listed by the bundle parser, but this screen does not decode its internal format.");
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not read bundle entry:\n{ex.Message}";
        }
    }

    private void ShowAssetDetails(AssetFileInfo asset)
    {
        if (_currentAssets is null)
            return;

        _currentPage = BrowserPage.AssetDetails;
        _searchBox.IsVisible = false;
        _status.Text = $"Asset details • Path ID {asset.PathId}";
        _items.Children.Clear();
        UpdateNavigation();

        var typeId = asset.GetTypeId(_currentAssets.file);
        AddDetail("Path ID", asset.PathId.ToString());
        AddDetail("Type ID", typeId.ToString());
        AddDetail("Data size", FormatSize(asset.ByteSize));
        AddDetail("Relative data offset", asset.ByteStart.ToString("N0") + " bytes");
        AddDetail("Absolute file offset", asset.GetAbsoluteByteStart(_currentAssets.file).ToString("N0") + " bytes");
        AddDetail("Source file", _currentAssets.name);
        AddDetail("Scope", "Metadata view only. Editing and raw object-field decoding are not part of this milestone.");
    }

    private void GoBack()
    {
        switch (_currentPage)
        {
            case BrowserPage.Assets when _currentBundle is not null:
                ShowBundleContents();
                break;
            case BrowserPage.EntryDetails when _currentBundle is not null:
                ShowBundleContents();
                break;
            case BrowserPage.AssetDetails:
                ShowAssetsContents();
                break;
            default:
                _currentPage = BrowserPage.Home;
                _searchBox.IsVisible = false;
                _items.Children.Clear();
                _status.Text = "Open a Unity AssetBundle or serialized assets file.";
                UpdateNavigation();
                break;
        }
    }

    private void UpdateNavigation()
    {
        _backButton.IsVisible = _currentPage != BrowserPage.Home;
    }

    private void AddDetail(string label, string value)
    {
        _items.Children.Add(CreateInfoRow(label, value));
    }

    private void AddEmptyMessage(string message)
    {
        _items.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brush.Parse("#AAB8C2"),
            Margin = new Thickness(6, 12)
        });
    }

    private static Control CreateActionRow(string title, string detail, Action action)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(11),
            Background = Brush.Parse("#202F3A"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Content = new StackPanel
            {
                Spacing = 4,
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
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush.Parse("#AAB8C2")
                    }
                }
            }
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Control CreateInfoRow(string title, string detail)
    {
        return new Border
        {
            Background = Brush.Parse("#202F3A"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(11),
            Child = new StackPanel
            {
                Spacing = 4,
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
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush.Parse("#AAB8C2"
                        )
                    }
                }
            }
        };
    }

    private static bool Matches(string value, string query)
    {
        return string.IsNullOrEmpty(query) ||
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetEntryKind(string name)
    {
        if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
            return "Resource stream (.resS)";
        if (name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return "Resource file";
        if (name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
            return "Assets-like file (not flagged serialized)";
        return "Raw bundle entry";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:N0} B" : $"{size:0.##} {units[unit]}";
    }
}
