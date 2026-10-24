using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;
using JX3Cleaner.Core.Rules;

namespace JX3Cleaner.Tests;

/// <summary>清理计划构建：去重、父子包含收敛、路径守卫拦截。</summary>
public sealed class CleanPlanBuilderTests
{
    private static ScanNode FileNode(string path, long size = 100) => new()
    {
        Kind = NodeKind.File,
        Title = Path.GetFileName(path),
        FullPath = path,
        SizeBytes = size,
        Targets = { new TargetItem(path, size, DateTime.UtcNow, false) }
    };

    private static ScanNode DirNode(string path, long size = 100) => new()
    {
        Kind = NodeKind.Account,
        Title = Path.GetFileName(path),
        FullPath = path,
        SizeBytes = size,
        Targets = { new TargetItem(path, size, DateTime.UtcNow, true) }
    };

    [Fact]
    public void 重复条目只保留一份()
    {
        using var client = new TempClient();
        var path = client.File(@"userdata\acc\a.dat");

        var plan = CleanPlanBuilder.Build([FileNode(path), FileNode(path)], new PathGuard(client.Root));

        Assert.Single(plan.Items);
        Assert.Empty(plan.Rejected);
    }

    [Fact]
    public void 父目录覆盖子文件时不重复删除()
    {
        using var client = new TempClient();
        var child = client.File(@"userdata\acc\sub\a.dat");
        var parent = Path.GetDirectoryName(Path.GetDirectoryName(child))!;   // …\userdata\acc

        var plan = CleanPlanBuilder.Build([DirNode(parent), FileNode(child)], new PathGuard(client.Root));

        var item = Assert.Single(plan.Items);
        Assert.True(item.IsDirectory);
        Assert.Equal(parent, item.Path);
    }

    [Fact]
    public void 子节点条目会随父节点递归收集()
    {
        using var client = new TempClient();
        var a = client.File(@"userdata\acc\a.dat");
        var b = client.File(@"userdata\acc\b.dat");

        var root = new ScanNode { Kind = NodeKind.Category, Title = "分类" };
        root.Add(FileNode(a)).Add(FileNode(b));

        var plan = CleanPlanBuilder.Build([root], new PathGuard(client.Root));

        Assert.Equal(2, plan.Items.Count);
        Assert.Equal(200, plan.TotalBytes);
        Assert.Equal(2, plan.TotalFiles);
    }

    [Fact]
    public void 越界路径被拒绝并给出原因()
    {
        using var client = new TempClient();
        var outside = Path.GetTempFileName();

        try
        {
            var plan = CleanPlanBuilder.Build([FileNode(outside)], new PathGuard(client.Root));

            Assert.Empty(plan.Items);
            var rejected = Assert.Single(plan.Rejected);
            Assert.Contains("不在客户端", rejected.Reason);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void 受保护文件即使被勾选也不会进计划()
    {
        using var client = new TempClient();
        var local = client.File(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\local.jx3dat");

        var plan = CleanPlanBuilder.Build([FileNode(local)], new PathGuard(client.Root));

        Assert.Empty(plan.Items);
        Assert.Contains("受保护", Assert.Single(plan.Rejected).Reason);
    }
}
public sealed class PathGuardTests
{
    [Fact]
    public void 客户端目录内的普通文件通过()
    {
        using var client = new TempClient();
        var file = client.File(@"userdata\acc\a.dat");

        Assert.True(new PathGuard(client.Root).Check(file).Ok);
    }

    [Fact]
    public void 客户端目录之外的路径被拦截()
    {
        using var client = new TempClient();
        var guard = new PathGuard(client.Root);

        Assert.False(guard.Check(@"C:\Windows\System32\drivers\etc\hosts").Ok);
        Assert.False(guard.Check(Path.Combine(Path.GetTempPath(), "somewhere", "x.dat")).Ok);
    }

    [Fact]
    public void zhcn_hd根目录与关键父目录被拦截()
    {
        using var client = new TempClient();
        var guard = new PathGuard(client.Root);

        Assert.False(guard.Check(client.Root.ZhcnDir).Ok);
        Assert.False(guard.Check(client.Root.UserDataDir).Ok);
        Assert.False(guard.Check(client.Root.MyDataDir).Ok);
    }

    [Theory]
    [InlineData(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\local.jx3dat")]
    [InlineData(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\metalist.jx3dat")]
    public void 云团监本地数据被保护(string relative)
    {
        using var client = new TempClient();
        var file = client.File(relative);

        Assert.False(new PathGuard(client.Root).Check(file).Ok);
    }

    [Fact]
    public void 上跳路径被规范化后依然拦截()
    {
        using var client = new TempClient();
        var sneaky = Path.Combine(client.Root.ZhcnDir, "userdata", "..", "..", "..", "Windows", "win.ini");

        Assert.False(new PathGuard(client.Root).Check(sneaky).Ok);
    }
}

/// <summary>分类目录与文件名正则（金团记录 exclude current.gkp.jx3dat）。</summary>
public sealed class CategoryCatalogTests
{
    private static bool Matches(CleanCategory id, string fileName)
    {
        var def = CategoryCatalog.Get(id);
        return def.NamePattern is null || System.Text.RegularExpressions.Regex.IsMatch(fileName, def.NamePattern);
    }

    [Fact]
    public void 金团记录只匹配带时间戳的文件()
    {
        Assert.True(Matches(CleanCategory.Gkp, "2021-08-07-21-45-33_25人普通白帝江关.gkp.jx3dat"));
        Assert.True(Matches(CleanCategory.Gkp, "2026-10-24-20-00-01_英雄范阳夜变.gkp.jx3dat"));
    }

    [Theory]
    [InlineData("current.gkp.jx3dat")]                  // 当前正在记录，不能删
    [InlineData("2021-8-7-21-45-33_x.gkp.jx3dat")]      // 月份未补零，不是游戏命名格式
    [InlineData("random.jx3dat")]
    public void 金团记录排除非时间戳文件(string fileName) =>
        Assert.False(Matches(CleanCategory.Gkp, fileName));

    [Fact]
    public void 截图与云图只由图片页处理()
    {
        Assert.True(CategoryCatalog.IsMediaOnly(CleanCategory.Screenshot));
        Assert.True(CategoryCatalog.IsMediaOnly(CleanCategory.Dcim));

        var dataTab = CategoryCatalog.GroupOrder
            .SelectMany(g => CategoryCatalog.OfDataTab(g.Group))
            .Select(c => c.Id)
            .ToList();

        Assert.DoesNotContain(CleanCategory.Screenshot, dataTab);
        Assert.DoesNotContain(CleanCategory.Dcim, dataTab);
    }

    [Fact]
    public void 三组分类标题与顺序()
    {
        Assert.Equal(["茗伊插件", "懒人插件", "游戏数据"],
            CategoryCatalog.GroupOrder.Select(g => g.Title).ToArray());

        Assert.Equal("懒人插件", CategoryCatalog.GroupTitle(CategoryGroup.Lazy));
    }

    [Fact]
    public void 每个分类的相对路径都不以分隔符开头()
    {
        foreach (var def in CategoryCatalog.Default)
        {
            Assert.False(def.RelativePath.StartsWith('\\'), def.Name);
            Assert.False(def.RelativePath.StartsWith('/'), def.Name);
        }
    }

    // ---------------- 已弃用分类（懒人插件两类 + 旧版团监） ----------------

    /// <summary>只有这三类会被标「已弃用」——改动时必须同步更新，避免误加或漏加。</summary>
    private static readonly CleanCategory[] DeprecatedIds =
    [
        CleanCategory.LazyPlugin,
        CleanCategory.LazyPluginData,
        CleanCategory.LegacyTeamMon
    ];

    [Fact]
    public void 已弃用分类恰好是懒人插件两类与旧版团监()
    {
        var actual = CategoryCatalog.Default
            .Where(c => c.Deprecated)
            .Select(c => c.Id.ToString())
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        var expected = DeprecatedIds
            .Select(id => id.ToString())
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(CleanCategory.LazyPlugin)]
    [InlineData(CleanCategory.LazyPluginData)]
    [InlineData(CleanCategory.LegacyTeamMon)]
    public void 已弃用分类是整目录粒度_靠目录存在性决定是否出现(CleanCategory id)
    {
        var def = CategoryCatalog.Get(id);

        // 整目录 + RequireExists：目录不存在时扫描不产出节点，界面两栏自然都不显示
        Assert.Equal(Granularity.Directory, def.Granularity);
        Assert.True(def.RequireExists);
    }

    [Fact]
    public void 已弃用后缀文案固定()
    {
        Assert.Equal("（已弃用）", CategoryDefinition.DeprecatedLabel);
    }

    [Fact]
    public void 未标记的分类不会被误判为已弃用()
    {
        var notDeprecated = CategoryCatalog.Default
            .Where(c => !DeprecatedIds.Contains(c.Id))
            .ToList();

        Assert.NotEmpty(notDeprecated);
        Assert.All(notDeprecated, d => Assert.False(d.Deprecated));
    }
}
