using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JX3Cleaner.App.Interop;
using JX3Cleaner.App.ViewModels;
using JX3Cleaner.Core;
using JX3Cleaner.Core.Auditing;
using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Executing;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;
using JX3Cleaner.Core.Scanning;

namespace JX3Cleaner.App;

public partial class MainWindow : Window
{
    private static readonly Brush NormalButtonBg = Freeze("#1F1F1F");
    private static readonly Brush DangerButtonBg = Freeze("#C62828");
    private static readonly Brush OkBrush = Freeze("#2E7D32");
    private static readonly Brush ErrorBrush = Freeze("#C62828");
    private static readonly Brush MutedBrush = Freeze("#8C8C8C");
    private static readonly Brush DarkText = Freeze("#1F1F1F");

    /// <summary>已弃用标记的黄色字（在浅底上取偏深的金黄，保证 11px 仍可辨）。</summary>
    private static readonly Brush DeprecatedBrush = Freeze("#B8860B");

    private readonly ObservableCollection<TreeItemVm> _roots = [];
    private readonly Dictionary<CleanCategory, CheckBox> _categoryBoxes = [];
    private readonly List<FilterGroupUi> _filterGroups = [];
    private readonly HashSet<string> _confirmedLegacy = new(StringComparer.OrdinalIgnoreCase);

    private ScanResult? _scan;
    private bool _ready;
    private bool _teamMonPrompted;
    private bool _busy;
    private bool _loadingMode;
    private bool _syncingMode;

    public MainWindow()
    {
        InitializeComponent();
        WindowChrome.EnableRoundedCorners(this);

        DataTree.ItemsSource = _roots;
        Loaded += MainWindow_Loaded;

        BuildCategoryFilters();
        ApplySettingsToUi();
        ApplyModeVisual();

        // 图片页的勾选清理交回主窗口，复用同一套计划构建 / 路径守卫 / 审计
        MediaPage.CleanRequested += OnMediaCleanRequested;
        MediaPage.ModeChanged += OnMediaModeChanged;
    }

    private CleanerService Service => App.Service;

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // ---------------- 初始化 ----------------

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ready = true;

        if (Service.Root is null)
        {
            // 正常情况下 App 启动时已强制走过设置页，这里只是兜底
            SetStatus("未定位客户端地址", ErrorBrush);
            await OpenSettingsAsync(firstRun: true);
            return;
        }

        ApplyRoot();
        ApplyShotWindow();
        await ScanAsync();
        await MaybeScreenshotAsync();
    }

    /// <summary>调试用：--days N 预设文件时间筛选，便于截图验证过滤提示。</summary>
    private void ApplyShotWindow()
    {
        if (App.ShotDays <= 0) return;

        foreach (var item in WindowBox.Items.OfType<ComboBoxItem>())
        {
            if ((string?)item.Tag == App.ShotDays.ToString())
            {
                WindowBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ApplyRoot()
    {
        if (Service.Root is null) return;

        PathBox.Text = Service.Root.Root;
        PathBox.ToolTip = Service.Root.ZhcnDir;
        SetStatus("已校验", OkBrush);
        MediaPage.Attach(Service.Root);
    }

    /// <summary>把设置里的默认清理方式铺到界面上（两页共用同一个设置）。</summary>
    private void ApplySettingsToUi()
    {
        _loadingMode = true;
        try
        {
            var mode = Service.Settings.Current.DefaultMode;
            RecycleRadio.IsChecked = mode == CleanMode.RecycleBin;
            DeleteRadio.IsChecked = mode == CleanMode.DirectDelete;
            MediaPage.SetMode(mode);
        }
        finally
        {
            _loadingMode = false;
        }
    }

    /// <summary>调试断言：把当前窗口渲染为 PNG 后退出。</summary>
    private async Task MaybeScreenshotAsync()
    {
        var path = App.ScreenshotPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        if (App.ShotMediaTab)
        {
            Pages.SelectedIndex = 1;
            await Task.Delay(1200);
            MediaPage.Refit();              // 截图前按最终宽度再排一次

            if (App.ShotMonth >= 0)         // 验证「点击月份 → 滚动定位」
            {
                await Task.Delay(400);
                MediaPage.JumpToMonth(App.ShotMonth);
            }

            await Task.Delay(1400);         // 等缩略图解码
        }
        else
        {
            await Task.Delay(600);  // 等布局与树展开完成
        }

        try
        {
            WindowShot.Save(this, path);

            // 诊断：验证图片页是行级虚拟化（实体化卡片数应远小于文件总数）
            if (App.ShotMediaTab)
            {
                var stats = Path.Combine(Path.GetTempPath(), "jx3-media-stats.txt");
                await File.WriteAllTextAsync(stats,
                    $"rows={MediaPage.RowCount}{Environment.NewLine}" +
                    $"realizedCards={MediaPage.RealizedCardCount}{Environment.NewLine}");
            }
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private void BuildCategoryFilters()
    {
        var first = true;

        foreach (var (group, title) in CategoryCatalog.GroupOrder)
        {
            // 截图 / 幻境云图 归「截图与云图」页，不在数据清理页出现
            var defs = CategoryCatalog.OfDataTab(group).ToList();
            if (defs.Count == 0) continue;

            // 每个组包一层，组内分类全部不存在时整组（含分隔线）一起隐藏
            var separator = new Border
            {
                Height = 1,
                Background = Freeze("#E5E5E3"),
                Margin = new Thickness(0, 12, 0, 10),
                Visibility = first ? Visibility.Collapsed : Visibility.Visible
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DarkText,
                Margin = new Thickness(0, 0, 0, 7)
            });

            var ui = new FilterGroupUi { Group = group, Panel = panel, Separator = separator };

            foreach (var def in defs)
            {
                var box = new CheckBox
                {
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 5),
                    IsChecked = true,
                    Tag = def.Id,
                    Content = def.Deprecated ? DeprecatedContent(def.Name) : def.Name
                };
                box.Checked += CategoryFilter_Changed;
                box.Unchecked += CategoryFilter_Changed;

                ui.Boxes.Add(box);
                _categoryBoxes[def.Id] = box;
                panel.Children.Add(box);
            }

            FilterPanel.Children.Add(separator);
            FilterPanel.Children.Add(panel);
            _filterGroups.Add(ui);
            first = false;
        }
    }

    /// <summary>已弃用分类的标题：名称 + 黄色「（已弃用）」。</summary>
    private static StackPanel DeprecatedContent(string name)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        row.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12,
            Foreground = DarkText,
            VerticalAlignment = VerticalAlignment.Center
        });

        row.Children.Add(new TextBlock
        {
            Text = CategoryDefinition.DeprecatedLabel,
            FontSize = 11,
            Foreground = DeprecatedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0)
        });

        return row;
    }

    // ---------------- 设置 ----------------

    private async void OpenSettings_Click(object sender, RoutedEventArgs e) =>
        await OpenSettingsAsync(firstRun: false);

    /// <summary>打开设置页；保存后若客户端目录变了就重新扫描。</summary>
    private async Task OpenSettingsAsync(bool firstRun)
    {
        if (_busy) return;

        var before = Service.Root?.Root;

        var dialog = new SettingsWindow(firstRun) { Owner = this };
        dialog.SettingsSaved += settings => Service.SaveSettings(settings);

        if (dialog.ShowDialog() != true) return;

        ApplySettingsToUi();
        ApplyModeVisual();

        if (Service.Root is null)
        {
            SetStatus("未定位客户端地址", ErrorBrush);
            return;
        }

        ApplyRoot();

        var rootChanged = !string.Equals(before, Service.Root.Root, StringComparison.OrdinalIgnoreCase);
        if (rootChanged || _scan is null) await ScanAsync();
    }

    private void SetStatus(string text, Brush brush)
    {
        StatusText.Text = text;
        StatusText.Foreground = brush;
        StatusText.ToolTip = text;
    }

    /// <summary>分类行标题后的相对路径：点击在资源管理器中打开对应目录。</summary>
    private void RelativePath_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TreeItemVm vm }) return;

        var path = vm.BrowsePath;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "打开目录失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        e.Handled = true;
    }

    // ---------------- 扫描 ----------------

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (Service.Root is null)
        {
            await OpenSettingsAsync(firstRun: true);
            return;
        }

        await ScanAsync();
    }

    private async Task ScanAsync()
    {
        if (Service.Root is null || _busy) return;

        _busy = true;
        ShowBusy("正在扫描客户端数据…", indeterminate: true);

        try
        {
            var options = new ScanOptions
            {
                Window = SelectedWindow(),
                Now = DateTime.Now,
                Keep = Service.Settings.Current.ToKeepSettings()
            };

            var result = await Task.Run(() => Service.Scan(options));
            _scan = result;
            _confirmedLegacy.Clear();
            UpdateCategoryAvailability(result);
            RebuildTree();
            UpdateSummary();
            UpdateFilterHint(result);
            MediaPage.Attach(result.Root);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            HideBusy();
            _busy = false;
        }
    }

    /// <summary>
    /// 目录不存在的分类：左右两栏直接隐藏（不再灰显禁用）。
    /// 左栏隐藏后，组内若一项都不剩，组标题与分隔线也一并隐藏。
    /// </summary>
    private void UpdateCategoryAvailability(ScanResult result)
    {
        // 以「本次扫描产出了节点」为准：目录不存在时引擎不会建节点，也不会出现在 Categories 里
        var present = result.Categories
            .Where(c => c.Category is not null)
            .Select(c => c.Category!.Value)
            .ToHashSet();

        foreach (var (id, box) in _categoryBoxes)
        {
            var hidden = result.Missing.Contains(id) || !present.Contains(id);
            box.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            if (!hidden) continue;

            box.Checked -= CategoryFilter_Changed;
            box.IsChecked = false;
            box.Checked += CategoryFilter_Changed;
        }

        SyncFilterGroupVisibility();
    }

    /// <summary>组内分类全部不可见时隐藏整组；第一个可见组不显示顶部那条分隔线。</summary>
    private void SyncFilterGroupVisibility()
    {
        var hasVisibleGroup = false;

        foreach (var group in _filterGroups)
        {
            var anyVisible = group.Boxes.Any(b => b.Visibility == Visibility.Visible);
            group.Panel.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
            group.Separator.Visibility = anyVisible && hasVisibleGroup ? Visibility.Visible : Visibility.Collapsed;
            if (anyVisible) hasVisibleGroup = true;
        }
    }

    private void CategoryFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        RebuildTree();
        UpdateSummary();
    }

    private void WindowBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _scan is null) return;
        _ = ScanAsync();
    }

    private TimeWindow SelectedWindow() => WindowBox.SelectedItem is ComboBoxItem item
        ? (string?)item.Tag switch
        {
            "7" => TimeWindow.Days7,
            "30" => TimeWindow.Days30,
            "90" => TimeWindow.Days90,
            "180" => TimeWindow.Days180,
            "365" => TimeWindow.Days365,
            _ => TimeWindow.All
        }
        : TimeWindow.All;

    /// <summary>时间筛选提示：被排除的条目数与体积，显示在下拉框正下方。</summary>
    private void UpdateFilterHint(ScanResult? result)
    {
        if (result is null || SelectedWindow() == TimeWindow.All)
        {
            FilterHintText.Visibility = Visibility.Collapsed;
            return;
        }

        var filtered = result.Filtered;
        FilterHintText.Text = filtered.Count == 0
            ? "没有条目被该时间范围排除"
            : $"已过滤 {filtered.Count} 项，约 {TargetItem.FormatSize(filtered.Bytes)}";

        FilterHintText.Visibility = Visibility.Visible;
    }

    // ---------------- 树 ----------------

    private void RebuildTree()
    {
        _roots.Clear();
        if (_scan is null) return;

        foreach (var (group, title) in CategoryCatalog.GroupOrder)
        {
            // 组标题行：不可勾选、无复选框，只作分段
            var header = new ScanNode
            {
                Kind = NodeKind.Category,
                Title = title,
                Group = group,
                Selectable = false
            };

            var groupVm = new TreeItemVm(header, null, OnNodeCheckChanged, expand: true);

            foreach (var category in _scan.Categories.Where(c => c.Group == group))
            {
                // 双保险：截图 / 幻境云图 只属于图片页
                if (category.Category is { } catId && CategoryCatalog.IsMediaOnly(catId)) continue;

                if (category.Category is { } id &&
                    _categoryBoxes.TryGetValue(id, out var box) &&
                    box.IsChecked != true) continue;

                var vm = BuildVm(category);
                if (vm is null) continue;

                groupVm.Attach(vm);
            }

            if (groupVm.Children.Count == 0) continue;

            groupVm.IsExpanded = true;
            _roots.Add(groupVm);
        }
    }

    private TreeItemVm? BuildVm(ScanNode node)
    {
        // 分类节点在条目不多时默认展开；像聊天记录 40 个账号这种先收起，避免淹没其它分类
        var expand = node.Kind == NodeKind.Category && node.Children.Count <= 10;
        var vm = new TreeItemVm(node, null, OnNodeCheckChanged, expand);
        if (vm.Children.Count == 0 && node.Targets.Count == 0) return null;
        return vm;
    }

    private void OnNodeCheckChanged(TreeItemVm vm)
    {
        if (vm.IsChecked == true)
        {
            // 云团监：首次勾选前提示备份，文案为需求指定原文
            if (vm.Node.Category == CleanCategory.CloudTeamMon && !_teamMonPrompted)
            {
                _teamMonPrompted = true;
                ConfirmDialog.Ask(this,
                    "清理云团监数据",
                    "若对团队监控数据有本地修改，请提前导出及备份。",
                    "我已了解，继续",
                    okText: "我知道了",
                    requireAck: false);
            }

            // 过期区服：本次勾选涉及的（含批量勾选分类带出来的）都需要确认
            var pending = vm.SelfAndDescendants()
                            .Where(v => v.IsLegacy && !_confirmedLegacy.Contains(v.Node.Key))
                            .ToList();

            if (pending.Count > 0)
            {
                var lines = string.Join("\n", pending.Take(8).Select(LegacyLine));

                var ok = ConfirmDialog.Ask(this,
                    "清理范围包含过期区服数据",
                    $"{lines}\n\n这些大区不属于当前客户端的有效区服（电信区 / 双线区 / 无界区），"
                    + "可能来自区服合并前的旧版本。\n\n"
                    + "其中可能仍有未迁移的按键、界面与宏配置，"
                    + "请先在游戏内确认角色已完成数据转移或合并。",
                    "我确认上述角色的数据已完成转移，或已不再需要",
                    okText: "确认清理");

                if (ok)
                {
                    foreach (var n in pending) _confirmedLegacy.Add(n.Node.Key);
                }
                else
                {
                    vm.RevertCheck(false);
                }
            }
        }

        UpdateSummary();
    }

    /// <summary>过期区服行的展示文案：区服节点带上所属账号。</summary>
    private static string LegacyLine(TreeItemVm vm)
    {
        if (vm.Node.Kind == NodeKind.Region && vm.Parent is { } parent)
            return $"· {parent.Title}　{vm.Title}";
        return $"· {vm.Title}";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void ClearAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool value)
    {
        foreach (var root in _roots)
            if (root.Node.Selectable || root.Children.Count > 0)
                root.IsChecked = value;

        UpdateSummary();
    }

    // ---------------- 底部统计 ----------------

    private void UpdateSummary()
    {
        if (_roots.Count == 0)
        {
            SelectionText.Text = "已选 0 项";
            SelectionSubText.Text = _scan is null ? "尚未扫描" : "勾选上方条目后即可清理";
            CleanButton.IsEnabled = true;   // 「开始清理」始终存在且可点，无内容时点击给出提示
            return;
        }

        var map = new Dictionary<string, (long Size, bool IsDir)>(StringComparer.OrdinalIgnoreCase);

        foreach (var vm in _roots.SelectMany(r => r.SelfAndDescendants()))
        {
            if (vm.IsChecked != true) continue;
            foreach (var t in vm.Node.Targets)
                map[t.Path] = (t.SizeBytes, t.IsDirectory);
        }

        var bytes = map.Values.Sum(v => v.Size);
        var files = map.Values.Count(v => !v.IsDir);
        var dirs = map.Values.Count(v => v.IsDir);

        SelectionText.Text = map.Count == 0
            ? "已选 0 项"
            : $"已选 {map.Count} 项 · {TargetItem.FormatSize(bytes)}";

        SelectionSubText.Text = map.Count == 0
            ? "勾选上方条目后即可清理"
            : $"{files} 个文件 / {dirs} 个目录";

        CleanButton.IsEnabled = true;
    }

    // ---------------- 清理方式 ----------------

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _loadingMode || _syncingMode) return;

        ApplyModeVisual();

        // 两页共用同一套清理方式，避免「数据页删了回收站、图片页却直接删」
        _syncingMode = true;
        try { MediaPage.SetMode(SelectedMode); }
        finally { _syncingMode = false; }
    }

    private void OnMediaModeChanged(CleanMode mode)
    {
        if (!_ready || _loadingMode || _syncingMode) return;

        _syncingMode = true;
        try
        {
            RecycleRadio.IsChecked = mode == CleanMode.RecycleBin;
            DeleteRadio.IsChecked = mode == CleanMode.DirectDelete;
        }
        finally
        {
            _syncingMode = false;
        }

        ApplyModeVisual();
    }

    private void ApplyModeVisual()
    {
        var danger = DeleteRadio?.IsChecked == true;

        if (DangerBanner is not null)
            DangerBanner.Visibility = danger ? Visibility.Visible : Visibility.Collapsed;

        if (DeleteRadio is not null)
            DeleteRadio.Foreground = danger ? ErrorBrush : DarkText;

        if (CleanButton is not null)
        {
            CleanButton.Background = danger ? DangerButtonBg : NormalButtonBg;
            CleanButton.Foreground = Brushes.White;
        }
    }

    private CleanMode SelectedMode => DeleteRadio?.IsChecked == true
        ? CleanMode.DirectDelete
        : CleanMode.RecycleBin;

    // ---------------- 执行 ----------------

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        if (_scan is null)
        {
            MessageBox.Show(this, "请先扫描客户端数据。", "尚未扫描",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selected = _roots.SelectMany(r => r.SelfAndDescendants())
                             .Where(v => v.IsChecked == true)
                             .Select(v => v.Node)
                             .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "还没有勾选任何可清理的数据，请先在左侧选择数据类型并勾选条目。",
                "无可清理项", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 过期区服兜底确认（例如被「全选」批量勾上）
        var legacyNodes = selected
            .Where(n => n.Flags.HasFlag(NodeFlags.LegacyRegion) && !_confirmedLegacy.Contains(n.Key))
            .ToList();

        if (legacyNodes.Count > 0)
        {
            var detail = string.Join("\n", legacyNodes.Take(8)
                .Select(n => n.LegacyDetail is { Length: > 0 } d ? $"· {n.Title}（{d}）" : $"· {n.Title}"));
            var ok = ConfirmDialog.Ask(this,
                "清理范围包含过期区服数据",
                $"{detail}\n\n这些大区不属于当前客户端的有效区服，可能来自区服合并前的旧版本，"
                + "清理时将以账号文件夹为整体删除。",
                "我确认上述角色的数据已完成转移，或已不再需要",
                okText: "确认清理");

            if (!ok) return;
            foreach (var n in legacyNodes) _confirmedLegacy.Add(n.Key);
        }

        await RunCleanAsync(selected, SelectedMode);
    }

    /// <summary>图片页勾选清理：走与数据页完全相同的计划 / 守卫 / 确认 / 执行流程。</summary>
    private async void OnMediaCleanRequested(IReadOnlyList<ScanNode> nodes, CleanMode mode) =>
        await RunCleanAsync(nodes, mode);

    private async Task RunCleanAsync(IReadOnlyList<ScanNode> selected, CleanMode mode)
    {
        if (_busy) return;

        var plan = Service.BuildPlan(selected);
        if (plan.Items.Count == 0)
        {
            MessageBox.Show(this, "没有可清理的条目（可能都被保留策略或路径校验排除了）。",
                "无内容", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ProcessGuard.IsGameRunning)
        {
            MessageBox.Show(this, "检测到剑网3客户端或相关进程正在运行，请先退出游戏再清理。",
                "请先退出游戏", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var modeText = mode == CleanMode.DirectDelete
            ? "直接删除（cmd.exe del，不进入回收站）"
            : "移动到回收站（可还原）";

        var summary = $"将清理 {plan.Items.Count} 项，共 {TargetItem.FormatSize(plan.TotalBytes)}。\n方式：{modeText}";
        if (plan.Rejected.Count > 0)
            summary += $"\n另有 {plan.Rejected.Count} 项未通过路径校验，已自动排除。";

        var confirmed = mode == CleanMode.DirectDelete
            ? ConfirmDialog.Ask(this, "即将直接删除，无法恢复",
                summary + "\n\n文件不会进入回收站，删除后无法通过任何方式找回。",
                "我知道该操作不可撤销", okText: "确认删除", danger: true)
            : ConfirmDialog.Ask(this, "确认清理",
                summary, "我已确认清理范围无误", okText: "开始清理");

        if (!confirmed) return;

        await ExecuteAsync(plan, mode);
    }

    private async Task ExecuteAsync(CleanPlan plan, CleanMode mode)
    {
        _busy = true;
        ShowBusy("正在清理…", indeterminate: false);
        BusyBar.Maximum = Math.Max(1, plan.Items.Count);
        BusyBar.Value = 0;

        try
        {
            var progress = new Progress<CleanProgress>(p =>
            {
                BusyBar.Value = p.Done;
                BusyText.Text = $"{p.Done} / {p.Total}    {Path.GetFileName(p.CurrentPath)}";
            });

            var outcome = await Service.ExecuteAsync(plan, mode, progress);
            AuditWriter.Write(plan, outcome, Service.Root!);

            var message = $"清理完成。\n\n成功 {outcome.Succeeded} 项\n跳过 {outcome.Skipped} 项\n失败 {outcome.Failed} 项\n"
                          + $"释放空间 {TargetItem.FormatSize(outcome.FreedBytes)}";

            if (outcome.Failures.Count > 0)
                message += "\n\n失败示例：\n" + string.Join("\n",
                    outcome.Failures.Take(5).Select(f => $"· {Path.GetFileName(f.Path)}：{f.Reason}"));

            MessageBox.Show(this, message, "清理结果", MessageBoxButton.OK,
                outcome.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await ScanAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "清理失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            HideBusy();
            _busy = false;
            UpdateSummary();
        }
    }

    private void ShowBusy(string text, bool indeterminate)
    {
        BusyText.Text = text;
        BusyBar.IsIndeterminate = indeterminate;
        BusyBar.Value = 0;
        BusyOverlay.Visibility = Visibility.Visible;
        ScanButton.IsEnabled = false;
        CleanButton.IsEnabled = false;
    }

    private void HideBusy()
    {
        BusyOverlay.Visibility = Visibility.Collapsed;
        ScanButton.IsEnabled = true;
        UpdateSummary();
    }

    /// <summary>左栏里的一组：组标题 + 各分类复选框，可整组隐藏。</summary>
    private sealed class FilterGroupUi
    {
        public required CategoryGroup Group { get; init; }

        public required StackPanel Panel { get; init; }

        /// <summary>组前的分隔线（第一组不显示）。</summary>
        public required Border Separator { get; init; }

        public List<CheckBox> Boxes { get; } = [];
    }
}
