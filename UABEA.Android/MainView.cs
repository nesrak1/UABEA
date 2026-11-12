using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Button = Avalonia.Controls.Button;

namespace UABEA.Mobile;

public sealed class MainView : UserControl
{
    private const int PageSize = 150;

    private readonly TextBlock _status;
    private readonly StackPanel _items;
    private readonly TextBox _searchBox;
    private readonly Button _backButton;
    private readonly Button _loadMoreButton;
    private readonly Avalonia.Controls.ProgressBar _loadingBar;

    private AssetsManager? _assetsManager;
    private BundleFileInstance? _currentBundle;
    private AssetsFileInstance? _currentAssets;
    private string _currentFileName = "";
    private BrowserPage _currentPage = BrowserPage.Home;
    private List<RowData> _currentRows = new();
    private List<AssetRow> _assetRows = new();
    private int _visibleRows;

    private enum BrowserPage { Home, Bundle, Assets, EntryDetails, AssetDetails }

    private sealed record RowData(string Title, string Detail, Action Action);
    private sealed record OpenResult(AssetsManager Manager, BundleFileInstance? Bundle,
        AssetsFileInstance? Assets, List<RowData> Rows, List<AssetRow> AssetRows,
        string Summary, BrowserPage Page);
    private sealed record AssetRow(AssetFileInfo Info, string Name, string TypeName);

    public MainView()
    {
        var root = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(16),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        root.Children.Add(new TextBlock
        {
            Text = "UABEA Mobile",
            FontSize = 27,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#56C3F2")
        });
        root.Children.Add(new TextBlock
        {
            Text = "Unity Assets & AssetBundle Explorer",
            FontSize = 13,
            Foreground = Brush.Parse("#AAB8C2")
        });

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
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
            Content = "Back", MinHeight = 48, IsVisible = false,
            Margin = new Thickness(8, 0, 0, 0),
            Background = Brush.Parse("#263946"), Foreground = Brushes.White
        };
        _backButton.Click += (_, _) => GoBack();
        Grid.SetColumn(_backButton, 1);
        toolbar.Children.Add(openButton);
        toolbar.Children.Add(_backButton);
        root.Children.Add(toolbar);

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
        root.Children.Add(_searchBox);

        _loadingBar = new Avalonia.Controls.ProgressBar
        {
            IsIndeterminate = true,
            Height = 5,
            IsVisible = false,
            Foreground = Brush.Parse("#56C3F2")
        };
        root.Children.Add(_loadingBar);

        _status = new TextBlock
        {
            Text = "Open a Unity AssetBundle or serialized assets file.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush.Parse("#E5EDF2")
        };
        root.Children.Add(_status);

        _items = new StackPanel { Spacing = 7 };
        var listFrame = new Border
        {
            Background = Brush.Parse("#17232D"),
            BorderBrush = Brush.Parse("#2C3E4B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            Child = _items
        };
        root.Children.Add(listFrame);

        _loadMoreButton = new Button
        {
            Content = "Load more",
            MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush.Parse("#263946"),
            Foreground = Brushes.White,
            IsVisible = false
        };
        _loadMoreButton.Click += (_, _) => RenderNextPage();
        root.Children.Add(_loadMoreButton);

        // One scroll viewer only. Nested scroll viewers were competing for touch gestures.
        Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Background = Brush.Parse("#101820");
    }

    private async void OpenFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var selected = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Open Unity asset or bundle", AllowMultiple = false });
        var file = selected.FirstOrDefault();
        if (file is null) return;

        SetLoading(true, $"Copying {file.Name}...");
        _items.Children.Clear();
        _searchBox.IsVisible = false;
        _loadMoreButton.IsVisible = false;
        _searchBox.Text = "";

        string? localPath = null;
        try
        {
            localPath = Path.Combine(Path.GetTempPath(),
                $"{Guid.NewGuid():N}_{Path.GetFileName(file.Name)}");

            await using (var source = await file.OpenReadAsync())
            await using (var destination = File.Create(localPath))
                await source.CopyToAsync(destination);

            SetLoading(true, $"Analyzing {file.Name}...");
            var path = localPath;
            var result = await Task.Run(() => LoadFileInBackground(path, file.Name));

            _assetsManager = result.Manager;
            _currentBundle = result.Bundle;
            _currentAssets = result.Assets;
            _currentFileName = file.Name;
            _currentPage = result.Page;
            _currentRows = result.Rows;
            _assetRows = result.AssetRows;
            _status.Text = result.Summary;
            _searchBox.IsVisible = result.Page is BrowserPage.Bundle or BrowserPage.Assets;
            _searchBox.Watermark = result.Page == BrowserPage.Bundle
                ? "Search bundle entries..." : "Search by asset name or type...";
            _searchBox.Text = "";
            _visibleRows = 0;
            RefreshCurrentList();
            UpdateNavigation();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open this file:\n{ex.Message}";
            _items.Children.Clear();
            _currentBundle = null;
            _currentAssets = null;
            _currentRows.Clear();
            _assetRows.Clear();
            _currentPage = BrowserPage.Home;
            UpdateNavigation();
        }
        finally
        {
            SetLoading(false);
            // Keep the copied file available while AssetsTools has it open.
        }
    }

    private static OpenResult LoadFileInBackground(string path, string fileName)
    {
        var manager = new AssetsManager();
        var signature = new byte[8];
        using (var stream = File.OpenRead(path))
            _ = stream.Read(signature, 0, signature.Length);

        var header = Encoding.ASCII.GetString(signature);
        if (header.StartsWith("UnityFS", StringComparison.Ordinal) ||
            header.StartsWith("UnityRaw", StringComparison.Ordinal) ||
            header.StartsWith("UnityWeb", StringComparison.Ordinal))
        {
            var bundle = manager.LoadBundleFile(path, false);
            var rows = new List<RowData>();
            var entries = bundle.file.BlockAndDirInfo.DirectoryInfos;
            foreach (var entry in entries)
            {
                if ((entry.Flags & 0x02) != 0) continue;
                var capturedName = entry.Name;
                var kind = (entry.Flags & 0x04) != 0
                    ? "Serialized assets file" : GetEntryKind(entry.Name);
                rows.Add(new RowData(capturedName,
                    $"{kind} • {FormatSize(entry.DecompressedSize)}", () => { }));
            }
            return new OpenResult(manager, bundle, null, rows, new List<AssetRow>(),
                $"Bundle: {fileName}\nEntries: {rows.Count}", BrowserPage.Bundle);
        }

        if (AssetsFile.IsAssetsFile(path))
        {
            var assets = manager.LoadAssetsFile(path, false);
            var rows = BuildAssetRows(assets);
            return new OpenResult(manager, null, assets,
                rows.Select(row => new RowData(row.Name,
                    $"{row.TypeName} • {FormatSize(row.Info.ByteSize)}",
                    () => { })).ToList(), rows,
                $"Assets file: {assets.name}\nAssets: {rows.Count}", BrowserPage.Assets);
        }

        manager.UnloadAllAssetsFiles(true);
        throw new InvalidDataException($"Unsupported or unrecognized Unity file: {fileName}");
    }

    private static List<AssetRow> BuildAssetRows(AssetsFileInstance instance)
    {
        var result = new List<AssetRow>(instance.file.AssetInfos.Count);
        foreach (var info in instance.file.AssetInfos)
        {
            var typeId = info.GetTypeId(instance.file);
            var typeName = Enum.GetName(typeof(AssetClassID), typeId) ?? $"Type {typeId}";
            var name = TryReadAssetName(instance.file, info, typeName);
            result.Add(new AssetRow(info, name, typeName));
        }
        return result;
    }

    private static string TryReadAssetName(AssetsFile file, AssetFileInfo info, string fallbackType)
    {
        try
        {
            var typeId = info.GetTypeId(file);
            TypeTreeType? typeTree = null;
            if (typeId == (int)AssetClassID.MonoBehaviour || typeId < 0)
                typeTree = file.Metadata.FindTypeTreeTypeByScriptIndex(file.GetScriptIndex(info));
            else
                typeTree = file.Metadata.FindTypeTreeTypeByID(typeId);

            if (typeTree is not null && typeTree.Nodes.Count > 1 &&
                typeTree.Nodes[1].GetNameString(typeTree.StringBuffer) == "m_Name")
            {
                var reader = file.Reader;
                var oldPosition = reader.Position;
                try
                {
                    reader.Position = info.GetAbsoluteByteStart(file);
                    var name = reader.ReadCountStringInt32();
                    if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                }
                finally { reader.Position = oldPosition; }
            }
        }
        catch { /* Some Unity versions do not expose a readable m_Name field. */ }

        return $"{fallbackType} (unnamed)";
    }

    private void RefreshCurrentList()
    {
        if (_items is null) return;
        _items.Children.Clear();
        _visibleRows = 0;
        var query = (_searchBox.Text ?? "").Trim();

        if (_currentPage == BrowserPage.Bundle && _currentBundle is not null)
        {
            var entries = _currentBundle.file.BlockAndDirInfo.DirectoryInfos;
            var filtered = entries.Where(entry => (entry.Flags & 0x02) == 0 &&
                (Matches(entry.Name, query) || Matches(entry.DecompressedSize.ToString(), query)))
                .ToList();

            _currentRows = filtered.Select(entry =>
            {
                var name = entry.Name;
                var isSerialized = (entry.Flags & 0x04) != 0;
                var kind = isSerialized ? "Serialized assets file" : GetEntryKind(name);
                return new RowData(name, $"{kind} • {FormatSize(entry.DecompressedSize)}",
                    () => _ = OpenBundleEntryAsync(name));
            }).ToList();
        }
        else if (_currentPage == BrowserPage.Assets && _currentAssets is not null)
        {
            var assetRows = _assetRows;
            _currentRows = assetRows
                .Where(row => Matches(row.Name, query) || Matches(row.TypeName, query))
                .Select(row => new RowData(row.Name,
                    $"{row.TypeName} • {FormatSize(row.Info.ByteSize)}",
                    () => ShowAssetDetails(row.Info)))
                .ToList();
        }

        if (_currentRows.Count == 0)
        {
            AddEmptyMessage(string.IsNullOrEmpty(query)
                ? "This file contains no displayable entries."
                : "No items match this search.");
            _loadMoreButton.IsVisible = false;
            return;
        }

        RenderNextPage();
    }

    private void RenderNextPage()
    {
        var end = Math.Min(_visibleRows + PageSize, _currentRows.Count);
        for (var i = _visibleRows; i < end; i++)
        {
            var row = _currentRows[i];
            _items.Children.Add(CreateActionRow(row.Title, row.Detail, row.Action));
        }
        _visibleRows = end;
        _loadMoreButton.IsVisible = _visibleRows < _currentRows.Count;
        _loadMoreButton.Content = $"Load more ({_currentRows.Count - _visibleRows} remaining)";
    }

    private async Task OpenBundleEntryAsync(string entryName)
    {
        if (_currentBundle is null || _assetsManager is null) return;
        var bundle = _currentBundle;
        var manager = _assetsManager;
        SetLoading(true, $"Opening {entryName}...");
        try
        {
            var result = await Task.Run(() =>
            {
                var entries = bundle.file.BlockAndDirInfo.DirectoryInfos;
                var index = Array.FindIndex(entries,
                    x => string.Equals(x.Name, entryName, StringComparison.Ordinal));
                if (index < 0) throw new InvalidDataException("Bundle entry not found.");
                var entry = entries[index];

                if ((entry.Flags & 0x04) != 0)
                {
                    var assets = manager.LoadAssetsFileFromBundle(bundle, index, false);
                    var rows = BuildAssetRows(assets);
                    return (Assets: assets, Rows: rows, IsSerialized: true,
                        Name: entry.Name, Kind: GetEntryKind(entry.Name),
                        Size: entry.DecompressedSize, Offset: entry.Offset);
                }

                return (Assets: (AssetsFileInstance?)null, Rows: new List<AssetRow>(),
                    IsSerialized: false, Name: entry.Name, Kind: GetEntryKind(entry.Name),
                    Size: entry.DecompressedSize, Offset: entry.Offset);
            });

            if (result.IsSerialized && result.Assets is not null)
            {
                _currentAssets = result.Assets;
                _assetRows = result.Rows;
                _currentPage = BrowserPage.Assets;
                _currentRows = result.Rows.Select(row => new RowData(row.Name,
                    $"{row.TypeName} • {FormatSize(row.Info.ByteSize)}",
                    () => ShowAssetDetails(row.Info))).ToList();
                _status.Text = $"Assets file: {result.Assets.name}\nAssets: {_currentRows.Count}";
                _searchBox.IsVisible = true;
                _searchBox.Watermark = "Search by asset name or type...";
                _searchBox.Text = "";
                _visibleRows = 0;
                RefreshCurrentList();
            }
            else
            {
                _currentPage = BrowserPage.EntryDetails;
                _searchBox.IsVisible = false;
                _items.Children.Clear();
                AddDetail("Name", result.Name);
                AddDetail("Kind", result.Kind);
                AddDetail("Size", FormatSize(result.Size));
                AddDetail("Offset", result.Offset.ToString("N0") + " bytes");
                AddDetail("Serialized", "No");
                AddDetail("Note", "Raw bundle resource. Its internal data is not decoded in this view.");
                _status.Text = $"Bundle entry: {result.Name}";
                _loadMoreButton.IsVisible = false;
            }
            UpdateNavigation();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not read bundle entry:\n{ex.Message}";
        }
        finally { SetLoading(false); }
    }

    private void ShowAssetDetails(AssetFileInfo asset)
    {
        if (_currentAssets is null) return;
        _currentPage = BrowserPage.AssetDetails;
        _searchBox.IsVisible = false;
        _status.Text = $"Asset • {TryReadAssetName(_currentAssets.file, asset,
            Enum.GetName(typeof(AssetClassID), asset.GetTypeId(_currentAssets.file)) ?? "Asset")}";
        _items.Children.Clear();
        _loadMoreButton.IsVisible = false;
        UpdateNavigation();

        var typeId = asset.GetTypeId(_currentAssets.file);
        AddDetail("Name", TryReadAssetName(_currentAssets.file, asset,
            Enum.GetName(typeof(AssetClassID), typeId) ?? $"Type {typeId}"));
        AddDetail("Type", Enum.GetName(typeof(AssetClassID), typeId) ?? $"Type {typeId}");
        AddDetail("Path ID", asset.PathId.ToString());
        AddDetail("Type ID", typeId.ToString());
        AddDetail("Data size", FormatSize(asset.ByteSize));
        AddDetail("Relative data offset", asset.ByteStart.ToString("N0") + " bytes");
        AddDetail("Absolute file offset", asset.GetAbsoluteByteStart(_currentAssets.file).ToString("N0") + " bytes");
        AddDetail("Source file", _currentAssets.name);
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
                _currentRows.Clear();
                _assetRows.Clear();
                _loadMoreButton.IsVisible = false;
                _status.Text = "Open a Unity AssetBundle or serialized assets file.";
                UpdateNavigation();
                break;
        }
    }

    private void ShowBundleContents()
    {
        if (_currentBundle is null) return;
        _currentPage = BrowserPage.Bundle;
        _searchBox.IsVisible = true;
        _searchBox.Watermark = "Search bundle entries...";
        _status.Text = $"Bundle: {_currentFileName}\nEntries: {_currentRows.Count}";
        UpdateNavigation();
        RefreshCurrentList();
    }

    private void ShowAssetsContents()
    {
        if (_currentAssets is null) return;
        _currentPage = BrowserPage.Assets;
        _searchBox.IsVisible = true;
        _searchBox.Watermark = "Search by asset name or type...";
        _status.Text = $"Assets file: {_currentAssets.name}\nAssets: {_currentAssets.file.AssetInfos.Count}";
        UpdateNavigation();
        RefreshCurrentList();
    }

    private void SetLoading(bool loading, string? message = null)
    {
        _loadingBar.IsVisible = loading;
        if (message is not null) _status.Text = message;
    }

    private void UpdateNavigation() => _backButton.IsVisible = _currentPage != BrowserPage.Home;

    private void AddDetail(string label, string value) =>
        _items.Children.Add(CreateInfoRow(label, value));

    private void AddEmptyMessage(string message)
    {
        _items.Children.Add(new TextBlock
        {
            Text = message, Foreground = Brush.Parse("#AAB8C2"),
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
                        Text = title, FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = detail, FontSize = 12,
                        TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#AAB8C2")
                    }
                }
            }
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Control CreateInfoRow(string title, string detail) =>
        new Border
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
                        Text = title, FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = detail, FontSize = 12,
                        TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#AAB8C2")
                    }
                }
            }
        };

    private static bool Matches(string value, string query) =>
        string.IsNullOrEmpty(query) ||
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string GetEntryKind(string name)
    {
        if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
            return "Resource stream (.resS)";
        if (name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return "Resource file";
        if (name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
            return "Assets-like file";
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
