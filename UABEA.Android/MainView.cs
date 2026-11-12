using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Material.Icons;
using Material.Icons.Avalonia;
using Button = Avalonia.Controls.Button;
using ProgressBar = Avalonia.Controls.ProgressBar;
using Orientation = Avalonia.Layout.Orientation;

namespace UABEA.Mobile;

public sealed class MainView : UserControl
{
    private const int PageSize = 150;

    private static readonly IBrush BackgroundBrush = Brush.Parse("#000000");
    private static readonly IBrush SurfaceBrush = Brush.Parse("#0D0D0D");
    private static readonly IBrush SurfaceContainerBrush = Brush.Parse("#171717");
    private static readonly IBrush SurfaceHighBrush = Brush.Parse("#252525");
    private static readonly IBrush OutlineBrush = Brush.Parse("#414141");
    private static readonly IBrush PrimaryBrush = Brush.Parse("#FFFFFF");
    private static readonly IBrush OnPrimaryBrush = Brush.Parse("#000000");
    private static readonly IBrush TextBrush = Brush.Parse("#FFFFFF");
    private static readonly IBrush MutedBrush = Brush.Parse("#BDBDBD");

    private readonly TextBlock _status;
    private readonly StackPanel _items;
    private readonly TextBox _searchBox;
    private readonly ComboBox _filterBox;
    private readonly Button _filterButton;
    private readonly ScrollViewer _scrollViewer;
    private readonly Button _backButton;
    private readonly Button _loadMoreButton;
    private readonly StackPanel _loadingIndicator;
    private readonly TextBlock _spinnerGlyph;
    private readonly RotateTransform _spinnerRotation;
    private readonly DispatcherTimer _spinnerTimer;
    private readonly System.Threading.SemaphoreSlim _assetNameLock = new(1, 1);

    private AssetsManager? _assetsManager;
    private string? _currentTempPath;
    private BundleFileInstance? _currentBundle;
    private AssetsFileInstance? _currentAssets;
    private string _currentFileName = "";
    private BrowserPage _currentPage = BrowserPage.Home;
    private List<RowData> _currentRows = new();
    private List<AssetRow> _assetRows = new();
    private int _visibleRows;
    private bool _isRendering;
    private bool _isBusy;

    private enum BrowserPage { Home, Bundle, Assets, EntryDetails, AssetDetails }

    private sealed record RowData(string Title, string Detail, MaterialIconKind Icon, Action Action, AssetFileInfo? AssetInfo = null);
    private sealed record OpenResult(AssetsManager Manager, BundleFileInstance? Bundle,
        AssetsFileInstance? Assets, List<RowData> Rows, List<AssetRow> AssetRows,
        string Summary, BrowserPage Page);
    private sealed record AssetRow(AssetFileInfo Info, string Name, string TypeName);

    public MainView()
    {
        var root = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(18, 18, 18, 24),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = "UABEA Mobile",
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = TextBrush
        });
        header.Children.Add(new TextBlock
        {
            Text = "Unity Assets & AssetBundle Explorer",
            FontSize = 13,
            Foreground = MutedBrush
        });
        root.Children.Add(header);

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var openButton = new Button
        {
            Content = CreateIconLabel(MaterialIconKind.FolderOpen, "Open Unity file", OnPrimaryBrush, 16),
            MinHeight = 54,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = PrimaryBrush,
            Foreground = OnPrimaryBrush,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18, 10)
        };
        ToolTip.SetTip(openButton, "Open a Unity file");
        openButton.Click += OpenFile_Click;

        _backButton = new Button
        {
            Content = CreateIcon(MaterialIconKind.ArrowLeft, 22, TextBrush),
            MinWidth = 54,
            MinHeight = 54,
            IsVisible = false,
            Margin = new Thickness(10, 0, 0, 0),
            Background = SurfaceHighBrush,
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(18)
        };
        ToolTip.SetTip(_backButton, "Back");
        _backButton.Click += (_, _) => GoBack();
        Grid.SetColumn(_backButton, 1);
        toolbar.Children.Add(openButton);
        toolbar.Children.Add(_backButton);
        root.Children.Add(toolbar);

        var searchToolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _searchBox = new TextBox
        {
            Watermark = "Search files or assets",
            MinHeight = 50,
            Padding = new Thickness(16, 10),
            Background = SurfaceContainerBrush,
            Foreground = TextBrush,
            BorderBrush = OutlineBrush,
            CornerRadius = new CornerRadius(16),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsVisible = false
        };
        _searchBox.TextChanged += (_, _) => RefreshCurrentList();

        _filterButton = new Button
        {
            Content = CreateIcon(MaterialIconKind.FilterVariant, 22, TextBrush),
            MinWidth = 50,
            MinHeight = 50,
            IsVisible = false,
            Margin = new Thickness(10, 0, 0, 0),
            Background = SurfaceHighBrush,
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(16)
        };
        ToolTip.SetTip(_filterButton, "Filter by asset type");
        _filterButton.Click += (_, _) => _filterBox.IsVisible = !_filterBox.IsVisible;

        searchToolbar.Children.Add(_searchBox);
        Grid.SetColumn(_filterButton, 1);
        searchToolbar.Children.Add(_filterButton);
        root.Children.Add(searchToolbar);

        _filterBox = new ComboBox
        {
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = SurfaceContainerBrush,
            Foreground = TextBrush,
            IsVisible = false,
            ItemsSource = new[] { "All types" },
            SelectedIndex = 0
        };
        _filterBox.SelectionChanged += (_, _) => RefreshCurrentList();
        root.Children.Add(_filterBox);

        _spinnerRotation = new RotateTransform();
        _spinnerGlyph = new TextBlock
        {
            Text = "◌",
            FontSize = 32,
            Foreground = PrimaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = _spinnerRotation
        };
        _loadingIndicator = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 2,
            IsVisible = false,
            Children =
            {
                _spinnerGlyph,
                new TextBlock
                {
                    Text = "Loading...",
                    FontSize = 12,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
        _spinnerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _spinnerTimer.Tick += (_, _) => _spinnerRotation.Angle = (_spinnerRotation.Angle + 18) % 360;
        root.Children.Add(_loadingIndicator);

        var statusCard = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16)
        };
        _status = new TextBlock
        {
            Text = "Open a Unity AssetBundle or serialized assets file.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = TextBrush,
            FontSize = 14
        };
        statusCard.Child = _status;
        root.Children.Add(statusCard);

        _items = new StackPanel { Spacing = 9 };
        var listFrame = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(10),
            Child = _items
        };
        root.Children.Add(listFrame);

        _loadMoreButton = new Button
        {
            Content = CreateIconLabel(MaterialIconKind.ChevronDown, "Load more", TextBrush, 15),
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = SurfaceHighBrush,
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(18),
            IsVisible = false
        };
        _loadMoreButton.Click += (_, _) => RenderNextPage();
        root.Children.Add(_loadMoreButton);

        _scrollViewer = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _scrollViewer.ScrollChanged += (_, _) => TryAutoLoadMore();
        Content = _scrollViewer;
        Background = BackgroundBrush;
    }

    public bool HandleSystemBack()
    {
        if (_currentPage == BrowserPage.Home)
            return false;

        GoBack();
        return true;
    }

    private async void OpenFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var selected = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Open Unity asset or bundle", AllowMultiple = false });
        var file = selected.FirstOrDefault();
        if (file is null || _isBusy) return;

        _isBusy = true;
        SetLoading(true, $"Copying {file.Name}...");
        _items.Children.Clear();
        _searchBox.IsVisible = false;
        _filterButton.IsVisible = false;
        _filterBox.IsVisible = false;
        _loadMoreButton.IsVisible = false;
        _searchBox.Text = "";
        SetFilterOptions(new[] { "All types" });

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

            _assetsManager?.UnloadAllAssetsFiles(true);
            if (_currentTempPath is not null)
            {
                try { File.Delete(_currentTempPath); } catch { }
            }

            _assetsManager = result.Manager;
            _currentTempPath = localPath;
            localPath = null;
            _currentBundle = result.Bundle;
            _currentAssets = result.Assets;
            _currentFileName = file.Name;
            _currentPage = result.Page;
            _currentRows = result.Rows;
            _assetRows = result.AssetRows;
            _status.Text = result.Summary;
            _searchBox.IsVisible = result.Page is BrowserPage.Bundle or BrowserPage.Assets;
            _filterButton.IsVisible = _searchBox.IsVisible;
            _filterBox.IsVisible = false;
            SetFilterOptions(result.Page == BrowserPage.Assets
                ? result.AssetRows.Select(row => row.TypeName)
                : result.Rows.Select(row => GetEntryKind(row.Title)));
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
            _filterButton.IsVisible = false;
            _filterBox.IsVisible = false;
            UpdateNavigation();
        }
        finally
        {
            if (localPath is not null)
            {
                try { File.Delete(localPath); } catch { }
            }
            _isBusy = false;
            SetLoading(false);
        }
    }

    private static OpenResult LoadFileInBackground(string path, string fileName)
    {
        var manager = new AssetsManager();
        var classDataPath = EnsureClassDataFile();
        manager.LoadClassPackage(classDataPath);

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
                    $"{kind} • {FormatSize(entry.DecompressedSize)}",
                    GetEntryIcon(capturedName), () => { }));
            }
            return new OpenResult(manager, bundle, null, rows, new List<AssetRow>(),
                $"Bundle: {fileName}\nEntries: {rows.Count}", BrowserPage.Bundle);
        }

        if (AssetsFile.IsAssetsFile(path))
        {
            var assets = manager.LoadAssetsFile(path, false);
            LoadClassDatabase(manager, assets.file.Metadata.UnityVersion);
            var rows = BuildAssetRows(manager, assets);
            return new OpenResult(manager, null, assets,
                rows.Select(row => new RowData(row.Name,
                    $"{row.TypeName} • {FormatSize(row.Info.ByteSize)} • ID: {row.Info.PathId}",
                    GetAssetIcon(row.TypeName), () => { })).ToList(), rows,
                $"Assets file: {assets.name}\nAssets: {rows.Count}", BrowserPage.Assets);
        }

        manager.UnloadAllAssetsFiles(true);
        throw new InvalidDataException($"Unsupported or unrecognized Unity file: {fileName}");
    }

    private static string EnsureClassDataFile()
    {
        const string resourceName = "UABEA.Mobile.classdata.tpk";
        var targetPath = Path.Combine(Path.GetTempPath(), "uabea-mobile-classdata.tpk");

        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
            return targetPath;

        using var resource = typeof(MainView).GetTypeInfo().Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException("The embedded Unity class database (classdata.tpk) is missing.");
        using var output = File.Create(targetPath);
        resource.CopyTo(output);
        return targetPath;
    }

    private static void LoadClassDatabase(AssetsManager manager, string? unityVersion)
    {
        if (string.IsNullOrWhiteSpace(unityVersion) || unityVersion == "0.0.0")
            return;

        try
        {
            manager.LoadClassDatabaseFromPackage(unityVersion);
        }
        catch
        {
            // A bundle may carry its own type tree. Keep reading it even if this
            // class database does not contain the exact Unity version.
        }
    }

    private static List<AssetRow> BuildAssetRows(AssetsManager manager, AssetsFileInstance instance)
    {
        var infos = instance.file.AssetInfos;
        var result = new List<AssetRow>(infos.Count);
        foreach (var info in infos)
        {
            var typeId = info.GetTypeId(instance.file);
            var typeName = Enum.GetName(typeof(AssetClassID), typeId) ?? $"Type {typeId}";
            // Avoid parsing every asset's serialized fields during initial loading.
            // Large files can contain thousands of complex assets; read names only on demand.
            var name = "Unnamed asset";
            result.Add(new AssetRow(info, name, typeName));
        }
        return result;
    }

    private static string TryReadAssetName(AssetsManager manager, AssetsFileInstance instance,
        AssetFileInfo info, string fallbackType)
    {
        try
        {
            var baseField = manager.GetBaseField(instance, info);
            var nameField = baseField["m_Name"];
            var name = nameField?.AsString;
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();
        }
        catch
        {
            // Some asset classes do not contain m_Name or have unsupported layouts.
        }

        return "Unnamed asset";
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
            var selectedType = _filterBox.SelectedItem?.ToString() ?? "All types";
            var filtered = entries.Where(entry => (entry.Flags & 0x02) == 0 &&
                (selectedType == "All types" || GetEntryKind(entry.Name) == selectedType) &&
                (Matches(entry.Name, query) || Matches(entry.DecompressedSize.ToString(), query)))
                .ToList();

            _currentRows = filtered.Select(entry =>
            {
                var name = entry.Name;
                var isSerialized = (entry.Flags & 0x04) != 0;
                var kind = isSerialized ? "Serialized assets file" : GetEntryKind(name);
                return new RowData(name, $"{kind} • {FormatSize(entry.DecompressedSize)}",
                    GetEntryIcon(name), () => _ = OpenBundleEntryAsync(name));
            }).ToList();
        }
        else if (_currentPage == BrowserPage.Assets && _currentAssets is not null)
        {
            var selectedType = _filterBox.SelectedItem?.ToString() ?? "All types";
            _currentRows = _assetRows
                .Where(row => selectedType == "All types" || row.TypeName == selectedType)
                .Where(row => Matches(row.Name, query) || Matches(row.TypeName, query))
                .Select(row => new RowData(row.Name,
                    $"{row.TypeName} • {FormatSize(row.Info.ByteSize)} • ID: {row.Info.PathId}",
                    GetAssetIcon(row.TypeName), () => ShowAssetDetails(row.Info), row.Info))
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
        if (_isRendering || _visibleRows >= _currentRows.Count) return;
        _isRendering = true;
        var pendingAssetNames = new List<(AssetFileInfo Info, Control Control)>();
        try
        {
            var end = Math.Min(_visibleRows + PageSize, _currentRows.Count);
            for (var i = _visibleRows; i < end; i++)
            {
                var row = _currentRows[i];
                var isBundleEntry = _currentPage == BrowserPage.Bundle && _currentBundle is not null;
                var control = CreateActionRow(row.Title, row.Detail, row.Icon, row.Action,
                    isBundleEntry ? () => _ = ExportBundleEntryAsync(row.Title) : null,
                    isBundleEntry ? () => _ = OpenBundleEntryAsync(row.Title) : null);
                _items.Children.Add(control);
                if (row.AssetInfo is not null)
                    pendingAssetNames.Add((row.AssetInfo, control));
            }
            _visibleRows = end;
            _loadMoreButton.IsVisible = _visibleRows < _currentRows.Count;
            _loadMoreButton.Content = CreateIconLabel(MaterialIconKind.ChevronDown,
                $"Load more ({_currentRows.Count - _visibleRows} remaining)", TextBrush, 15);
        }
        finally { _isRendering = false; }
        if (pendingAssetNames.Count > 0)
            _ = ResolveAssetNamesAsync(pendingAssetNames, _currentAssets, _assetsManager);
    }

    private async Task ResolveAssetNamesAsync(
        List<(AssetFileInfo Info, Control Control)> pending,
        AssetsFileInstance? instance, AssetsManager? manager)
    {
        if (instance is null || manager is null) return;
        List<(Control Control, long PathId, string Name)> resolved;
        try
        {
            await _assetNameLock.WaitAsync();
            try
            {
                resolved = await Task.Run(() => pending.Select(item =>
                    (item.Control, item.Info.PathId,
                     TryReadAssetName(manager, instance, item.Info, "Unnamed asset"))).ToList());
            }
            finally { _assetNameLock.Release(); }
        }
        catch { return; }

        if (_currentAssets != instance || _currentPage != BrowserPage.Assets) return;
        foreach (var item in resolved)
        {
            _assetRows = _assetRows.Select(row => row.Info.PathId == item.PathId
                ? row with { Name = item.Name } : row).ToList();
            if (item.Control is not Button button || button.Content is not Grid grid ||
                grid.Children.Count < 2 || grid.Children[1] is not StackPanel panel ||
                panel.Children.Count == 0 || panel.Children[0] is not TextBlock title)
                continue;
            title.Text = item.Name;
        }
    }

    private void TryAutoLoadMore()
    {
        if (_isRendering || !_loadMoreButton.IsVisible || _visibleRows >= _currentRows.Count)
            return;

        if (_scrollViewer.Offset.Y + _scrollViewer.Viewport.Height >= _scrollViewer.Extent.Height - 140)
            RenderNextPage();
    }

    private void SetFilterOptions(IEnumerable<string> values)
    {
        var options = new[] { "All types" }
            .Concat(values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var previous = _filterBox.SelectedItem?.ToString() ?? "All types";
        _filterBox.ItemsSource = options;
        _filterBox.SelectedItem = options.FirstOrDefault(value =>
            string.Equals(value, previous, StringComparison.OrdinalIgnoreCase)) ?? "All types";
    }

    private async Task ExportBundleEntryAsync(string entryName)
    {
        if (_currentBundle is null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var bundle = _currentBundle;
        var entries = bundle.file.BlockAndDirInfo.DirectoryInfos;
        var index = Array.FindIndex(entries,
            entry => string.Equals(entry.Name, entryName, StringComparison.Ordinal));
        if (index < 0) return;

        var entry = entries[index];
        var destination = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export bundle entry",
                SuggestedFileName = Path.GetFileName(entry.Name)
            });
        if (destination is null) return;

        SetLoading(true, $"Exporting {entry.Name}...");
        try
        {
            using var source = new SegmentStream(
                bundle.file.DataReader.BaseStream, entry.Offset, entry.DecompressedSize);
            await using var output = await destination.OpenWriteAsync();
            await source.CopyToAsync(output);
            _status.Text = $"Exported: {entry.Name}\nSize: {FormatSize(entry.DecompressedSize)}";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not export bundle entry:\n{ex.Message}";
        }
        finally
        {
            SetLoading(false);
        }
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
                    var version = assets.file.Metadata.UnityVersion;
                    if (string.IsNullOrWhiteSpace(version) || version == "0.0.0")
                        version = bundle.file.Header.EngineVersion;
                    LoadClassDatabase(manager, version);

                    var rows = BuildAssetRows(manager, assets);
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
                    GetAssetIcon(row.TypeName), () => ShowAssetDetails(row.Info))).ToList();
                _status.Text = $"Assets file: {result.Assets.name}\nAssets: {_currentRows.Count}";
                _searchBox.IsVisible = true;
                _filterButton.IsVisible = true;
                _filterBox.IsVisible = false;
                SetFilterOptions(result.Rows.Select(row => row.TypeName));
                _searchBox.Watermark = "Search by asset name or type...";
                _searchBox.Text = "";
                _visibleRows = 0;
                RefreshCurrentList();
            }
            else
            {
                _currentPage = BrowserPage.EntryDetails;
                _searchBox.IsVisible = false;
                _filterButton.IsVisible = false;
                _filterBox.IsVisible = false;
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

    private async void ShowAssetDetails(AssetFileInfo asset)
    {
        if (_currentAssets is null || _assetsManager is null) return;
        var instance = _currentAssets;
        var manager = _assetsManager;
        _currentPage = BrowserPage.AssetDetails;
        _searchBox.IsVisible = false;
        _filterButton.IsVisible = false;
        _filterBox.IsVisible = false;
        _items.Children.Clear();
        _loadMoreButton.IsVisible = false;
        UpdateNavigation();

        var typeId = asset.GetTypeId(instance.file);
        var typeName = Enum.GetName(typeof(AssetClassID), typeId) ?? $"Type {typeId}";
        _status.Text = $"Reading asset details...\n{typeName}";
        SetLoading(true);

        string name;
        try
        {
            name = await Task.Run(() => TryReadAssetName(manager, instance, asset, typeName));
        }
        catch
        {
            name = "Unnamed asset";
        }
        finally
        {
            SetLoading(false);
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
            return;

        _status.Text = $"Asset • {name}";
        AddDetail("Name", name);
        AddDetail("Type", typeName);
        AddDetail("Path ID", asset.PathId.ToString());
        AddDetail("Type ID", typeId.ToString());
        AddDetail("Data size", FormatSize(asset.ByteSize));
        AddDetail("Relative data offset", asset.ByteStart.ToString("N0") + " bytes");
        AddDetail("Absolute file offset",
            asset.GetAbsoluteByteStart(instance.file).ToString("N0") + " bytes");
        AddDetail("Source file", instance.name);

        if (typeId == (int)AssetClassID.TextAsset)
        {
            await ShowTextAssetPreviewAsync(manager, instance, asset);
        }
        if (typeId == (int)AssetClassID.Texture2D)
        {
            await ShowTexturePreviewAsync(manager, instance, asset);
        }
        else if (typeId == (int)AssetClassID.Font)
        {
            await ShowFontPreviewAsync(manager, instance, asset);
        }
    }

    private async Task ShowTexturePreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        _status.Text = "Loading image preview...";
        SetLoading(true);
        Avalonia.Media.Imaging.Bitmap? previewBitmap = null;
        string? error = null;

        try
        {
            byte[] png = await Task.Run(() =>
            {
                var field = manager.GetBaseField(instance, asset);
                if (field == null)
                    throw new InvalidDataException("Texture data could not be read.");
                var texture = TextureFile.ReadTextureFile(field);
                if (texture.m_Width <= 0 || texture.m_Height <= 0 ||
                    texture.m_Width > 8192 || texture.m_Height > 8192)
                    throw new InvalidDataException("Unsupported or invalid image dimensions.");

                byte[] data = texture.pictureData ?? Array.Empty<byte>();
                if (texture.m_StreamData.size > 0 && !string.IsNullOrWhiteSpace(texture.m_StreamData.path))
                {
                    string streamPath = texture.m_StreamData.path;
                    if (streamPath.StartsWith("archive:/", StringComparison.OrdinalIgnoreCase))
                        streamPath = Path.GetFileName(streamPath);

                    if (instance.parentBundle != null)
                    {
                        var bundle = instance.parentBundle.file;
                        var entry = bundle.BlockAndDirInfo.DirectoryInfos.FirstOrDefault(x =>
                            Path.GetFileName(x.Name).Equals(Path.GetFileName(streamPath), StringComparison.OrdinalIgnoreCase));
                        if (entry == null)
                            throw new FileNotFoundException("The texture resource was not found inside the bundle.");
                        bundle.DataReader.Position = entry.Offset + (long)texture.m_StreamData.offset;
                        data = bundle.DataReader.ReadBytes((int)texture.m_StreamData.size);
                    }
                    else
                    {
                        string root = Path.GetDirectoryName(instance.path) ?? "";
                        string fullPath = Path.IsPathRooted(streamPath) ? streamPath : Path.Combine(root, streamPath);
                        if (!File.Exists(fullPath))
                            throw new FileNotFoundException("The external texture resource file was not found.");
                        using var stream = File.OpenRead(fullPath);
                        stream.Position = (long)texture.m_StreamData.offset;
                        data = new byte[(int)texture.m_StreamData.size];
                        int read = 0;
                        while (read < data.Length)
                        {
                            int count = stream.Read(data, read, data.Length - read);
                            if (count == 0) break;
                            read += count;
                        }
                    }
                }

                if (data.Length == 0)
                    throw new InvalidDataException("This texture contains no image data.");

                // TextureFile.DecodeManaged returns pixels in BGRA byte order.
                // Android Bitmap.SetPixels expects packed ARGB integers, so swap
                // the red/blue channels while preserving the source alpha.
                byte[] bgra = TextureFile.DecodeManaged(
                    data, (TextureFormat)texture.m_TextureFormat,
                    texture.m_Width, texture.m_Height);
                if (bgra == null || bgra.Length < texture.m_Width * texture.m_Height * 4)
                    throw new NotSupportedException("This texture format is not supported by the mobile preview decoder.");

                int width = texture.m_Width;
                int height = texture.m_Height;
                using var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(
                    width, height, global::Android.Graphics.Bitmap.Config.Argb8888!);
                var pixels = new int[width * height];

                // Unity texture pixel data is bottom-left oriented. The current
                // decoder output is also horizontally reversed for this preview
                // path, so map both axes to restore the expected top-left image.
                for (int y = 0; y < height; y++)
                {
                    int sourceY = height - 1 - y;
                    for (int x = 0; x < width; x++)
                    {
                        int sourceX = width - 1 - x;
                        int sourceOffset = (sourceY * width + sourceX) * 4;
                        byte blue = bgra[sourceOffset];
                        byte green = bgra[sourceOffset + 1];
                        byte red = bgra[sourceOffset + 2];
                        byte alpha = bgra[sourceOffset + 3];

                        pixels[y * width + x] =
                            (alpha << 24) | (red << 16) | (green << 8) | blue;
                    }
                }

                bitmap.SetPixels(pixels, 0, width, 0, 0, width, height);
                using var output = new MemoryStream();
                bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, output);
                return output.ToArray();
            });

            using var imageStream = new MemoryStream(png);
            previewBitmap = new Avalonia.Media.Imaging.Bitmap(imageStream);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            SetLoading(false);
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            previewBitmap?.Dispose();
            return;
        }

        _items.Children.Add(new Border
        {
            Background = SurfaceContainerBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = previewBitmap != null
                ? new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Image preview", FontWeight = FontWeight.SemiBold, Foreground = TextBrush },
                        new Image { Source = previewBitmap, Stretch = Stretch.Uniform, MaxHeight = 360 }
                    }
                }
                : new TextBlock
                {
                    Text = "Image preview unavailable: " + error,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap
                }
        });
    }

    private async Task ShowFontPreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        _status.Text = "Loading font preview...";
        SetLoading(true);
        Avalonia.Media.Imaging.Bitmap? previewBitmap = null;
        string? fontName = null;
        string? error = null;
        string? tempFontPath = null;

        try
        {
            byte[] fontBytes = await Task.Run(() =>
            {
                // Unity stores Font.m_FontData as a generic array in some class
                // databases. Reinterpret its element as ByteArray before deserializing,
                // matching UABEA's FontPlugin implementation.
                var template = manager.GetTemplateBaseField(instance, asset);
                if (template == null)
                    throw new InvalidDataException("Font type information could not be read.");

                var fontDataTemplate = template.Children.FirstOrDefault(x => x.Name == "m_FontData");
                if (fontDataTemplate == null || fontDataTemplate.Children.Count == 0)
                    throw new InvalidDataException("This Unity Font has no m_FontData field.");

                fontDataTemplate.Children[0].ValueType = AssetValueType.ByteArray;
                var field = manager.GetBaseField(instance, asset);
                if (field == null)
                    throw new InvalidDataException("Font data could not be read.");

                fontName = field["m_Name"]?.AsString;
                return field["m_FontData.Array"]?.AsByteArray ?? Array.Empty<byte>();
            });

            if (fontBytes.Length == 0)
                throw new InvalidDataException("This font uses an external or built-in font and has no embedded TTF/OTF data.");

            tempFontPath = Path.Combine(Path.GetTempPath(), "uabea-preview-" + Guid.NewGuid().ToString("N") +
                (fontBytes.Length > 4 && fontBytes[0] == 0x4F && fontBytes[1] == 0x54 ? ".otf" : ".ttf"));
            await File.WriteAllBytesAsync(tempFontPath, fontBytes);

            using var typeface = global::Android.Graphics.Typeface.CreateFromFile(tempFontPath);
            using var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(900, 360, global::Android.Graphics.Bitmap.Config.Argb8888!);
            using (var canvas = new global::Android.Graphics.Canvas(bitmap))
            using (var paint = new global::Android.Graphics.Paint(global::Android.Graphics.PaintFlags.AntiAlias))
            {
                canvas.DrawColor(global::Android.Graphics.Color.Rgb(23, 23, 23));
                paint.Color = global::Android.Graphics.Color.White;
                paint.SetTypeface(typeface);
                paint.TextSize = 62;
                canvas.DrawText("Aa Bb Cc 123", 28, 105, paint);
                paint.TextSize = 34;
                canvas.DrawText("The quick brown fox jumps over the lazy dog.", 28, 180, paint);
                canvas.DrawText("0123456789  !? @#&", 28, 245, paint);
                paint.TextSize = 22;
                canvas.DrawText(fontName ?? "Unity Font", 28, 315, paint);
            }

            using var pngStream = new MemoryStream();
            bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png!, 100, pngStream);
            pngStream.Position = 0;
            previewBitmap = new Avalonia.Media.Imaging.Bitmap(pngStream);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFontPath))
            {
                try { File.Delete(tempFontPath); } catch { }
            }
            SetLoading(false);
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            previewBitmap?.Dispose();
            return;
        }

        _items.Children.Add(new Border
        {
            Background = SurfaceContainerBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = previewBitmap != null
                ? new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Font preview • " + (fontName ?? "Unnamed font"), FontWeight = FontWeight.SemiBold, Foreground = TextBrush },
                        new Image { Source = previewBitmap, Stretch = Stretch.Uniform, MaxHeight = 220 }
                    }
                }
                : new TextBlock
                {
                    Text = "Font preview unavailable: " + error,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap
                }
        });
    }

    private async Task ShowTextAssetPreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        _status.Text = "Loading text preview...";
        SetLoading(true);

        string preview;
        try
        {
            preview = await Task.Run(() =>
            {
                var field = manager.GetBaseField(instance, asset);
                var bytes = field["m_Script"].AsByteArray ?? Array.Empty<byte>();
                const int maxPreviewBytes = 1024 * 1024;
                bool truncated = bytes.Length > maxPreviewBytes;
                int length = Math.Min(bytes.Length, maxPreviewBytes);

                string text;
                if (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                    text = Encoding.Unicode.GetString(bytes, 2, length - 2);
                else if (length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                    text = Encoding.BigEndianUnicode.GetString(bytes, 2, length - 2);
                else if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    text = Encoding.UTF8.GetString(bytes, 3, length - 3);
                else
                    text = Encoding.UTF8.GetString(bytes, 0, length);

                return truncated
                    ? text + "\\n\\n[Preview limited to the first 1 MB. Export the asset to view the complete content.]"
                    : text;
            });
        }
        catch (Exception ex)
        {
            preview = $"Could not read TextAsset content: {ex.Message}";
        }
        finally
        {
            SetLoading(false);
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
            return;

        _status.Text = "TextAsset preview";
        _items.Children.Add(new Border
        {
            Background = SurfaceContainerBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = string.IsNullOrEmpty(preview) ? "(This TextAsset is empty.)" : preview,
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 13,
                    Foreground = TextBrush,
                    TextWrapping = TextWrapping.NoWrap
                }
            }
        });
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
                _filterButton.IsVisible = false;
                _filterBox.IsVisible = false;
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
        _filterButton.IsVisible = true;
        _filterBox.IsVisible = false;
        SetFilterOptions(_currentBundle.file.BlockAndDirInfo.DirectoryInfos
            .Where(entry => (entry.Flags & 0x02) == 0).Select(entry => GetEntryKind(entry.Name)));
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
        _filterButton.IsVisible = true;
        _filterBox.IsVisible = false;
        SetFilterOptions(_assetRows.Select(row => row.TypeName));
        _searchBox.Watermark = "Search by asset name or type...";
        _status.Text = $"Assets file: {_currentAssets.name}\nAssets: {_currentAssets.file.AssetInfos.Count}";
        UpdateNavigation();
        RefreshCurrentList();
    }

    private void SetLoading(bool loading, string? message = null)
    {
        _loadingIndicator.IsVisible = loading;
        if (loading)
        {
            if (!_spinnerTimer.IsEnabled) _spinnerTimer.Start();
        }
        else
        {
            _spinnerTimer.Stop();
            _spinnerRotation.Angle = 0;
        }
        if (message is not null) _status.Text = message;
    }

    private void UpdateNavigation() => _backButton.IsVisible = _currentPage != BrowserPage.Home;

    private void AddDetail(string label, string value)
    {
        var icon = label switch
        {
            "Name" => MaterialIconKind.TagText,
            "Type" => MaterialIconKind.ShapeOutline,
            "Path ID" => MaterialIconKind.Identifier,
            "Type ID" => MaterialIconKind.Numeric,
            "Data size" => MaterialIconKind.Database,
            "Source file" => MaterialIconKind.FileDocumentOutline,
            _ => MaterialIconKind.InformationOutline
        };
        _items.Children.Add(CreateInfoRow(label, value, icon));
    }

    private void AddEmptyMessage(string message)
    {
        _items.Children.Add(new Border
        {
            Background = SurfaceContainerBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Child = CreateIconLabel(MaterialIconKind.InformationOutline, message, MutedBrush, 14)
        });
    }

    private static Control CreateActionRow(string title, string detail,
        MaterialIconKind iconKind, Action action, Action? exportAction = null, Action? openAction = null)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 13),
            Background = SurfaceContainerBrush,
            Foreground = TextBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(17),
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("44,*,24"),
                Children =
                {
                    new Border
                    {
                        Width = 38,
                        Height = 38,
                        CornerRadius = new CornerRadius(13),
                        Background = SurfaceHighBrush,
                        Child = CreateIcon(iconKind, 21, PrimaryBrush),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new StackPanel
                    {
                        Spacing = 5,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Margin = new Thickness(8, 0, 8, 0),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontWeight = FontWeight.SemiBold,
                                FontSize = 14,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = TextBrush
                            },
                            new TextBlock
                            {
                                Text = detail,
                                FontSize = 12,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = MutedBrush
                            }
                        }
                    },
                    CreateIcon(MaterialIconKind.ChevronRight, 20, MutedBrush)
                }
            }
        };
        Grid.SetColumn((Control)((Grid)button.Content).Children[0], 0);
        Grid.SetColumn((Control)((Grid)button.Content).Children[1], 1);
        Grid.SetColumn((Control)((Grid)button.Content).Children[2], 2);
        ToolTip.SetTip(button, $"Open {title}");
        var suppressNextClick = false;
        button.PointerPressed += (_, _) => suppressNextClick = false;
        button.Holding += (_, args) =>
        {
            if (args.HoldingState.ToString() == "Started")
                suppressNextClick = true;
        };
        button.Click += (_, _) =>
        {
            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }
            action();
        };

        if (exportAction is not null || openAction is not null)
        {
            var menu = new ContextMenu();
            if (openAction is not null)
            {
                var openItem = new MenuItem { Header = "Open" };
                openItem.Click += (_, _) => openAction();
                menu.Items.Add(openItem);
            }
            if (exportAction is not null)
            {
                var exportItem = new MenuItem { Header = "Export" };
                exportItem.Click += (_, _) => exportAction();
                menu.Items.Add(exportItem);
            }
            button.ContextMenu = menu;
        }
        return button;
    }

    private static Control CreateInfoRow(string title, string detail, MaterialIconKind iconKind)
    {
        var icon = CreateIcon(iconKind, 20, PrimaryBrush);
        var text = new StackPanel
        {
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = MutedBrush
                },
                new TextBlock
                {
                    Text = detail,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = TextBrush
                }
            }
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("34,*"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(icon);
        grid.Children.Add(text);

        return new Border
        {
            Background = SurfaceContainerBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = grid
        };
    }

    private static Control CreateIcon(MaterialIconKind kind, double size, IBrush? foreground = null) =>
        new MaterialIcon
        {
            Kind = kind,
            Width = size,
            Height = size,
            Foreground = foreground ?? TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static Control CreateIconLabel(MaterialIconKind kind, string text,
        IBrush foreground, double fontSize)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(CreateIcon(kind, fontSize + 5, foreground));
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static MaterialIconKind GetAssetIcon(string typeName)
    {
        if (typeName.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Sprite", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.Image;
        if (typeName.Contains("Audio", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.MusicNote;
        if (typeName.Contains("GameObject", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.CubeOutline;
        if (typeName.Contains("Text", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.FileDocumentOutline;
        if (typeName.Contains("MonoBehaviour", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.CodeBraces;
        if (typeName.Contains("Font", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.FormatFont;
        if (typeName.Contains("Material", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.PaletteOutline;
        if (typeName.Contains("Mesh", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.Shape;
        return MaterialIconKind.Cube;
    }

    private static MaterialIconKind GetEntryIcon(string name)
    {
        if (name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.Database;
        if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            return MaterialIconKind.FileDocumentOutline;
        return MaterialIconKind.FileOutline;
    }

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
