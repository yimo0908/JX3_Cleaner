using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Planning;

public sealed record RejectedItem(string Path, string Reason);

/// <summary>
/// 一次清理的执行计划。干跑（dry-run）与真正执行使用同一份计划，
/// 保证「看到的清单」= 「删除的清单」。
/// </summary>
public sealed class CleanPlan
{
    public List<TargetItem> Items { get; } = [];
    public List<RejectedItem> Rejected { get; } = [];

    public long TotalBytes => Items.Sum(i => i.SizeBytes);
    public int TotalFiles => Items.Count(i => !i.IsDirectory);
    public int TotalEntries => Items.Count;

    public string SummaryText =>
        $"{Items.Count} 项 · {TargetItem.FormatSize(TotalBytes)}";
}

public static class CleanPlanBuilder
{
    /// <summary>
    /// 从选中的节点收集条目。会递归取得子孙节点的条目，
    /// 并剔除被父目录包含的重复条目。
    /// </summary>
    public static CleanPlan Build(IEnumerable<ScanNode> selected, PathGuard guard)
    {
        var plan = new CleanPlan();
        var buckets = new List<TargetItem>();

        foreach (var node in selected)
            foreach (var t in EnumerateTargets(node))
                buckets.Add(t);

        // 去重 + 父目录包含关系收敛
        var distinct = buckets
            .GroupBy(t => Path.TrimEndingDirectorySeparator(t.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var dirs = distinct.Where(t => t.IsDirectory)
                           .Select(t => Path.TrimEndingDirectorySeparator(t.Path) + Path.DirectorySeparatorChar)
                           .ToList();

        foreach (var item in distinct)
        {
            var full = Path.TrimEndingDirectorySeparator(item.Path);
            if (dirs.Any(d => full.StartsWith(d, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(full + Path.DirectorySeparatorChar, d, StringComparison.OrdinalIgnoreCase)))
                continue; // 已被父目录覆盖

            var check = guard.Check(item);
            if (check.Ok) plan.Items.Add(item);
            else plan.Rejected.Add(new RejectedItem(item.Path, check.Reason ?? "未通过校验"));
        }

        return plan;
    }

    private static IEnumerable<TargetItem> EnumerateTargets(ScanNode node)
    {
        foreach (var t in node.Targets) yield return t;
        foreach (var child in node.Children)
            foreach (var t in EnumerateTargets(child))
                yield return t;
    }
}
