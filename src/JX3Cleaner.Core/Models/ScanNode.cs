namespace JX3Cleaner.Core.Models;

/// <summary>
/// 扫描结果树节点。既是数据模型也是界面数据源，
/// 体积/文件数/最近修改时间在扫描结束时自底向上聚合。
/// </summary>
public sealed class ScanNode
{
    public required NodeKind Kind { get; init; }

    public CleanCategory? Category { get; init; }

    /// <summary>分类节点所属的组（仅分类节点有效）。</summary>
    public CategoryGroup Group { get; init; } = CategoryGroup.Game;

    /// <summary>分类节点在界面标题后显示的相对路径（相对 bin\zhcn_hd）。</summary>
    public string? RelativePathText { get; init; }

    /// <summary>分类节点「点击路径打开资源管理器」的目标目录。</summary>
    public string? BrowsePath { get; init; }

    /// <summary>已弃用分类（懒人插件 / 旧版团监）：标题后补黄色「（已弃用）」，仅分类节点有效。</summary>
    public bool Deprecated { get; init; }

    /// <summary>主标题。账号节点为账号名，角色数据节点为大区/服务器/角色名。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 账号节点的角色名摘要，界面以「（角色1、角色2…）」小号灰字呈现。
    /// </summary>
    public string? RoleSummary { get; set; }

    /// <summary>灰色备注，如 "保留最近 2 个"。</summary>
    public string? Note { get; set; }

    /// <summary>过期区服名称列表（逗号分隔），仅 NodeFlags.LegacyRegion 时有值。</summary>
    public string? LegacyDetail { get; set; }

    /// <summary>磁盘路径（分类节点为 null）。</summary>
    public string? FullPath { get; init; }

    public List<ScanNode> Children { get; } = [];

    /// <summary>该节点勾选后实际要删除的条目。空表示仅作展示。</summary>
    public List<TargetItem> Targets { get; } = [];

    public long SizeBytes { get; set; }

    public int FileCount { get; set; }

    public DateTime LastWriteUtc { get; set; }

    public NodeFlags Flags { get; set; }

    public RiskLevel Risk { get; set; } = RiskLevel.Low;

    /// <summary>扫描完成时的默认勾选状态（受保留策略与时间窗口影响）。</summary>
    public bool DefaultSelected { get; set; }

    public bool Selectable { get; set; } = true;

    public bool HasTargets => Targets.Count > 0 || Children.Any(c => c.Targets.Count > 0 || c.Children.Count > 0);

    /// <summary>本节点直接持有的条目总字节（不含子节点）。</summary>
    public long OwnTargetBytes => Targets.Sum(t => t.SizeBytes);

    public bool IsInvalidAccount => Flags.HasFlag(NodeFlags.InvalidAccount);

    public bool IsLegacyRegion => Flags.HasFlag(NodeFlags.LegacyRegion);

    public string Key => FullPath is { Length: > 0 } p ? $"{Kind}|{p}" : $"{Kind}|{Category}|{Title}";

    public ScanNode Add(ScanNode child)
    {
        Children.Add(child);
        return child;
    }

    /// <summary>自底向上汇总体积、文件数与最近修改时间。</summary>
    public void Aggregate()
    {
        foreach (var child in Children) child.Aggregate();

        if (Children.Count > 0)
        {
            SizeBytes = Children.Sum(c => c.SizeBytes);
            FileCount = Children.Sum(c => c.FileCount);
            var max = Children.Count == 0 ? LastWriteUtc : Children.Max(c => c.LastWriteUtc);
            if (max > LastWriteUtc) LastWriteUtc = max;
        }
    }

    public IEnumerable<ScanNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var d in child.Descendants()) yield return d;
        }
    }
}
