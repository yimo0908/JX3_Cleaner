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

    public TreeItemVm(ScanNode node, TreeItemVm? parent, Action<TreeItemVm>? onCheckChanged, bool expand)
    {
        Node = node;
        Parent = parent;
        _onCheckChanged = onCheckChanged;
        _isExpanded = expand;
        _isChecked = node.DefaultSelected;

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

    public string SizeText => !IsGroupHeader && (Node.SizeBytes > 0 || Node.FileCount > 0)
        ? TargetItem.FormatSize(Node.SizeBytes)
        : string.Empty;

    public bool IsInvalid => Node.Flags.HasFlag(NodeFlags.InvalidAccount);

    public bool IsLegacy => Node.Flags.HasFlag(NodeFlags.LegacyRegion);

    public bool IsIncomplete => Node.Flags.HasFlag(NodeFlags.IncompleteGroup);

    public bool IsSelectable => Node.Selectable && !IsGroupHeader;

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

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetChecked(value ?? false, propagateDown: true, notifyParent: true, notifyHost: true);
    }

    /// <summary>外部回滚勾选（例如用户取消了过期区服确认）。</summary>
    public void RevertCheck(bool value = false)
    {
        SetChecked(value, propagateDown: true, notifyParent: true, notifyHost: false);
    }

    public void Attach(TreeItemVm child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    private void SetChecked(bool value, bool propagateDown, bool notifyParent, bool notifyHost)
    {
        var changed = _isChecked != value;
        _isChecked = value;

        if (changed) OnPropertyChanged(nameof(IsChecked));

        if (propagateDown)
        {
            _suppress = true;
            foreach (var child in Children) child.SetChecked(value, true, false, false);
            _suppress = false;
        }

        if (notifyParent) Parent?.RefreshFromChildren();
        if (notifyHost && !_suppress) _onCheckChanged?.Invoke(this);
    }

    private void RefreshFromChildren()
    {
        if (Children.Count == 0) return;

        var all = Children.All(c => c._isChecked == true);
        var none = Children.All(c => c._isChecked == false);
        var target = all ? true : none ? false : (bool?)null;

        if (_isChecked != target)
        {
            _isChecked = target;
            OnPropertyChanged(nameof(IsChecked));
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
