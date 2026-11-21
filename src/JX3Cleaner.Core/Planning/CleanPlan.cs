using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Planning;

public sealed record RejectedItem(string Path, string Reason);

/// <summary>
/// 被勾选的一个节点，外加「它自己的条目算不算数」。
///
/// 账号行既是分组又是整包删除目标：只有用户**直接勾**了它，自有条目才生效。
/// 由子项推导出来的「全选」状态（勾了一个过期大区）不代表用户要删整个账号，
/// 否则过期大区的勾选会连带删掉同一账号下所有有效区服。
/// </summary>
public readonly record struct SelectedNode(ScanNode Node, bool IncludeOwnTargets = true);

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
    /// <summary>所有被勾选节点的自有条目都算数（图片页、诊断工具、测试用）。</summary>
    public static CleanPlan Build(IEnumerable<ScanNode> selected, PathGuard guard) =>
        Build(selected.Select(node => new SelectedNode(node)), guard);

    public static CleanPlan Build(IEnumerable<SelectedNode> selected, PathGuard guard)
    {
        var plan = new CleanPlan();

        foreach (var item in Collect(selected))
        {
            var check = guard.Check(item);
            if (check.Ok) plan.Items.Add(item);
            else plan.Rejected.Add(new RejectedItem(item.Path, check.Reason ?? "未通过校验"));
        }

        return plan;
    }

    /// <summary>
    /// 收集条目：递归取得子孙节点的条目，去重并剔除被父目录包含的重复项。
    /// 不做路径校验 —— 界面的「已选 N 项」也走这里，保证与真正执行的清单同口径。
    /// </summary>
    public static List<TargetItem> Collect(IEnumerable<SelectedNode> selected)
    {
        var buckets = new List<TargetItem>();

        foreach (var selection in selected)
            foreach (var t in EnumerateTargets(selection.Node, selection.IncludeOwnTargets))
                buckets.Add(t);

        var distinct = buckets
            .GroupBy(t => Path.TrimEndingDirectorySeparator(t.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var dirs = distinct.Where(t => t.IsDirectory)
                           .Select(t => Path.TrimEndingDirectorySeparator(t.Path) + Path.DirectorySeparatorChar)
                           .ToList();

        var result = new List<TargetItem>(distinct.Count);

        foreach (var item in distinct)
        {
            var full = Path.TrimEndingDirectorySeparator(item.Path);
            if (dirs.Any(d => full.StartsWith(d, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(full + Path.DirectorySeparatorChar, d, StringComparison.OrdinalIgnoreCase)))
                continue; // 已被父目录覆盖

            result.Add(item);
        }

        return result;
    }

    private static IEnumerable<TargetItem> EnumerateTargets(ScanNode node, bool includeOwn)
    {
        if (includeOwn)
            foreach (var t in node.Targets) yield return t;

        foreach (var child in node.Children)
            foreach (var t in EnumerateTargets(child, includeOwn: true))
                yield return t;
    }
}
