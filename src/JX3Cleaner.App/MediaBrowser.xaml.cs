using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using JX3Cleaner.App.Interop;
using JX3Cleaner.App.ViewModels;
using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.App;

/// <summary>
/// 截图 / 幻境云图页：月份导航 + 缩略图网格（行级虚拟化）+ 直接勾选清理。
/// 不内联播放任何视频：视频只显示首帧与「▶ 视频」角标，双击交给系统默认程序。
/// </summary>
public partial class MediaBrowser : UserControl
{
    /// <summary>一格宽度：卡片 176 + 左右各 6 外边距。必须与 XAML 里的卡片尺寸一致。</summary>
    private const double CellWidth = 188;

    /// <summary>可视范围上下各保留多少行不释放缩略图。</summary>
    private const int TrimBuffer = 40;

    private static readonly HashSet<string> MediaExtensions =
        new(CategoryCatalog.DefaultMedia, StringComparer.OrdinalIgnoreCase);

    private static readonly Brush PrimaryButtonBg = Freeze("#1F1F1F");
    private static readonly Brush DangerButtonBg = Freeze("#C62828");
    private static readonly Brush PrimaryText = Freeze("#1F1F1F");
    private static readonly Brush DangerText = Freeze("#C62828");

    /// <summary>缩略图解码并发上限：一次只压 3 个，避免首屏把 CPU 打满。</summary>
    private static readonly SemaphoreSlim ThumbGate = new(3);

    private readonly ObservableCollection<MediaRowVm> _rows = [];
    private readonly ObservableCollection<MediaMonthVm> _months = [];
    private readonly List<MediaItemVm> _all = [];
    private readonly DispatcherTimer _debounce;

    private FileSystemWatcher? _watcher;
    private ScrollViewer? _scroll;
    private ClientRoot? _root;
    private string? _dir;
    private CleanCategory _category = CleanCategory.Screenshot;

    private int _columns = -1;
    private int _token;
    private int _lastTrimRow = int.MinValue;
    private bool _suppressScrollTo;
    private bool _pendingScrollTop;

    private MediaItemVm? _pendingToggle;
    private bool _pendingPrev;

    private bool _suppressModeEvent;

    public MediaBrowser()
    {
        InitializeComponent();

        RowHost.ItemsSource = _rows;
        MonthList.ItemsSource = _months;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Reload(preserve: true); };

        Loaded += (_, _) =>
        {
            ApplyModeVisual();
            RebuildRows(force: true);
            HookScroll();
        };
        Unloaded += (_, _) => StopWatcher();
    }

    /// <summary>图片页要把选中的媒体交回主窗口走统一清理流程。</summary>
    public event Action<IReadOnlyList<ScanNode>, CleanMode>? CleanRequested;

    /// <summary>用户在图片页切换清理方式时通知主窗口，让两页保持一致。</summary>
    public event Action<CleanMode>? ModeChanged;

    /// <summary>当前清理方式。</summary>
    public CleanMode CurrentMode =>
        MediaDeleteRadio?.IsChecked == true ? CleanMode.DirectDelete : CleanMode.RecycleBin;

    /// <summary>外部（设置 / 数据清理页）设置清理方式，不反向触发 <see cref="ModeChanged"/>。</summary>
    public void SetMode(CleanMode mode)
    {
        if (MediaRecycleRadio is null || MediaDeleteRadio is null) return;

        _suppressModeEvent = true;
        try
        {
            MediaRecycleRadio.IsChecked = mode == CleanMode.RecycleBin;
            MediaDeleteRadio.IsChecked = mode == CleanMode.DirectDelete;
        }
        finally
        {
            _suppressModeEvent = false;
        }

        ApplyModeVisual();
    }

    /// <summary>诊断用：当前行总数（含月份标题行）。</summary>
    public int RowCount => _rows.Count;

    /// <summary>诊断用：累计实体化过的卡片数，用来验证虚拟化是否真的没全量创建。</summary>
    public int RealizedCardCount { get; private set; }

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // ---------------- 挂载客户端目录 ----------------

    /// <summary>扫描 / 校验成功后由主窗口调用。</summary>
    public void Attach(ClientRoot root)
    {
        var rootChanged = _root is null ||
                          !string.Equals(_root.ZhcnDir, root.ZhcnDir, StringComparison.OrdinalIgnoreCase);

        _root = root;

        if (rootChanged)
        {
            _dir = null;
            _category = ShotTab.IsChecked == true ? CleanCategory.Screenshot : CleanCategory.Dcim;
        }

        if (_dir is null) SelectFolder(CategoryDir(_category));
        else Reload(preserve: true);
    }

    /// <summary>清理完成后刷新当前目录（已删除的文件会自然消失）。</summary>
    public void ReloadAfterClean() => Reload(preserve: true);

    /// <summary>按当前宽度重排行（窗口尺寸变化后由 SizeChanged 自动调用，这里供调试用）。</summary>
    public void Refit() => RebuildRows(force: true);

    /// <summary>调试用：模拟点击左侧第 index 个月（配合 --shot --month N 做回归截图）。</summary>
    public void JumpToMonth(int index)
    {
        if (index < 0 || index >= _months.Count) return;
        MonthList.SelectedIndex = index;
        ScrollToRow(_months[index].RowIndex);
    }

    private string CategoryDir(CleanCategory id) =>
        Path.Combine(_root!.ZhcnDir, CategoryCatalog.Get(id).RelativePath);

    // ---------------- 目录 / 分类切换 ----------------

    private void ShotTab_Click(object sender, RoutedEventArgs e) => SwitchCategory(CleanCategory.Screenshot);

    private void DcimTab_Click(object sender, RoutedEventArgs e) => SwitchCategory(CleanCategory.Dcim);

    private void SwitchCategory(CleanCategory id)
    {
        ShotTab.IsChecked = id == CleanCategory.Screenshot;
        DcimTab.IsChecked = id == CleanCategory.Dcim;

        if (_root is null || _category == id) return;

        _category = id;
        _dir = null;
        SelectFolder(CategoryDir(id));
    }

    private void SelectFolder(string dir)
    {
        if (string.Equals(_dir, dir, StringComparison.OrdinalIgnoreCase)) return;

        _dir = dir;
        DirText.Text = dir;
        DirText.ToolTip = dir;
        _pendingScrollTop = true;
        Reload(preserve: false);
    }

    private void OpenDir_Click(object sender, RoutedEventArgs e)
    {
        if (_dir is null || !Directory.Exists(_dir)) return;
        Process.Start(new ProcessStartInfo(_dir) { UseShellExecute = true });
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload(preserve: true);

    // ---------------- 枚举 ----------------

    private void Reload(bool preserve)
    {
        _token++;
        if (_dir is null) return;

        if (!Directory.Exists(_dir))
        {
            _all.Clear();
            _rows.Clear();
            _months.Clear();
            EmptyHint.Text = "目录不存在";
            EmptyHint.Visibility = Visibility.Visible;
            CountText.Text = "0 个文件";
            UpdateSelection();
            StopWatcher();
            return;
        }

        List<FileInfo> files;
        try
        {
            files = new DirectoryInfo(_dir)
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .Where(f => MediaExtensions.Contains(f.Extension))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
        }
        catch
        {
            files = [];
        }

        // 保留已有 ViewModel，避免刷新时缩略图全部重解码（会闪一下）
        var existing = preserve
            ? _all.ToDictionary(v => v.FullPath, StringComparer.OrdinalIgnoreCase)
            : null;

        _all.Clear();
        foreach (var f in files)
            _all.Add(existing is not null && existing.TryGetValue(f.FullName, out var vm)
                ? vm
                : new MediaItemVm(f, _category));

        // 按拍摄时间倒序（文件名优先，缺则 mtime），月份分组才有意义
        _all.Sort((a, b) =>
        {
            var cmp = b.Timestamp.CompareTo(a.Timestamp);
            return cmp != 0 ? cmp : string.CompareOrdinal(b.Name, a.Name);
        });

        if (!preserve) _pendingScrollTop = true;

        _columns = -1;          // 强制按当前宽度重建行
        RebuildRows(force: true);

        StartWatcher(_dir);
    }

    // ---------------- 行构建（虚拟化的数据源） ----------------

    private void ContentHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) RebuildRows(force: false);
    }

    private void RebuildRows(bool force)
    {
        var width = ContentHost.ActualWidth - 16;     // 留出滚动条与内边距
        if (width <= 0) width = 900;

        var columns = Math.Max(1, (int)(width / CellWidth));
        if (!force && columns == _columns && _rows.Count > 0) return;

        _columns = columns;
        BuildRows();
    }

    private void BuildRows()
    {
        var anchor = _pendingScrollTop ? -1 : AnchorItemIndex();

        _suppressScrollTo = true;
        try
        {
            _rows.Clear();
            _months.Clear();

            var columns = Math.Max(1, _columns);
            var i = 0;

            while (i < _all.Count)
            {
                // 一个自然月一段：列表已按时间倒序，同月必然连续
                var monthKey = _all[i].MonthKey;
                var start = i;
                var slice = new List<MediaItemVm>();

                while (i < _all.Count && _all[i].MonthKey == monthKey)
                {
                    slice.Add(_all[i]);
                    i++;
                }

                var bytes = slice.Sum(v => v.SizeBytes);
                var videos = slice.Count(v => v.IsVideo);
                var sub = videos > 0
                    ? $"{slice.Count} 项 · {TargetItem.FormatSize(bytes)} · 视频 {videos}"
                    : $"{slice.Count} 项 · {TargetItem.FormatSize(bytes)}";

                var month = new MediaMonthVm
                {
                    Key = monthKey,
                    HeaderText = $"{slice[0].Timestamp:yyyy 年 MM 月}",
                    Count = slice.Count,
                    SizeBytes = bytes,
                    RowIndex = _rows.Count
                };
                _months.Add(month);

                _rows.Add(new MediaRowVm
                {
                    HeaderText = month.HeaderText,
                    HeaderSubText = sub
                });

                for (var k = 0; k < slice.Count; k += columns)
                {
                    var take = Math.Min(columns, slice.Count - k);
                    _rows.Add(new MediaRowVm
                    {
                        Items = slice.GetRange(k, take),
                        Columns = columns,
                        StartIndex = start + k
                    });
                }
            }

            // 不再自动选中首月：否则用户滚走后再点「已选中」的那一项会被 ListBox 吞掉
            UpdateCounts();
        }
        finally
        {
            _suppressScrollTo = false;
        }

        _pendingScrollTop = false;
        _lastTrimRow = int.MinValue;
        RestoreScroll(anchor);
    }

    /// <summary>当前视口首行对应的条目下标，用于宽度变化时保持阅读位置。</summary>
    private int AnchorItemIndex()
    {
        var scroll = RowScroll;
        if (scroll is null || _rows.Count == 0) return -1;

        var index = (int)Math.Floor(scroll.VerticalOffset);
        if (index < 0 || index >= _rows.Count) return -1;

        return _rows[index].StartIndex;
    }

    private void RestoreScroll(int anchorItem)
    {
        var target = anchorItem < 0 ? 0 : RowIndexOfItem(anchorItem);
        if (target < 0) target = 0;

        // 等虚拟化面板完成一次布局再定位，否则 ScrollToVerticalOffset 会被忽略
        Dispatcher.BeginInvoke(new Action(() =>
        {
            HookScroll();
            RowScroll?.ScrollToVerticalOffset(target);
        }), DispatcherPriority.Background);
    }

    private int RowIndexOfItem(int itemIndex)
    {
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (row.IsHeader) continue;
            if (itemIndex >= row.StartIndex && itemIndex < row.StartIndex + row.Items.Count) return r;
        }
        return -1;
    }

    // ---------------- 时间导航 ----------------

    private void MonthList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressScrollTo) return;
        if (MonthList.SelectedItem is not MediaMonthVm month || month.RowIndex < 0) return;

        ScrollToRow(month.RowIndex);
    }

    /// <summary>
    /// 点击已选中的月份时 ListBox 不会再发 SelectionChanged，
    /// 所以定位动作挂在 MouseUp 上，保证「点哪个月就跳到哪个月」永远有效。
    /// </summary>
    private void MonthItem_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: MediaMonthVm month } && month.RowIndex >= 0)
            ScrollToRow(month.RowIndex);
    }

    private void ScrollToRow(int rowIndex)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            HookScroll();
            RowScroll?.ScrollToVerticalOffset(rowIndex);
        }), DispatcherPriority.Background);
    }

    private static MediaMonthVm? MonthOf(object sender) =>
        sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } }
            ? target.DataContext as MediaMonthVm
            : null;

    private void MonthCheckAll_Click(object sender, RoutedEventArgs e) => SetMonthChecked(sender, true);

    private void MonthUncheckAll_Click(object sender, RoutedEventArgs e) => SetMonthChecked(sender, false);

    private void SetMonthChecked(object sender, bool value)
    {
        if (MonthOf(sender) is not { } month) return;

        foreach (var item in _all.Where(i => i.MonthKey == month.Key))
            item.IsChecked = value;

        UpdateSelection();
    }

    // ---------------- 滚动与缩略图释放 ----------------

    private ScrollViewer? RowScroll
    {
        get
        {
            if (_scroll is not null) return _scroll;

            _scroll = RowHost.Template?.FindName("RowScroll", RowHost) as ScrollViewer;
            if (_scroll is not null) _scroll.ScrollChanged += RowScroll_ScrollChanged;
            return _scroll;
        }
    }

    private void HookScroll() => _ = RowScroll;

    private void RowScroll_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
        TrimThumbnails((int)Math.Floor(e.VerticalOffset));

    /// <summary>
    /// 虚拟化只管视觉，缩略图是托管内存：滚出缓冲区的行主动释放，
    /// 否则一路翻到底会把整目录的缩略图都留在内存里。
    /// </summary>
    private void TrimThumbnails(int firstVisibleRow)
    {
        if (_rows.Count == 0 || firstVisibleRow == _lastTrimRow) return;
        _lastTrimRow = firstVisibleRow;

        var lo = Math.Max(0, firstVisibleRow - TrimBuffer);
        var hi = Math.Min(_rows.Count - 1, firstVisibleRow + TrimBuffer);

        for (var r = 0; r < _rows.Count; r++)
        {
            if (r >= lo && r <= hi) continue;
            foreach (var item in _rows[r].Items)
                if (item.HasThumbnail) item.ReleaseThumbnail();
        }
    }

    private void Card_Loaded(object sender, RoutedEventArgs e)
    {
        RealizedCardCount++;
        if (sender is FrameworkElement { DataContext: MediaItemVm vm }) _ = EnsureThumbnailAsync(vm);
    }

    private async Task EnsureThumbnailAsync(MediaItemVm vm)
    {
        if (vm.ThumbRequested || vm.HasThumbnail) return;

        vm.MarkThumbRequested();

        var token = _token;
        var path = vm.FullPath;

        await ThumbGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var image = await Task.Run(() => ShellThumbnail.Get(path));
            if (token != _token) return;
            vm.Thumbnail = image;
        }
        finally
        {
            ThumbGate.Release();
        }
    }

    // ---------------- 勾选 ----------------

    private void CardCheck_Click(object sender, RoutedEventArgs e) => UpdateSelection();

    private void CheckItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } vm) { vm.IsChecked = true; UpdateSelection(); }
    }

    private void UncheckItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } vm) { vm.IsChecked = false; UpdateSelection(); }
    }

    private void CheckAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void UncheckAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool value)
    {
        foreach (var item in _all) item.IsChecked = value;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selected = _all.Where(v => v.IsChecked).ToList();
        var bytes = selected.Sum(v => v.SizeBytes);

        SelText.Text = selected.Count == 0
            ? "已选 0 项"
            : $"已选 {selected.Count} 项 · {TargetItem.FormatSize(bytes)}";

        SelSubText.Text = selected.Count == 0
            ? "勾选缩略图左上角复选框后可清理"
            : $"共 {selected.Count} 个文件 · 其中视频 {selected.Count(v => v.IsVideo)} 个";

        // 与数据清理页一致：按钮常驻可点，未勾选时点击给出提示
        MediaCleanButton.IsEnabled = true;
    }

    private void UpdateCounts()
    {
        var videos = _all.Count(v => v.IsVideo);
        var bytes = _all.Sum(v => v.SizeBytes);

        CountText.Text = _all.Count == 0
            ? "0 个文件"
            : $"共 {_all.Count} 项（图片 {_all.Count - videos} · 视频 {videos}） · "
              + $"{TargetItem.FormatSize(bytes)} · {_months.Count} 个月";

        EmptyHint.Text = Directory.Exists(_dir ?? string.Empty) ? "该目录下暂无图片或视频" : "目录不存在";
        EmptyHint.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateSelection();
    }

    // ---------------- 清理方式 ----------------

    private void MediaMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressModeEvent) return;

        ApplyModeVisual();

        if (!IsLoaded) return;   // 初始化阶段（由 SetMode 设置）不算用户操作
        ModeChanged?.Invoke(CurrentMode);
    }

    private void ApplyModeVisual()
    {
        if (MediaCleanButton is null || MediaDeleteRadio is null) return;

        var danger = MediaDeleteRadio.IsChecked == true;

        MediaCleanButton.Background = danger ? DangerButtonBg : PrimaryButtonBg;
        MediaCleanButton.Content = danger ? "直接删除选中" : "清理选中";
        MediaDeleteRadio.Foreground = danger ? DangerText : PrimaryText;
    }

    private void MediaClean_Click(object sender, RoutedEventArgs e)
    {
        var selected = _all.Where(v => v.IsChecked).Select(v => v.ToNode()).ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                "还没有勾选任何图片或视频，点击缩略图左上角复选框即可选中。",
                "无可清理项", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var mode = MediaDeleteRadio.IsChecked == true ? CleanMode.DirectDelete : CleanMode.RecycleBin;
        CleanRequested?.Invoke(selected, mode);
    }

    // ---------------- 实时预览 ----------------

    private void StartWatcher(string dir)
    {
        StopWatcher();

        try
        {
            var watcher = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false
            };

            watcher.Created += OnDirChanged;
            watcher.Deleted += OnDirChanged;
            watcher.Renamed += OnDirChanged;
            watcher.Changed += OnDirChanged;
            watcher.EnableRaisingEvents = true;

            _watcher = watcher;
        }
        catch
        {
            _watcher = null; // 目录不可监听时静默降级，手动刷新仍可用
        }
    }

    private void StopWatcher()
    {
        var watcher = _watcher;
        _watcher = null;
        if (watcher is null) return;

        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        catch { /* ignore */ }
    }

    private void OnDirChanged(object sender, FileSystemEventArgs e)
    {
        var ext = Path.GetExtension(e.FullPath);
        if (string.IsNullOrEmpty(ext) || !MediaExtensions.Contains(ext)) return;

        Dispatcher.BeginInvoke(() =>
        {
            _debounce.Stop();   // 游戏落盘会连发多次，防抖后只刷一次
            _debounce.Start();
        });
    }

    // ---------------- 打开 / 右键菜单 ----------------

    private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MediaItemVm vm }) return;

        if (e.ClickCount == 1)
        {
            // 单击整张卡片也能勾选；记下before值，双击时撤销这次切换
            _pendingToggle = vm;
            _pendingPrev = vm.IsChecked;
            vm.IsChecked = !vm.IsChecked;
            UpdateSelection();
            return;
        }

        if (e.ClickCount == 2 && ReferenceEquals(_pendingToggle, vm))
        {
            vm.IsChecked = _pendingPrev;
            _pendingToggle = null;
            UpdateSelection();
            OpenWithDefault(vm);
            e.Handled = true;
        }
    }

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } vm) OpenWithDefault(vm);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } vm) return;

        try
        {
            var psi = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                Arguments = $"/select,\"{vm.FullPath}\""
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } vm) return;

        try { Clipboard.SetText(vm.FullPath); }
        catch { /* 剪贴板被占用时忽略 */ }
    }

    /// <summary>从 ContextMenu 的 MenuItem 反查所属条目。</summary>
    private static MediaItemVm? ItemOf(object sender) =>
        sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } }
            ? target.DataContext as MediaItemVm
            : null;

    private static void OpenWithDefault(MediaItemVm vm)
    {
        try
        {
            if (!File.Exists(vm.FullPath)) return;
            Process.Start(new ProcessStartInfo(vm.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "无法打开文件", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
