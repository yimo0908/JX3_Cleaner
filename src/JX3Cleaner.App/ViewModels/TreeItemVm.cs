using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.App.ViewModels;

/// <summary>树节点视图模型。负责勾选状态的父子联动与展示样式。</summary>
public sealed class TreeItemVm : INotifyPropertyChanged
{
    private static readonly Brush Primary = Freeze("#1F1F1F");
    private static readonly Brush Secondary = Freeze("#595959");
    private static readonly Brush Tertiary = Freeze("#8C8C8C");
    private static readonly Brush Danger = Freeze("#C62828");
    private static readonly Brush DangerBg = Freeze("#FDECEA");
    private static readonly Brush Transparent = Brushes.Transparent;
    private static readonly Brush Warn = Freeze("#B26A00");
    private static readonly Brush WarnBg = Freeze("#FFF4E5");
    private static readonly Brush GroupBg = Freeze("#F5F4F1");
    private static readonly Brush PathBrush = Freeze("#9A9A9A");

    /// <summary>已弃用标记：浅底上取偏深的金黄，11px 仍可辨。</summary>
    private static readonly Brush DeprecatedFg = Freeze("#B8860B");

    private readonly Action<TreeItemVm>? _onCheckChanged;
    private bool _suppress;
    private bool? _isChecked;
    private bool _isExpanded;
    private bool _isVisible = true;

    /// <summary>
    /// 勾选状态是「用户直接点出来的」还是「由子项推导出来的」。
    /// 账号行既是分组又是整包删除目标，只有直接勾选才算数：
    /// 勾一个过期大区会把账号行推成「全选」，那不代表用户要删整个账号。
    /// </summary>
    private bool _checkedExplicitly;

    public TreeItemVm(ScanNode node, TreeItemVm? parent, Action<TreeItemVm>? onCheckChanged, bool expand)
    {
        Node = node;
        Parent = parent;
        _onCheckChanged = onCheckChanged;
        _isExpanded = expand;
        // 没有可清理条目的行不接受勾选，初始态也不会是「勾着但清不了」
        _isChecked = node.DefaultSelected && IsCheckable;
        _checkedExplicitly = _isChecked == true;

        foreach (var child in node.Children)
            Children.Add(new TreeItemVm(child, this, onCheckChanged, expand: false));

        // 有子项的节点初始为三态：全选 / 全不选 / 部分
        if (Children.Count > 0 && !IsGroupHeader) RefreshFromChildren();
    }

    public ScanNode Node { get; }

    public TreeItemVm? Parent { get; private set; }

    public ObservableCollection<TreeItemVm> Children { get; } = [];

    /// <summary>组标题行（茗伊插件 / 懒人插件 / 游戏数据）：不可勾选、不显示复选框与体积。</summary>
    public bool IsGroupHeader => Node.Kind == NodeKind.Category && Node.Category is null;

    public bool IsCategory => Node.Kind == NodeKind.Category && Node.Category is not null;

    public string Title => Node.Title;

    public string RoleText => string.IsNullOrEmpty(Node.RoleSummary) ? string.Empty : $"（{Node.RoleSummary}）";

    public bool HasRoles => !string.IsNullOrEmpty(Node.RoleSummary);

    public string NoteText => Node.Note ?? string.Empty;

    public bool HasNote => !string.IsNullOrEmpty(Node.Note);

    /// <summary>已弃用分类标题后的黄色「（已弃用）」。</summary>
    public string DeprecatedText => Node.Deprecated ? CategoryDefinition.DeprecatedLabel : string.Empty;

    public bool HasDeprecated => Node.Deprecated;

    public Brush DeprecatedBrush => DeprecatedFg;

    /// <summary>分类行标题后的相对路径（灰色），可点击打开资源管理器。</summary>
    public string RelativePathText => Node.RelativePathText ?? string.Empty;

    public bool HasRelativePath => IsCategory && !string.IsNullOrEmpty(Node.RelativePathText);

    public string? BrowsePath => Node.BrowsePath;

    public string SizeText
    {
        get
        {
            if (IsGroupHeader) return string.Empty;

            // 行上显示「这次能清掉多少」：总占用与可清理量口径不同，
            // 直接显示总占用会出现「勾上 8.8 GB，底部却说已选 0 项」
            if (Node.CleanableBytes > 0) return TargetItem.FormatSize(Node.CleanableBytes);

            // 被保留策略或时间限制挡掉的行：占着空间但这次删不了，直说比留空更清楚
            return Node.SizeBytes > 0 ? "不清理" : string.Empty;
        }
    }

    /// <summary>体积列的悬浮说明：总占用 / 可清理条数。</summary>
    public string SizeTooltip
    {
        get
        {
            if (IsGroupHeader || Node.SizeBytes == 0) return string.Empty;

            var text = $"共占用 {TargetItem.FormatSize(Node.SizeBytes)}";
            return Node.CleanableCount > 0
                ? $"{text}　可清理 {Node.CleanableCount} 项 · {TargetItem.FormatSize(Node.CleanableBytes)}"
                : $"{text}　本次没有可清理的条目（被保留策略或时间限制排除了）";
        }
    }

    public bool IsInvalid => Node.Flags.HasFlag(NodeFlags.InvalidAccount);

    /// <summary>本行就是过期区服（大区名不在白名单内）。</summary>
    public bool IsLegacy => Node.IsLegacyRegion;

    /// <summary>账号下含过期区服：账号行补一个不同的角标，避免和「本行是旧大区」混为一谈。</summary>
    public bool HasLegacyChild => Node.HasLegacyChild;

    /// <summary>本次勾选会不会动到过期区服数据：旧大区行本身，或被直接勾选的账号行。</summary>
    public bool InvolvesLegacy => Node.IsLegacyRegion || (Node.HasLegacyChild && _checkedExplicitly);

    /// <summary>勾选状态是否来自用户直接操作（而非子项推导）。</summary>
    public bool CheckedExplicitly => _checkedExplicitly;

    public bool IsIncomplete => Node.Flags.HasFlag(NodeFlags.IncompleteGroup);

    /// <summary>被保留策略护住的行（如「保留最近 5 组」里的前 5 组）。</summary>
    public bool IsRetained => Node.Flags.HasFlag(NodeFlags.Retained);

    /// <summary>
    /// 本行能不能被勾选。判据只有一条：确实有能删的条目。
    /// 保留行、被时间限制挡掉的行、本次为空的分组都不参与勾选 ——
    /// 「勾着但清不了东西」在界面上是个误导，直接置灰。
    /// </summary>
    public bool IsCheckable => !IsGroupHeader && Node.CleanableCount > 0;

    public bool IsSelectable => Node.Selectable && IsCheckable;

    /// <summary>组标题行不显示复选框，其余都显示（顶级分类可勾选）。</summary>
    public bool ShowCheckBox => !IsGroupHeader;

    public Brush TitleBrush => IsInvalid ? Danger : IsGroupHeader ? Primary : Node.Kind == NodeKind.Character ? Secondary : Primary;

    public Brush RowBackground => IsGroupHeader ? GroupBg : IsInvalid ? DangerBg : Transparent;

    public Brush NoteBrush => IsInvalid ? Danger : Tertiary;

    public Brush RelativePathBrush => PathBrush;

    public double TitleFontSize => IsGroupHeader ? 14 : 13;

    public FontWeight TitleWeight => IsGroupHeader ? FontWeights.SemiBold : FontWeights.Medium;

    public Thickness RowPadding => IsGroupHeader ? new Thickness(4, 9, 4, 5) : new Thickness(3, 2, 3, 2);

    public Thickness RowMargin => IsGroupHeader ? new Thickness(0, 10, 0, 3) : new Thickness(0);

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 左栏「数据类型」开关：取消勾选只是把该分类从树里藏起来，
    /// 不重建树、不动用户已经勾好的条目（重建会把勾选状态全部重置）。
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetChecked(value ?? false, propagateDown: true, notifyParent: true, notifyHost: true, direct: true);
    }

    /// <summary>外部回滚勾选（例如用户取消了过期区服确认）。</summary>
    public void RevertCheck(bool value = false)
    {
        SetChecked(value, propagateDown: true, notifyParent: true, notifyHost: false, direct: false);
    }

    public void Attach(TreeItemVm child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    private void SetChecked(bool value, bool propagateDown, bool notifyParent, bool notifyHost, bool direct)
    {
        // 没有可清理条目的行（保留 / 被时间限制挡掉 / 空分组）不接受勾选，
        // 无论是点它自己还是被上游批量勾选带过来
        if (!IsCheckable) return;

        var changed = _isChecked != value;
        _isChecked = value;
        _checkedExplicitly = value && direct;

        if (changed)
        {
            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(InvolvesLegacy));
        }

        if (propagateDown)
        {
            _suppress = true;
            // 向下传播是「整棵子树都要」的明确意图，子节点按直接勾选处理；
            // 不可勾的子节点（保留组）跳过，否则会出现勾着却清不掉的假状态
            foreach (var child in Children.Where(c => c.IsCheckable))
                child.SetChecked(value, true, false, false, direct);
            _suppress = false;
        }

        if (notifyParent) Parent?.RefreshFromChildren();
        if (notifyHost && !_suppress) _onCheckChanged?.Invoke(this);
    }

    private void RefreshFromChildren()
    {
        var candidates = Children.Where(c => c.IsCheckable).ToList();
        if (candidates.Count == 0) return;

        var all = candidates.All(c => c._isChecked == true);
        var none = candidates.All(c => c._isChecked == false);
        var target = all ? true : none ? false : (bool?)null;

        if (_isChecked != target)
        {
            _isChecked = target;

            // 状态由子项推导出来，就不再代表「用户要动这个节点自己的条目」
            _checkedExplicitly = false;

            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(InvolvesLegacy));
        }

        Parent?.RefreshFromChildren();
    }

    public IEnumerable<TreeItemVm> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var d in child.SelfAndDescendants())
                yield return d;
    }

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
