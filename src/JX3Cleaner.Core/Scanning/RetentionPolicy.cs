using JX3Cleaner.Core.Config;

namespace JX3Cleaner.Core.Scanning;

/// <summary>
/// 一类数据按「保留策略 + 时间窗口」切分后的结果。返回的是下标而不是条数：
/// 保留的是前缀，但「可清理」和「被过滤」在剩余部分里是交错出现的
/// （较新被过滤、较旧可清理），只给条数会让调用方取错文件。
/// </summary>
public readonly record struct RetentionSplit(
    int KeepCount,
    IReadOnlyList<int> CleanIndexes,
    IReadOnlyList<int> FilteredIndexes)
{
    public int CleanCount => CleanIndexes.Count;

    public int FilteredCount => FilteredIndexes.Count;

    public int Total => KeepCount + CleanCount + FilteredCount;
}

/// <summary>
/// 保留策略的纯函数实现：给定一串「按时间倒序」的时间戳，算出
/// 前面几个被保留、后面几个进入清理范围、又有几个被时间窗口挡在外面。
///
/// 单独抽出来是为了可测：扫描引擎只负责枚举文件，规则判断全部落在这里。
/// </summary>
public static class RetentionPolicy
{
    /// <summary>
    /// 按规则保留最新的前几项。
    /// 条数规则直接取前 N；天数规则取「不早于 now - days」的前缀
    /// （输入已倒序，因此同样是一个前缀）。
    /// </summary>
    public static int KeptCount(IReadOnlyList<DateTime> timesUtcDesc, KeepRule rule, DateTime nowUtc)
    {
        if (timesUtcDesc.Count == 0 || !rule.IsActive) return 0;

        return rule.Mode switch
        {
            KeepMode.LatestCount => Math.Min(rule.Value, timesUtcDesc.Count),
            KeepMode.LatestDays => CountWithinDays(timesUtcDesc, rule.Value, nowUtc),
            _ => 0
        };
    }

    private static int CountWithinDays(IReadOnlyList<DateTime> timesUtcDesc, int days, DateTime nowUtc)
    {
        var cutoff = nowUtc.AddDays(-days);
        var count = 0;

        foreach (var t in timesUtcDesc)
        {
            if (t < cutoff) break;   // 倒序：一旦超出，后面只会更早
            count++;
        }

        return count;
    }

    /// <summary>
    /// 切分三段。<paramref name="windowCutoffUtc"/> 为 null 表示时间窗口为「全部」。
    /// 判断口径与 <see cref="ScanOptions.WithinWindow"/> 一致：早于等于截止时间才算可清理。
    /// </summary>
    public static RetentionSplit Split(IReadOnlyList<DateTime> timesUtcDesc,
                                       KeepRule rule,
                                       DateTime nowUtc,
                                       DateTime? windowCutoffUtc)
    {
        var keep = KeptCount(timesUtcDesc, rule, nowUtc);
        var clean = new List<int>();
        var filtered = new List<int>();

        for (var i = keep; i < timesUtcDesc.Count; i++)
        {
            var withinWindow = windowCutoffUtc is null || timesUtcDesc[i] <= windowCutoffUtc.Value;
            if (withinWindow) clean.Add(i);
            else filtered.Add(i);
        }

        return new RetentionSplit(keep, clean, filtered);
    }
}

/// <summary>被时间窗口挡掉的条目累计数，界面上提示「已过滤 N 项，约 XX MB」。</summary>
public sealed class FilterStats
{
    public int Count { get; private set; }

    public long Bytes { get; private set; }

    public bool IsEmpty => Count == 0;

    public void Add(int count, long bytes)
    {
        if (count <= 0) return;
        Count += count;
        Bytes += bytes;
    }

    public void Reset()
    {
        Count = 0;
        Bytes = 0;
    }
}
