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
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input;
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
    private readonly Grid _pageLayers;
    private readonly Button _backButton;
    private readonly Button _loadMoreButton;
    private readonly StackPanel _loadingIndicator;
    private readonly TextBlock _spinnerGlyph;
    private readonly RotateTransform _spinnerRotation;
    private readonly DispatcherTimer _spinnerTimer;
    private readonly System.Threading.SemaphoreSlim _assetNameLock = new(1, 1);

    private AssetsManager? _assetsManager;
    private string? _currentTempDirectory;
    private Border _loadingOverlay = null!;
    private Border _imageViewerOverlay = null!;
    private Image _zoomImage = null!;
    private ScaleTransform _zoomScale = null!;
    private TranslateTransform _zoomTranslate = null!;
    private readonly Dictionary<IPointer, Point> _zoomPointers = new();
    private readonly List<RotateTransform> _previewSpinnerRotations = new();
    private double _pinchStartDistance;
    private double _pinchStartScale = 1;
    private Point _lastPanPoint;
    private long _lastZoomTap;
    private bool _zoomGestureUsed;
    private BundleFileInstance? _currentBundle;
    private AssetsFileInstance? _currentAssets;
    private string _currentFileName = "";
    private BrowserPage _currentPage = BrowserPage.Home;
    private List<RowData> _currentRows = new();
    private List<AssetRow> _assetRows = new();
    private int _visibleRows;
    private bool _isRendering;
    private bool _isBusy;
    private global::Android.Media.MediaPlayer? _audioPlayer;
    private DispatcherTimer? _audioProgressTimer;
    private string? _audioTempPath;
    private string? _sessionResourcePath;

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
        _spinnerTimer.Tick += (_, _) => { _spinnerRotation.Angle = (_spinnerRotation.Angle + 18) % 360; foreach (var rotation in _previewSpinnerRotations) rotation.Angle = (rotation.Angle + 18) % 360; };

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
        _pageLayers = new Grid();
        _pageLayers.Children.Add(_scrollViewer);
        _loadingOverlay = new Border { Background = Brush.Parse("#D9000000"), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Child = _loadingIndicator };
        _loadingIndicator.HorizontalAlignment = HorizontalAlignment.Center;
        _loadingIndicator.VerticalAlignment = VerticalAlignment.Center;
        _pageLayers.Children.Add(_loadingOverlay);
        _zoomScale = new ScaleTransform(1, 1);
        _zoomTranslate = new TranslateTransform();
        _zoomImage = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, RenderTransformOrigin = RelativePoint.Center, RenderTransform = new TransformGroup { Children = { _zoomScale, _zoomTranslate } } };
        _zoomImage.PointerPressed += OnZoomPointerPressed;
        _zoomImage.PointerMoved += OnZoomPointerMoved;
        _zoomImage.PointerReleased += OnZoomPointerReleased;
        var closeViewer = new Button { Content = "✕", Width = 48, Height = 48, Margin = new Thickness(14), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Background = SurfaceHighBrush, Foreground = TextBrush, CornerRadius = new CornerRadius(24) };
        closeViewer.Click += (_, _) => CloseImageViewer();
        var viewerGrid = new Grid();
        viewerGrid.Children.Add(_zoomImage);
        viewerGrid.Children.Add(closeViewer);
        _imageViewerOverlay = new Border { Background = Brush.Parse("#FF000000"), IsVisible = false, Child = viewerGrid };
        _pageLayers.Children.Add(_imageViewerOverlay);
        Content = _pageLayers;
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
        if (topLevel is null || _isBusy) return;
        var selected = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Select an assets file and its resource files", AllowMultiple = true });
        if (selected.Count == 0) return;
        var file = selected.FirstOrDefault(x => x.Name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
            ?? selected.FirstOrDefault(x => !IsResourceFileName(x.Name));
        if (file is null) return;
        _isBusy = true;
        _sessionResourcePath = null;
        SetLoading(true, $"Copying {file.Name}...");
        _items.Children.Clear();
        _searchBox.IsVisible = false;
        _filterButton.IsVisible = false;
        _filterBox.IsVisible = false;
        _loadMoreButton.IsVisible = false;
        _searchBox.Text = "";
        SetFilterOptions(new[] { "All types" });
        string? tempDirectory = null;
        try
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "uabea-mobile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string? primaryPath = null;
            foreach (var selectedFile in selected)
            {
                string safeName = Path.GetFileName(selectedFile.Name);
                string destinationPath = Path.Combine(tempDirectory, safeName);
                if (File.Exists(destinationPath)) destinationPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + "_" + safeName);
                await using var source = await selectedFile.OpenReadAsync();
                await using var destination = File.Create(destinationPath);
                await source.CopyToAsync(destination);
                if (ReferenceEquals(selectedFile, file)) primaryPath = destinationPath;
            }
            if (primaryPath is null) throw new InvalidDataException("The selected assets file could not be copied.");
            SetLoading(true, $"Analyzing {file.Name}...");
            var result = await Task.Run(() => LoadFileInBackground(primaryPath, file.Name));
            _assetsManager?.UnloadAllAssetsFiles(true);
            if (_currentTempDirectory is not null) { try { Directory.Delete(_currentTempDirectory, true); } catch { } }
            _assetsManager = result.Manager;
            _currentTempDirectory = tempDirectory;
            tempDirectory = null;
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
            SetFilterOptions(result.Page == BrowserPage.Assets ? result.AssetRows.Select(row => row.TypeName) : result.Rows.Select(row => GetEntryKind(row.Title)));
            _searchBox.Watermark = result.Page == BrowserPage.Bundle ? "Search bundle entries..." : "Search by asset name or type...";
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
            if (tempDirectory is not null) { try { Directory.Delete(tempDirectory, true); } catch { } }
            _isBusy = false;
            SetLoading(false);
        }
    }

    private static bool IsResourceFileName(string name) =>
        name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase);

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
            var bundle = manager.LoadBundleFile(path, true);
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
        StopAudioPlayback();
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
        else if (typeId == (int)AssetClassID.AudioClip)
        {
            await ShowAudioClipPreviewAsync(manager, instance, asset);
        }
        else if (typeId == (int)AssetClassID.Font)
        {
            await ShowFontPreviewAsync(manager, instance, asset);
        }
        else if (typeId == (int)AssetClassID.Mesh)
        {
            await ShowMeshPreviewAsync(manager, instance, asset);
        }
    }

    private async Task ShowTexturePreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        var loadingCard = AddPreviewLoadingCard("Image preview", MaterialIconKind.Image);
        Avalonia.Media.Imaging.Bitmap? previewBitmap = null;
        string? error = null;
        int previewWidth = 0;
        int previewHeight = 0;
        string? textureResourceSource = null;
        bool needsExternalResource = false;

        try
        {
            var imageResult = await Task.Run(() =>
            {
                var field = manager.GetBaseField(instance, asset);
                if (field == null)
                    throw new InvalidDataException("Texture data could not be read.");
                var texture = TextureFile.ReadTextureFile(field);
                if (texture.m_Width <= 0 || texture.m_Height <= 0 ||
                    texture.m_Width > 8192 || texture.m_Height > 8192)
                    throw new InvalidDataException("Unsupported or invalid image dimensions.");

                byte[] data = texture.pictureData ?? Array.Empty<byte>();
                ulong streamOffset = ReadUnsignedField(field["m_StreamData"]["offset"]);
                ulong streamSize = ReadUnsignedField(field["m_StreamData"]["size"]);
                string streamPath = field["m_StreamData"]["path"]?.AsString ?? texture.m_StreamData.path ?? "";
                textureResourceSource = streamPath;
                if (streamSize > 0 && !string.IsNullOrWhiteSpace(streamPath))
                {
                    try
                    {
                        data = instance.parentBundle != null
                            ? ReadBundleResource(instance.parentBundle, streamPath, streamOffset, streamSize)
                            : ReadExternalResource(instance, streamPath, streamOffset, streamSize);
                    }
                    catch (FileNotFoundException)
                    {
                        needsExternalResource = instance.parentBundle == null;
                        throw;
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
                        // Apply the additional horizontal flip requested for the preview.
                        // The previous horizontal reversal is cancelled; vertical correction remains.
                        int sourceX = x;
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
                return (Png: output.ToArray(), Width: width, Height: height);
            });

            previewWidth = imageResult.Width;
            previewHeight = imageResult.Height;
            using var imageStream = new MemoryStream(imageResult.Png);
            previewBitmap = new Avalonia.Media.Imaging.Bitmap(imageStream);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally { }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            RemovePreviewLoadingCard(loadingCard);
            previewBitmap?.Dispose();
            return;
        }

        RemovePreviewLoadingCard(loadingCard);
        if (previewBitmap != null)
        {
            var image = new Image { Source = previewBitmap, Stretch = Stretch.Uniform, MaxHeight = 360 };
            image.Tapped += (_, _) => OpenImageViewer(previewBitmap);
            _items.Children.Add(CreatePreviewCard("Image preview", new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock { Text = $"{previewWidth}×{previewHeight} px", FontSize = 12, Foreground = TextBrush },
                    image
                }
            }, MaterialIconKind.Image));
        }
        else if (needsExternalResource && !string.IsNullOrWhiteSpace(textureResourceSource))
        {
            bool selected = await ChooseSessionResourceAsync(textureResourceSource, instance);
            if (selected && _currentAssets == instance && _currentPage == BrowserPage.AssetDetails)
            {
                await ShowTexturePreviewAsync(manager, instance, asset);
                return;
            }
            _items.Children.Add(CreatePreviewCard("Image preview", new TextBlock
            {
                Text = "Select the matching .resource file to preview this image.",
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap
            }, MaterialIconKind.Image));
        }
        else
        {
            _items.Children.Add(CreatePreviewCard("Image preview", new TextBlock
            {
                Text = "Preview unavailable: " + error,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap
            }, MaterialIconKind.Image));
        }
    }


    private async Task ShowAudioClipPreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        var loadingCard = AddPreviewLoadingCard("Audio preview", MaterialIconKind.MusicNote);
        string? tempAudioPath = null;
        string? error = null;
        string? audioName = null;
        string? audioResourceSource = null;
        bool needsExternalResource = false;
        try
        {
            var audioFile = await Task.Run(() =>
            {
                var field = manager.GetBaseField(instance, asset);
                if (field == null)
                    throw new InvalidDataException("AudioClip data could not be read.");

                audioName = field["m_Name"]?.AsString;
                // Match UABEA's AudioClip plugin field access. The Unity type tree
                // exposes these streamed-resource members as flattened field names.
                var sourceField = field["m_Resource.m_Source"];
                string source = sourceField.IsDummy ? "" : sourceField.AsString;
                audioResourceSource = source;
                ulong offset = ReadUnsignedField(field["m_Resource.m_Offset"]);
                ulong size = ReadUnsignedField(field["m_Resource.m_Size"]);
                if (string.IsNullOrWhiteSpace(source) || size == 0 || size > int.MaxValue)
                    throw new InvalidDataException("This AudioClip has no external FMOD resource. Embedded audio data is not supported by this preview yet.");

                // Follow UABEA's AudioClip plugin lookup order: try the parent
                // bundle first, then fall back to the external resource beside the
                // extracted assets file. Some bundles store AudioClip payloads as
                // sibling .resource/.resS files rather than directory entries.
                byte[] fsbData;
                if (instance.parentBundle != null)
                {
                    try
                    {
                        fsbData = ReadBundleResource(instance.parentBundle, source, offset, size);
                    }
                    catch (FileNotFoundException)
                    {
                        fsbData = ReadExternalResource(instance, source, offset, size);
                    }
                }
                else
                {
                    fsbData = ReadExternalResource(instance, source, offset, size);
                }

                if (fsbData.Length == 0)
                    throw new InvalidDataException("The AudioClip resource is empty.");
                if (!FsbLoader.TryLoadFsbFromByteArray(fsbData, out FmodSoundBank bank) ||
                    bank.Samples == null || bank.Samples.Count == 0)
                    throw new InvalidDataException("This AudioClip's FMOD sound bank could not be decoded.");

                bank.Samples[0].RebuildAsStandardFileFormat(out byte[] sampleData, out string sampleExtension);
                if (sampleData == null || sampleData.Length == 0)
                    throw new InvalidDataException("The audio decoder returned an empty sample.");

                sampleExtension = (sampleExtension ?? "").Trim().TrimStart('.').ToLowerInvariant();
                if (sampleExtension == "wav")
                    FixPreviewWav(ref sampleData);
                if (sampleExtension is not ("wav" or "ogg" or "mp3" or "aac" or "m4a"))
                    throw new NotSupportedException("This AudioClip uses a format that Android cannot preview directly (" +
                        (string.IsNullOrEmpty(sampleExtension) ? "unknown" : sampleExtension) + ").");

                return (Data: sampleData, Extension: sampleExtension);
            });

            tempAudioPath = Path.Combine(Path.GetTempPath(),
                "uabea-audio-" + Guid.NewGuid().ToString("N") + "." + audioFile.Extension);
            await File.WriteAllBytesAsync(tempAudioPath, audioFile.Data);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            needsExternalResource = ex is FileNotFoundException &&
                ex.Message.Contains("external resource file was not found", StringComparison.OrdinalIgnoreCase);
        }
        finally { }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            RemovePreviewLoadingCard(loadingCard);
            if (!string.IsNullOrEmpty(tempAudioPath))
            {
                try { File.Delete(tempAudioPath); } catch { }
            }
            return;
        }

        RemovePreviewLoadingCard(loadingCard);
        if (string.IsNullOrEmpty(tempAudioPath))
        {
            // If the sidecar resource is missing, ask for it immediately on the
            // first preview attempt instead of presenting an error first.
            if (needsExternalResource && !string.IsNullOrWhiteSpace(audioResourceSource))
            {
                bool selected = await ChooseAudioResourceAsync(
                    audioResourceSource, instance, asset);

                if (!selected && _currentAssets == instance &&
                    _currentPage == BrowserPage.AssetDetails)
                {
                    var promptContent = new StackPanel { Spacing = 10 };
                    promptContent.Children.Add(new TextBlock
                    {
                        Text = "Select the matching .resource file to preview this AudioClip.",
                        Foreground = TextBrush,
                        TextWrapping = TextWrapping.Wrap
                    });

                    var chooseResourceButton = new Button
                    {
                        Content = "Select .resource file",
                        MinHeight = 44,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Background = PrimaryBrush,
                        Foreground = OnPrimaryBrush,
                        CornerRadius = new CornerRadius(13),
                        FontWeight = FontWeight.SemiBold
                    };
                    var promptCard = CreatePreviewCard("Audio preview", promptContent, MaterialIconKind.MusicNote);
                    chooseResourceButton.Click += async (_, _) =>
                    {
                        chooseResourceButton.IsEnabled = false;
                        _items.Children.Remove(promptCard);
                        bool retried = await ChooseAudioResourceAsync(
                            audioResourceSource, instance, asset);
                        if (!retried && _currentAssets == instance &&
                            _currentPage == BrowserPage.AssetDetails)
                        {
                            _items.Children.Add(promptCard);
                            chooseResourceButton.IsEnabled = true;
                        }
                    };
                    promptContent.Children.Add(chooseResourceButton);
                    _items.Children.Add(promptCard);
                }
                return;
            }

            var errorContent = new StackPanel { Spacing = 10 };
            errorContent.Children.Add(new TextBlock
            {
                Text = "Preview unavailable: " + error,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap
            });
            _items.Children.Add(CreatePreviewCard("Audio preview", errorContent, MaterialIconKind.MusicNote));
            return;
        }

        _audioTempPath = tempAudioPath;
        BuildAudioPlayerCard(tempAudioPath, audioName ?? "AudioClip");
    }

    private async Task<bool> ChooseSessionResourceAsync(string source, AssetsFileInstance instance)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return false;

        var selected = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select the matching .resource file",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Unity resource files")
                    {
                        Patterns = new[] { "*.resource", "*.resources", "*.resS", "*.ress" }
                    }
                }
            });

        if (selected.Count == 0)
            return false;

        string root = GetExternalResourceDirectory(instance);
        Directory.CreateDirectory(root);
        string expectedName = Path.GetFileName(NormalizeResourcePath(source));
        if (string.IsNullOrWhiteSpace(expectedName))
            expectedName = selected[0].Name;
        string destination = Path.Combine(root, expectedName);

        try
        {
            await using var input = await selected[0].OpenReadAsync();
            await using var output = File.Create(destination);
            await input.CopyToAsync(output);
            _sessionResourcePath = destination;
            return true;
        }
        catch (Exception ex)
        {
            _items.Children.Add(CreatePreviewCard("Resource file", new TextBlock
            {
                Text = "Could not copy the selected resource: " + ex.Message,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap
            }, MaterialIconKind.MusicNote));
            return false;
        }
    }

    private async Task<bool> ChooseAudioResourceAsync(
        string source, AssetsFileInstance instance, AssetFileInfo asset)
    {
        if (!await ChooseSessionResourceAsync(source, instance))
            return false;

        await ShowAudioClipPreviewAsync(_assetsManager!, instance, asset);
        return true;
    }

    private void BuildAudioPlayerCard(string audioPath, string audioName)
    {
        var playButton = new Button
        {
            Content = "▶ Play",
            MinHeight = 44,
            Background = PrimaryBrush,
            Foreground = OnPrimaryBrush,
            CornerRadius = new CornerRadius(13),
            FontWeight = FontWeight.SemiBold
        };
        var loopCheck = new Avalonia.Controls.CheckBox
        {
            Content = "Loop",
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        var volumeLabel = new TextBlock
        {
            Text = "Volume 100%",
            FontSize = 12,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        var volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 1,
            MinWidth = 120
        };
        var positionSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            MinWidth = 160
        };
        var timeLabel = new TextBlock
        {
            Text = "00:00 / 00:00",
            FontSize = 12,
            Foreground = MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var playerStatus = new TextBlock
        {
            Text = "Ready to play",
            FontSize = 12,
            Foreground = MutedBrush
        };

        global::Android.Media.MediaPlayer? player = null;
        bool updatingPosition = false;
        bool isPrepared = false;

        void UpdateTime()
        {
            if (player == null || !isPrepared) return;
            int duration = Math.Max(0, player.Duration);
            int current = Math.Clamp(player.CurrentPosition, 0, Math.Max(1, duration));
            updatingPosition = true;
            positionSlider.Maximum = Math.Max(1, duration);
            positionSlider.Value = current;
            updatingPosition = false;
            timeLabel.Text = $"{FormatAudioTime(current)} / {FormatAudioTime(duration)}";
        }

        try
        {
            player = new global::Android.Media.MediaPlayer();
            player.SetDataSource(audioPath);
            player.Prepare();
            isPrepared = true;
            _audioPlayer = player;
            player.Looping = loopCheck.IsChecked == true;
            player.SetVolume(1f, 1f);
            UpdateTime();
            playerStatus.Text = "Ready";
        }
        catch (Exception ex)
        {
            player?.Release();
            player?.Dispose();
            _audioPlayer = null;
            playerStatus.Text = "Could not initialize audio: " + ex.Message;
            playButton.IsEnabled = false;
        }

        playButton.Click += (_, _) =>
        {
            if (player == null || !isPrepared) return;
            try
            {
                if (player.IsPlaying)
                {
                    player.Pause();
                    playButton.Content = "▶ Play";
                    playerStatus.Text = "Paused";
                }
                else
                {
                    if (player.CurrentPosition >= player.Duration)
                        player.SeekTo(0);
                    player.Start();
                    playButton.Content = "Ⅱ Pause";
                    playerStatus.Text = "Playing";
                }
                UpdateTime();
            }
            catch (Exception ex)
            {
                playerStatus.Text = "Playback error: " + ex.Message;
            }
        };

        loopCheck.IsCheckedChanged += (_, _) =>
        {
            if (player != null)
                player.Looping = loopCheck.IsChecked == true;
        };

        volumeSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty)
            {
                float volume = (float)Math.Clamp(volumeSlider.Value, 0, 1);
                volumeLabel.Text = $"Volume {(int)Math.Round(volume * 100)}%";
                player?.SetVolume(volume, volume);
            }
        };

        positionSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty && !updatingPosition && player != null && isPrepared)
            {
                int target = (int)Math.Clamp(positionSlider.Value, 0, Math.Max(0, player.Duration));
                try { player.SeekTo(target); } catch { }
                timeLabel.Text = $"{FormatAudioTime(target)} / {FormatAudioTime(Math.Max(0, player.Duration))}";
            }
        };

        if (player != null)
        {
            player.Completion += (_, _) =>
            {
                playButton.Content = "▶ Play";
                playerStatus.Text = loopCheck.IsChecked == true ? "Looping" : "Finished";
                UpdateTime();
            };
            _audioProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _audioProgressTimer.Tick += (_, _) =>
            {
                if (player.IsPlaying)
                    UpdateTime();
            };
            _audioProgressTimer.Start();
        }

        var playerContent = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                playerStatus,
                playButton,
                positionSlider,
                timeLabel,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { loopCheck }
                },
                new TextBlock { Text = "Volume", FontSize = 12, Foreground = MutedBrush },
                volumeSlider,
                volumeLabel
            }
        };
        _items.Children.Add(CreatePreviewCard(audioName, playerContent, MaterialIconKind.MusicNote));
    }

    private void StopAudioPlayback()
    {
        try { _audioProgressTimer?.Stop(); } catch { }
        _audioProgressTimer = null;
        try
        {
            if (_audioPlayer != null)
            {
                if (_audioPlayer.IsPlaying) _audioPlayer.Stop();
                _audioPlayer.Release();
                _audioPlayer.Dispose();
            }
        }
        catch { }
        _audioPlayer = null;
        if (!string.IsNullOrEmpty(_audioTempPath))
        {
            try { File.Delete(_audioTempPath); } catch { }
            _audioTempPath = null;
        }
    }

    private static string FormatAudioTime(int milliseconds)
    {
        int seconds = Math.Max(0, milliseconds / 1000);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static void FixPreviewWav(ref byte[] wavData)
    {
        if (wavData.Length < 44 ||
            System.Text.Encoding.ASCII.GetString(wavData, 0, 4) != "RIFF" ||
            System.Text.Encoding.ASCII.GetString(wavData, 8, 4) != "WAVE")
            return;

        int originalLength = wavData.Length;
        for (int i = 36; i < originalLength - 2; i++)
            wavData[i] = wavData[i + 2];
        Array.Resize(ref wavData, originalLength - 2);
        BitConverter.GetBytes(wavData.Length - 8).CopyTo(wavData, 4);
        BitConverter.GetBytes(16).CopyTo(wavData, 16);
        BitConverter.GetBytes(wavData.Length - 44).CopyTo(wavData, 40);
    }

    private async Task ShowMeshPreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        var loadingCard = AddPreviewLoadingCard("3D Mesh preview", MaterialIconKind.Image);
        MeshPreviewData? meshData = null;
        string? error = null;
        bool needsExternalResource = false;
        string streamSource = "";

        try
        {
            meshData = await Task.Run(() =>
            {
                var field = manager.GetBaseField(instance, asset);
                if (field == null)
                    throw new InvalidDataException("Mesh data could not be read.");

                MeshStreamInfo stream = MeshPreviewDataReader.ReadStreamInfo(field);
                streamSource = stream.Path;
                byte[]? streamedData = null;

                if (stream.Size > 0 && !string.IsNullOrWhiteSpace(stream.Path))
                {
                    try
                    {
                        streamedData = instance.parentBundle != null
                            ? ReadBundleResource(instance.parentBundle, stream.Path, stream.Offset, stream.Size)
                            : ReadExternalResource(instance, stream.Path, stream.Offset, stream.Size);
                    }
                    catch (FileNotFoundException)
                    {
                        needsExternalResource = instance.parentBundle == null;
                        throw;
                    }
                }

                return MeshPreviewDataReader.Read(field, streamedData);
            });
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            RemovePreviewLoadingCard(loadingCard);
            return;
        }

        RemovePreviewLoadingCard(loadingCard);

        if (meshData == null && needsExternalResource && !string.IsNullOrWhiteSpace(streamSource))
        {
            bool selected = await ChooseSessionResourceAsync(streamSource, instance);
            if (selected && _currentAssets == instance && _currentPage == BrowserPage.AssetDetails)
            {
                await ShowMeshPreviewAsync(manager, instance, asset);
                return;
            }

            _items.Children.Add(CreatePreviewCard("3D Mesh preview", new TextBlock
            {
                Text = "This Mesh uses external vertex data. Select its matching .resource or .resS file to preview it.",
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap
            }, MaterialIconKind.Image));
            return;
        }

        if (meshData == null)
        {
            _items.Children.Add(CreatePreviewCard("3D Mesh preview", new TextBlock
            {
                Text = "Preview unavailable: " + error,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap
            }, MaterialIconKind.Image));
            return;
        }

        // Inline preview is intentionally passive; camera controls exist only in fullscreen.
        var viewer = new MeshPreviewControl(meshData.Vertices, meshData.Indices, interactive: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var fullscreenButton = new Button
        {
            Content = CreateIcon(MaterialIconKind.ArrowExpand, 18, TextBrush),
            Background = SurfaceHighBrush,
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(10),
            Width = 42,
            Height = 40,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        fullscreenButton.Click += (_, _) =>
        {
            try
            {
                var largeViewer = new MeshPreviewControl(meshData.Vertices, meshData.Indices, interactive: true)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 0,
                    Height = double.NaN
                };

                var overlay = new Border
                {
                    Background = BackgroundBrush,
                    Padding = new Thickness(12),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var closeButton = new Button
                {
                    Content = "Close",
                    Background = SurfaceHighBrush,
                    Foreground = TextBrush,
                    CornerRadius = new CornerRadius(10),
                    MinHeight = 42,
                    MinWidth = 76
                };
                var resetLarge = new Button
                {
                    Content = "Reset camera",
                    Background = SurfaceHighBrush,
                    Foreground = TextBrush,
                    CornerRadius = new CornerRadius(10),
                    MinHeight = 42
                };
                var wireframeLarge = new Button
                {
                    Content = "Wireframe",
                    Background = SurfaceHighBrush,
                    Foreground = TextBrush,
                    CornerRadius = new CornerRadius(10),
                    MinHeight = 42
                };

                closeButton.Click += (_, _) =>
                {
                    largeViewer.Dispose();
                    _pageLayers.Children.Remove(overlay);
                };
                resetLarge.Click += (_, _) => largeViewer.ResetView();
                wireframeLarge.Click += (_, _) => largeViewer.ToggleWireframe();

                var layout = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto")
                };
                var header = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"3D Mesh preview • {meshData.Vertices.Length:N0} vertices • {meshData.Indices.Length / 3:N0} triangles",
                            Foreground = MutedBrush,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        },
                        closeButton
                    }
                };
                Grid.SetColumn(closeButton, 1);
                layout.Children.Add(header);
                layout.Children.Add(largeViewer);
                Grid.SetRow(largeViewer, 1);

                var footer = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 8,
                    Children =
                    {
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Spacing = 8,
                            Children = { resetLarge, wireframeLarge }
                        },
                        new TextBlock
                        {
                            Text = "Drag to rotate • Pinch to zoom • Two-finger drag to orbit",
                            Foreground = MutedBrush,
                            FontSize = 11,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                };
                layout.Children.Add(footer);
                Grid.SetRow(footer, 2);

                overlay.Child = layout;
                _pageLayers.Children.Add(overlay);
            }
            catch (Exception ex)
            {
                _status.Text = "Could not open full-screen Mesh preview: " + ex.Message;
            }
        };

        var topBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = $"{meshData.Vertices.Length:N0} vertices • {meshData.Indices.Length / 3:N0} triangles",
                    FontSize = 12,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                },
                fullscreenButton
            }
        };

        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                topBar,
                viewer,
                new TextBlock
                {
                    Text = "Open full-screen to rotate and zoom the model",
                    FontSize = 11,
                    Foreground = MutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

        _items.Children.Add(CreatePreviewCard("3D Mesh preview", content, MaterialIconKind.Image));
    }

    private async Task ShowFontPreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        var loadingCard = AddPreviewLoadingCard("Font preview", MaterialIconKind.FormatFont);
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
                // Follow UABEA FontPlugin: change the m_FontData array element
                // type on the template, then deserialize with that SAME template.
                // Calling manager.GetBaseField afterwards would create a fresh
                // template and lose the ByteArray override on uncached managers.
                var template = manager.GetTemplateBaseField(instance, asset);
                if (template == null)
                    throw new InvalidDataException("Font type information could not be read.");

                var fontDataTemplate = template.Children.FirstOrDefault(x => x.Name == "m_FontData");
                if (fontDataTemplate == null || fontDataTemplate.Children.Count == 0)
                    throw new InvalidDataException("This Unity Font has no m_FontData field.");

                fontDataTemplate.Children[0].ValueType = AssetValueType.ByteArray;

                AssetTypeValueField field;
                var reader = instance.file.Reader;
                lock (reader)
                {
                    long absolutePosition = asset.GetAbsoluteByteStart(instance.file);
                    field = template.MakeValue(reader, absolutePosition, null);
                }

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
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            RemovePreviewLoadingCard(loadingCard);
            previewBitmap?.Dispose();
            return;
        }

        RemovePreviewLoadingCard(loadingCard);
        Control content = previewBitmap != null
            ? new Image { Source = previewBitmap, Stretch = Stretch.Uniform, MaxHeight = 220 }
            : new TextBlock { Text = "Preview unavailable: " + error, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap };
        _items.Children.Add(CreatePreviewCard("Font preview • " + (fontName ?? "Unnamed font"), content, MaterialIconKind.FormatFont));
    }

    private async Task ShowTextAssetPreviewAsync(
        AssetsManager manager, AssetsFileInstance instance, AssetFileInfo asset)
    {
        var loadingCard = AddPreviewLoadingCard("Text preview", MaterialIconKind.FileDocumentOutline);

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
        }

        if (_currentAssets != instance || _currentPage != BrowserPage.AssetDetails)
        {
            RemovePreviewLoadingCard(loadingCard);
            return;
        }

        RemovePreviewLoadingCard(loadingCard);
        var textContent = new ScrollViewer
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
        };
        _items.Children.Add(CreatePreviewCard("TextAsset preview", textContent, MaterialIconKind.FileDocumentOutline));
    }

    private async void GoBack()
    {
        if (_currentPage == BrowserPage.Home || _isBusy)
            return;

        SetLoading(true, "Going back...");
        await Task.Yield();

        if (_currentPage == BrowserPage.AssetDetails)
            StopAudioPlayback();
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
        SetLoading(false);
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
        _loadingOverlay.IsVisible = loading;
        if (loading || _previewSpinnerRotations.Count > 0)
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

    private static string NormalizeResourcePath(string path)
    {
        path = path.Replace('\\', '/');
        if (path.StartsWith("archive:/", StringComparison.OrdinalIgnoreCase))
            path = path.Substring("archive:/".Length);
        while (path.StartsWith("/", StringComparison.Ordinal)) path = path.Substring(1);
        return path;
    }

    private static ulong ReadUnsignedField(AssetTypeValueField field)
    {
        if (field == null || field.IsDummy || field.Value == null)
            return 0;

        return field.Value.ValueType switch
        {
            AssetValueType.UInt8 => field.AsByte,
            AssetValueType.UInt16 => field.AsUShort,
            AssetValueType.UInt32 => field.AsUInt,
            AssetValueType.UInt64 => field.AsULong,
            AssetValueType.Int8 => checked((ulong)field.AsSByte),
            AssetValueType.Int16 => checked((ulong)field.AsShort),
            AssetValueType.Int32 => checked((ulong)field.AsInt),
            AssetValueType.Int64 => checked((ulong)field.AsLong),
            _ => throw new InvalidDataException($"Unexpected numeric field type: {field.Value.ValueType}.")
        };
    }

    private static byte[] ReadBundleResource(BundleFileInstance bundleInstance, string source, ulong offset, ulong size)
    {
        if (size == 0 || size > int.MaxValue)
            throw new InvalidDataException("The AudioClip resource size is invalid.");

        var entries = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;
        string wanted = NormalizeResourcePath(source);
        string basename = Path.GetFileName(wanted);

        // UABEA stores the AudioClip resource path as archive:/<entry>. On Android
        // some bundles expose a different directory prefix, so compare both the
        // normalized full path and the basename, case-insensitively.
        var exact = entries.Where(x =>
            string.Equals(NormalizeResourcePath(x.Name), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        var suffix = entries.Where(x =>
            NormalizeResourcePath(x.Name).EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        var matches = exact.Count > 0 ? exact : suffix;
        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(basename))
        {
            matches = entries.Where(x =>
                string.Equals(Path.GetFileName(NormalizeResourcePath(x.Name)), basename,
                    StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Some Unity bundles have a stale/virtual m_Source name while keeping
        // the actual stream as the only non-serialized resource entry. Only use
        // this fallback when the requested byte range fits exactly one resource.
        if (matches.Count == 0)
        {
            var possible = entries.Where(x =>
            {
                string name = x.Name ?? "";
                bool serialized = (x.Flags & 0x04) != 0;
                bool resourceLike = name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".ress", StringComparison.OrdinalIgnoreCase);
                return !serialized && resourceLike &&
                    offset <= (ulong)x.DecompressedSize &&
                    size <= (ulong)x.DecompressedSize - offset;
            }).ToList();

            if (possible.Count == 1)
                matches = possible;
        }

        if (matches.Count != 1)
            throw new FileNotFoundException(matches.Count == 0
                ? $"AudioClip resource '{source}' was not found in the AssetBundle. Bundle entries: {entries.Length}."
                : $"AudioClip resource '{source}' matches multiple AssetBundle entries.");

        var entry = matches[0];
        if (offset > (ulong)entry.DecompressedSize || size > (ulong)entry.DecompressedSize - offset)
            throw new InvalidDataException($"AudioClip byte range ({offset}, {size}) exceeds bundle entry '{entry.Name}' ({entry.DecompressedSize} bytes).");

        // Use a bounded stream instead of mutating the shared bundle reader's
        // cursor. This is the same stream strategy used by bundle-entry export.
        var reader = bundleInstance.file.DataReader;
        lock (reader)
        {
            using var segment = new SegmentStream(reader.BaseStream, entry.Offset + (long)offset, (long)size);
            using var output = new MemoryStream(checked((int)size));
            segment.CopyTo(output);
            byte[] data = output.ToArray();
            if ((ulong)data.Length != size)
                throw new EndOfStreamException($"Bundle entry '{entry.Name}' ended before the requested AudioClip data was read.");
            return data;
        }
    }

    private static string GetExternalResourceDirectory(AssetsFileInstance instance)
    {
        string root = Path.GetDirectoryName(instance.path) ?? "";
        if (instance.parentBundle != null)
            root = Path.GetDirectoryName(root) ?? root;
        return root;
    }

    private byte[] ReadExternalResource(AssetsFileInstance instance, string source, ulong offset, ulong size)
    {
        string root = GetExternalResourceDirectory(instance);
        string normalized = NormalizeResourcePath(source);
        string relative = normalized.Replace('/', Path.DirectorySeparatorChar);
        var candidates = new List<string>();
        if (Path.IsPathRooted(source)) candidates.Add(source);
        candidates.Add(Path.Combine(root, relative));
        candidates.Add(Path.Combine(root, Path.GetFileName(normalized)));
        string? fullPath = candidates.FirstOrDefault(File.Exists);
        // A resource file selected for one preview belongs to the whole open
        // assets session. Reuse it for other AudioClip/Texture2D stream paths
        // when the requested byte range fits, even if Unity stored a different
        // logical source name in the asset metadata.
        if (fullPath == null && !string.IsNullOrWhiteSpace(_sessionResourcePath) &&
            File.Exists(_sessionResourcePath))
        {
            var sessionInfo = new FileInfo(_sessionResourcePath);
            if (offset <= (ulong)sessionInfo.Length && size <= (ulong)sessionInfo.Length - offset)
                fullPath = _sessionResourcePath;
        }
        if (fullPath == null && Directory.Exists(root))
        {
            var sameName = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(x => string.Equals(Path.GetFileName(x), Path.GetFileName(normalized), StringComparison.OrdinalIgnoreCase)).ToList();
            if (sameName.Count == 1) fullPath = sameName[0];
        }
        if (fullPath == null)
            throw new FileNotFoundException("The external resource file was not found. If it is a separate .resource/.resources/.resS file, select it together with the .assets file.");
        using var stream = File.OpenRead(fullPath);
        if (offset > (ulong)stream.Length || size > (ulong)stream.Length - offset)
            throw new InvalidDataException("The requested resource range is outside the external resource file.");
        stream.Position = (long)offset;
        byte[] data = new byte[checked((int)size)];
        int read = 0;
        while (read < data.Length)
        {
            int count = stream.Read(data, read, data.Length - read);
            if (count == 0) throw new EndOfStreamException("The external resource file ended unexpectedly.");
            read += count;
        }
        return data;
    }

    private Border CreatePreviewCard(string title, Control content, MaterialIconKind iconKind = MaterialIconKind.Image)
    {
        var panel = new StackPanel { Spacing = 9, VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(CreateIcon(iconKind, 18, PrimaryBrush));
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Foreground = MutedBrush,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(titleRow);
        panel.Children.Add(content);
        var card = new Border
        {
            Background = SurfaceContainerBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = panel
        };
        FadeIn(card);
        return card;
    }

    private static void FadeIn(Control control)
    {
        control.Opacity = 0;
        int frame = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(24) };
        timer.Tick += (_, _) =>
        {
            frame++;
            control.Opacity = Math.Min(1, frame / 5.0);
            if (frame >= 5)
                timer.Stop();
        };
        timer.Start();
    }

    private Border AddPreviewLoadingCard(string title, MaterialIconKind iconKind)
    {
        var rotation = new RotateTransform();
        _previewSpinnerRotations.Add(rotation);
        var spinner = new TextBlock { Text = "◌", FontSize = 34, Foreground = PrimaryBrush, HorizontalAlignment = HorizontalAlignment.Center, RenderTransform = rotation };
        var label = new TextBlock { Text = "Loading preview...", FontSize = 12, Foreground = MutedBrush, HorizontalAlignment = HorizontalAlignment.Center };
        var content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(spinner); content.Children.Add(label);
        var card = CreatePreviewCard(title, content, iconKind);
        card.Tag = rotation;
        _items.Children.Add(card);
        if (!_spinnerTimer.IsEnabled) _spinnerTimer.Start();
        return card;
    }

    private void RemovePreviewLoadingCard(Border card)
    {
        if (card.Tag is RotateTransform rotation) _previewSpinnerRotations.Remove(rotation);
        _items.Children.Remove(card);
        if (!_loadingOverlay.IsVisible && _previewSpinnerRotations.Count == 0) _spinnerTimer.Stop();
    }

    private void OpenImageViewer(Avalonia.Media.Imaging.Bitmap bitmap)
    {
        _zoomImage.Source = bitmap;
        _zoomScale.ScaleX = _zoomScale.ScaleY = 1;
        _zoomTranslate.X = _zoomTranslate.Y = 0;
        _zoomPointers.Clear();
        _lastZoomTap = 0;
        _imageViewerOverlay.IsVisible = true;
    }

    private void CloseImageViewer()
    {
        _imageViewerOverlay.IsVisible = false;
        _zoomImage.Source = null;
        _zoomPointers.Clear();
        _zoomScale.ScaleX = _zoomScale.ScaleY = 1;
        _zoomTranslate.X = _zoomTranslate.Y = 0;
    }

    private void OnZoomPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Point pointerPosition = e.GetPosition(_imageViewerOverlay);
        _zoomPointers[e.Pointer] = pointerPosition;
        e.Pointer.Capture(_zoomImage);
        _zoomGestureUsed = false;
        if (_zoomPointers.Count == 1) _lastPanPoint = pointerPosition;
        else if (_zoomPointers.Count == 2)
        {
            var points = _zoomPointers.Values.ToArray();
            _pinchStartDistance = Distance(points[0], points[1]);
            _pinchStartScale = _zoomScale.ScaleX;
            _zoomGestureUsed = true;
        }
    }

    private void OnZoomPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_zoomPointers.ContainsKey(e.Pointer)) return;
        // Measure gestures in the fixed viewer coordinate space. Measuring
        // against the transformed image makes pan speed drift as zoom changes.
        Point current = e.GetPosition(_imageViewerOverlay);
        _zoomPointers[e.Pointer] = current;
        if (_zoomPointers.Count >= 2)
        {
            var points = _zoomPointers.Values.Take(2).ToArray();
            double distance = Distance(points[0], points[1]);
            if (_pinchStartDistance > 0)
            {
                double scale = Math.Clamp(_pinchStartScale * distance / _pinchStartDistance, 1, 6);
                _zoomScale.ScaleX = _zoomScale.ScaleY = scale;
            }
            _zoomGestureUsed = true;
        }
        else if (_zoomScale.ScaleX > 1)
        {
            _zoomTranslate.X += current.X - _lastPanPoint.X;
            _zoomTranslate.Y += current.Y - _lastPanPoint.Y;
        }
        _lastPanPoint = current;
    }

    private void OnZoomPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        bool single = _zoomPointers.Count == 1;
        _zoomPointers.Remove(e.Pointer);
        e.Pointer.Capture(null);
        if (single && !_zoomGestureUsed)
        {
            long now = Environment.TickCount64;
            if (now - _lastZoomTap <= 320)
            {
                double next = _zoomScale.ScaleX >= 5.5 ? 1 : Math.Min(6, Math.Max(2, _zoomScale.ScaleX * 2));
                _zoomScale.ScaleX = _zoomScale.ScaleY = next;
                if (next == 1) _zoomTranslate.X = _zoomTranslate.Y = 0;
                _lastZoomTap = 0;
            }
            else _lastZoomTap = now;
        }
        if (_zoomPointers.Count < 2) _pinchStartDistance = 0;
        if (_zoomPointers.Count == 0) _zoomGestureUsed = false;
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X; double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

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
