using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Scanning;

namespace JX3Cleaner.Tests;

/// <summary>保留策略：条数 / 天数 / 时间窗口的切分逻辑。</summary>
public sealed class RetentionPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime DaysAgo(double days) => Now.AddDays(-days);

    [Fact]
    public void 无规则时不保留任何项()
    {
        DateTime[] times = [DaysAgo(1), DaysAgo(2), DaysAgo(3)];

        Assert.Equal(0, RetentionPolicy.KeptCount(times, KeepRule.None, Now));
    }

    [Fact]
    public void 条数规则保留最新N个()
    {
        DateTime[] times = [DaysAgo(1), DaysAgo(2), DaysAgo(3), DaysAgo(4)];

        Assert.Equal(2, RetentionPolicy.KeptCount(times, KeepRule.Latest(2), Now));
    }

    [Theory]
    [InlineData(0, 0)]      // 值为 0 → 规则失效，一个都不保留
    [InlineData(1, 1)]
    [InlineData(9, 4)]      // 超过总数 → 全保留
    public void 条数规则边界(int keep, int expected)
    {
        DateTime[] times = [DaysAgo(1), DaysAgo(2), DaysAgo(3), DaysAgo(4)];

        Assert.Equal(expected, RetentionPolicy.KeptCount(times, KeepRule.Latest(keep), Now));
    }

    [Fact]
    public void 天数规则保留最近N天产生的前缀()
    {
        DateTime[] times = [DaysAgo(1), DaysAgo(10), DaysAgo(29), DaysAgo(31), DaysAgo(60)];

        Assert.Equal(3, RetentionPolicy.KeptCount(times, KeepRule.Days(30), Now));
    }

    [Fact]
    public void 空列表不保留()
    {
        Assert.Equal(0, RetentionPolicy.KeptCount([], KeepRule.Latest(5), Now));
    }

    [Fact]
    public void 切分三段_保留_可清理_被时间窗口过滤()
    {
        // 保留 2 个；剩下 3 个中，60/100 天的早于 30 天窗口 → 可清理，5 天的新于窗口 → 被过滤
        DateTime[] times = [DaysAgo(1), DaysAgo(2), DaysAgo(5), DaysAgo(60), DaysAgo(100)];

        var split = RetentionPolicy.Split(times, KeepRule.Latest(2), Now, Now.AddDays(-30));

        Assert.Equal(2, split.KeepCount);
        Assert.Equal(2, split.CleanCount);
        Assert.Equal(1, split.FilteredCount);
        Assert.Equal(5, split.Total);
    }

    [Fact]
    public void 时间窗口为全部时不过滤任何条目()
    {
        DateTime[] times = [DaysAgo(1), DaysAgo(2), DaysAgo(3)];

        var split = RetentionPolicy.Split(times, KeepRule.None, Now, windowCutoffUtc: null);

        Assert.Equal(0, split.KeepCount);
        Assert.Equal(3, split.CleanCount);
        Assert.Equal(0, split.FilteredCount);
    }

    [Fact]
    public void 保留天数与时间窗口相同时剩余项都可清理()
    {
        DateTime[] times = [DaysAgo(1), DaysAgo(29), DaysAgo(30), DaysAgo(45)];

        var split = RetentionPolicy.Split(times, KeepRule.Days(30), Now, Now.AddDays(-30));

        Assert.Equal(3, split.KeepCount);      // 1 / 29 / 30 天：正好等于截止时间也算保留
        Assert.Equal(1, split.CleanCount);     // 45 天
        Assert.Equal(0, split.FilteredCount);
    }

    [Fact]
    public void 可清理与可过滤的条目在剩余部分里交错时下标正确()
    {
        // 保留最新的 1 个后，剩下的是「2 天（新，过滤）、100 天（旧，可清理）」交错
        DateTime[] times = [DaysAgo(1), DaysAgo(2), DaysAgo(100), DaysAgo(3)];

        var split = RetentionPolicy.Split(times, KeepRule.Latest(1), Now, Now.AddDays(-30));

        Assert.Equal(1, split.KeepCount);
        Assert.Equal([2], split.CleanIndexes);
        Assert.Equal([1, 3], split.FilteredIndexes);
    }

    [Fact]
    public void FilterStats累计条数与体积()
    {
        var stats = new FilterStats();

        stats.Add(2, 300);
        stats.Add(1, 50);
        stats.Add(0, 999);      // 条数为 0 不计入

        Assert.Equal(3, stats.Count);
        Assert.Equal(350, stats.Bytes);
        Assert.False(stats.IsEmpty);

        stats.Reset();
        Assert.True(stats.IsEmpty);
        Assert.Equal(0, stats.Bytes);
    }
}
