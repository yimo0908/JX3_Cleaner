using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Scanning;

namespace JX3Cleaner.Tests;

/// <summary>
/// 扫描引擎端到端：用临时目录搭出真实结构，验证保留策略、错误账号、
/// 过期区服、时间窗口过滤统计、媒体分类跳过等关键行为。
/// </summary>
public sealed class ScanEngineTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Local);

    private static ScanResult Scan(TempClient client, TimeWindow window = TimeWindow.All,
                                   KeepSettings? keep = null) =>
        new ScanEngine(new RegionConfig()).Scan(client.Root, new ScanOptions
        {
            Now = Now,
            Window = window,
            Keep = keep ?? KeepSettings.Default
        });

    private static ScanNode Category(ScanResult result, CleanCategory id) =>
        result.Categories.Single(c => c.Category == id);

    // ---------------- 茗伊：保留策略 ----------------

    [Fact]
    public void 聊天记录按N保留最新()
    {
        using var client = new TempClient();
        var account = client.Account();

        foreach (var (name, daysAgo) in new[] { ("a.db", 1), ("b.db", 2), ("c.db", 3), ("d.db", 4) })
        {
            client.File($@"interface\my#data\{Path.GetFileName(account)}\userdata\chat_log\{name}",
                100, Now.AddDays(-daysAgo).ToUniversalTime());
        }

        var node = Assert.Single(Category(Scan(client, keep: new KeepSettings { ChatLogCount = 2 }), CleanCategory.ChatLog).Children);

        Assert.Equal("角色甲", node.Title);
        Assert.Equal(4, node.FileCount);
        Assert.Equal(2, node.Targets.Count);
        Assert.Equal("保留最近 2 个", node.Note);
        Assert.Contains(node.Targets, t => t.Path.EndsWith("c.db", StringComparison.Ordinal));
        Assert.Contains(node.Targets, t => t.Path.EndsWith("d.db", StringComparison.Ordinal));
    }

    [Fact]
    public void 聊天记录保留数设为0时全部可清理()
    {
        using var client = new TempClient();
        var account = client.Account();

        for (var i = 1; i <= 3; i++)
        {
            client.File($@"interface\my#data\{Path.GetFileName(account)}\userdata\chat_log\{i}.db",
                100, Now.AddDays(-i).ToUniversalTime());
        }

        var node = Assert.Single(Category(Scan(client, keep: new KeepSettings { ChatLogCount = 0 }), CleanCategory.ChatLog).Children);

        Assert.Equal(3, node.Targets.Count);
        Assert.Null(node.Note);
    }

    [Fact]
    public void 战斗记录按X天保留()
    {
        using var client = new TempClient();
        var account = client.Account();
        var rel = $@"interface\my#data\{Path.GetFileName(account)}\userdata\fight_stat";

        client.File($@"{rel}\new.jx3dat", 100, Now.AddDays(-5).ToUniversalTime());
        client.File($@"{rel}\mid.jx3dat", 100, Now.AddDays(-20).ToUniversalTime());
        client.File($@"{rel}\old.jx3dat", 100, Now.AddDays(-90).ToUniversalTime());

        var node = Assert.Single(Category(Scan(client, keep: new KeepSettings { FightStatDays = 30 }), CleanCategory.FightStat).Children);

        Assert.Equal("保留最近 30 天", node.Note);
        var target = Assert.Single(node.Targets);
        Assert.EndsWith("old.jx3dat", target.Path);
    }

    [Fact]
    public void 时间窗口把较新的条目过滤掉并计入统计()
    {
        using var client = new TempClient();
        var account = client.Account();
        var rel = $@"interface\my#data\{Path.GetFileName(account)}\userdata\chat_log";

        client.File($@"{rel}\a.db", 100, Now.AddDays(-1).ToUniversalTime());    // 保留
        client.File($@"{rel}\b.db", 200, Now.AddDays(-2).ToUniversalTime());    // 保留
        client.File($@"{rel}\c.db", 400, Now.AddDays(-10).ToUniversalTime());   // 新于窗口 → 过滤
        client.File($@"{rel}\d.db", 800, Now.AddDays(-100).ToUniversalTime());  // 可清理

        var result = Scan(client, TimeWindow.Days30, new KeepSettings { ChatLogCount = 2 });

        var node = Assert.Single(Category(result, CleanCategory.ChatLog).Children);
        Assert.Single(node.Targets);
        Assert.EndsWith("d.db", node.Targets[0].Path);

        Assert.Equal(1, result.Filtered.Count);
        Assert.Equal(400, result.Filtered.Bytes);
    }

    [Fact]
    public void 时间窗口为全部时没有过滤统计()
    {
        using var client = new TempClient();
        var account = client.Account();
        client.File($@"interface\my#data\{Path.GetFileName(account)}\userdata\chat_log\a.db", 100,
            Now.AddDays(-1).ToUniversalTime());

        var result = Scan(client);

        Assert.True(result.Filtered.IsEmpty);
    }

    // ---------------- 云团监 ----------------

    [Fact]
    public void 云团监按M组保留()
    {
        using var client = new TempClient();
        const string rel = @"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote";

        for (var i = 1; i <= 4; i++)
        {
            var stamp = Now.AddDays(-i).ToUniversalTime();
            client.File($@"{rel}\r-{i}.jx3dat", 100, stamp);
            client.File($@"{rel}\r-{i}.meta.jx3dat", 50, stamp);
        }

        var node = Category(Scan(client, keep: new KeepSettings { TeamMonGroups = 2 }), CleanCategory.CloudTeamMon);

        Assert.Equal(4, node.Children.Count);

        var retained = node.Children.Where(c => c.Flags.HasFlag(NodeFlags.Retained)).ToList();
        Assert.Equal(2, retained.Count);
        Assert.All(retained, n => Assert.Empty(n.Targets));

        Assert.All(node.Children.Where(c => !c.Flags.HasFlag(NodeFlags.Retained)),
            n => Assert.Equal(2, n.Targets.Count));
    }

    // ---------------- 角色数据 ----------------

    [Fact]
    public void 没有区服子目录的账号标记为错误账号且默认勾选()
    {
        using var client = new TempClient();
        client.Dir(@"userdata\bad-account");

        var node = Assert.Single(Category(Scan(client), CleanCategory.RoleData).Children);

        Assert.True(node.IsInvalidAccount);
        Assert.True(node.DefaultSelected);
        Assert.Contains("错误账号", node.Note);
        Assert.Single(node.Targets);
    }

    [Fact]
    public void 不在白名单的大区标记为过期区服并生成可单独勾选的子节点()
    {
        using var client = new TempClient();
        client.Dir(@"userdata\acc\电信区\示例服\角色甲");
        client.Dir(@"userdata\acc\电信五区\旧服\角色乙");

        var node = Assert.Single(Category(Scan(client), CleanCategory.RoleData).Children);

        Assert.True(node.IsLegacyRegion);
        Assert.Equal("电信五区", node.LegacyDetail);
        Assert.Contains("角色甲", node.RoleSummary);

        var legacyChild = Assert.Single(node.Children);
        Assert.Equal("电信五区", legacyChild.Title);
        Assert.True(legacyChild.IsLegacyRegion);
    }

    [Fact]
    public void userdata下的保留目录不被当作账号()
    {
        using var client = new TempClient();
        client.Dir(@"userdata\config");
        client.Dir(@"userdata\fight_stat");
        client.Dir(@"userdata\role_statistics");

        var result = Scan(client);

        // 三个目录全被跳过 → 没有任何账号节点 → 该分类视为未检测到
        Assert.Contains(CleanCategory.RoleData, result.Missing);
        Assert.DoesNotContain(CleanCategory.RoleData, result.Categories.Select(c => c.Category));
    }

    // ---------------- 分类存在性 / 媒体 ----------------

    [Fact]
    public void 不存在的目录记为Missing且不生成节点()
    {
        using var client = new TempClient();
        var result = Scan(client);

        Assert.Contains(CleanCategory.LazyPlugin, result.Missing);
        Assert.Contains(CleanCategory.LazyPluginData, result.Missing);
        Assert.Contains(CleanCategory.LegacyTeamMon, result.Missing);
        Assert.DoesNotContain(CleanCategory.LazyPlugin, result.Categories.Select(c => c.Category));
    }

    [Fact]
    public void 懒人插件存在时整目录可勾选()
    {
        using var client = new TempClient();
        client.File(@"interface\LR_Plugin\plugin.dll", 512);
        client.File(@"interface\LR_Plugin@DATA\data.dat", 256);

        var result = Scan(client);
        var plugin = Category(result, CleanCategory.LazyPlugin);
        var data = Category(result, CleanCategory.LazyPluginData);

        Assert.Equal("整目录", plugin.Note);
        Assert.True(plugin.DefaultSelected);
        var target = Assert.Single(plugin.Targets);
        Assert.True(target.IsDirectory);
        Assert.Equal(768, data.SizeBytes + plugin.SizeBytes);

        // 已弃用标记随分类定义传到节点，界面据此补黄色「（已弃用）」
        Assert.True(plugin.Deprecated);
        Assert.True(data.Deprecated);
    }

    [Fact]
    public void 旧版团监存在时带已弃用标记_不存在时整条不出现()
    {
        using var absent = new TempClient();
        var missing = Scan(absent);
        Assert.Contains(CleanCategory.LegacyTeamMon, missing.Missing);
        Assert.DoesNotContain(CleanCategory.LegacyTeamMon, missing.Categories.Select(c => c.Category));

        using var present = new TempClient();
        present.File(@"interface\my#data\!all-users@zhcn_hd\userdata\TeamMon\a.jx3dat", 128);

        var node = Category(Scan(present), CleanCategory.LegacyTeamMon);
        Assert.True(node.Deprecated);
        Assert.True(node.DefaultSelected);
    }

    [Fact]
    public void 普通分类不带已弃用标记()
    {
        using var client = new TempClient();
        client.File(@"homelanddir\Blueprints\a.blueprintx", 64);

        Assert.False(Category(Scan(client), CleanCategory.Blueprint).Deprecated);
    }

    [Fact]
    public void 截图与幻境云图不进入数据清理扫描()
    {
        using var client = new TempClient();
        client.File(@"ScreenShot\2026-10-24_20-24-55.jpg", 100);
        client.File(@"DCIM\2026-10-24_20-24-55.jpg", 100);

        var result = Scan(client);

        var ids = result.Categories.Select(c => c.Category).ToList();
        Assert.DoesNotContain(CleanCategory.Screenshot, ids);
        Assert.DoesNotContain(CleanCategory.Dcim, ids);
    }

    [Fact]
    public void 蓝图按文件列出且受时间窗口影响()
    {
        using var client = new TempClient();
        client.File(@"homelanddir\Blueprints\old.blueprintx", 100, Now.AddDays(-200).ToUniversalTime());
        client.File(@"homelanddir\Blueprints\new.blueprintx", 100, Now.AddDays(-2).ToUniversalTime());

        var result = Scan(client, TimeWindow.Days30);
        var category = Category(result, CleanCategory.Blueprint);

        Assert.Equal(2, category.Children.Count);
        var cleanable = category.Children.Where(c => c.Targets.Count > 0).ToList();
        var single = Assert.Single(cleanable);
        Assert.EndsWith("old.blueprintx", single.Title);
        Assert.Equal(1, result.Filtered.Count);
    }

    [Fact]
    public void 扫描结果汇总体积与文件数()
    {
        using var client = new TempClient();
        client.File(@"interface\LR_Plugin\a.dll", 1000);
        client.File(@"interface\LR_Plugin@DATA\b.dat", 500);

        var result = Scan(client);

        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(1500, result.TotalBytes);
        Assert.Equal(0, result.AccountCount);
    }
}
