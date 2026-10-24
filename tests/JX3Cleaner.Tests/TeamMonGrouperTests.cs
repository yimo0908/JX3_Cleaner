using JX3Cleaner.Core.Rules;

namespace JX3Cleaner.Tests;

/// <summary>云团监：.jx3dat 与 .meta.jx3dat 成组。</summary>
public sealed class TeamMonGrouperTests
{
    [Theory]
    [InlineData("r-02b1c7bd19102d8f.jx3dat", "r-02b1c7bd19102d8f")]
    [InlineData("r-02b1c7bd19102d8f.meta.jx3dat", "r-02b1c7bd19102d8f")]
    [InlineData("R-02B1C7BD19102D8F.JX3DAT", "r-02b1c7bd19102d8f")]
    public void 分组键去掉两级后缀(string fileName, string expected) =>
        Assert.Equal(expected, TeamMonGrouper.KeyOf(fileName));

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("r-aaa")]
    [InlineData("")]
    public void 非jx3dat后缀没有分组键(string fileName) =>
        Assert.Null(TeamMonGrouper.KeyOf(fileName));

    [Fact]
    public void 本地数据文件不会被当成团监分组()
    {
        // local.jx3dat / metalist.jx3dat 位于 team_mon 根目录而不是 remote，
        // 扫描只枚举 remote，因此这两个文件永远不会进入分组。
        using var client = new TempClient();
        client.File(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\local.jx3dat", 100);
        var remote = client.Dir(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote");

        Assert.Empty(TeamMonGrouper.Group(remote));
    }

    [Fact]
    public void 成对文件归为同一组()
    {
        using var client = new TempClient();
        var dir = client.Dir(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote");

        client.File(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote\r-aaa.jx3dat", 100);
        client.File(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote\r-aaa.meta.jx3dat", 50);

        var groups = TeamMonGrouper.Group(dir);

        var group = Assert.Single(groups);
        Assert.Equal("r-aaa", group.Key);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(150, group.SizeBytes);
        Assert.False(group.IsIncomplete);
    }

    [Fact]
    public void 只有一半文件时标记为不完整()
    {
        using var client = new TempClient();
        var dir = client.Dir(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote");

        client.File(@"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote\r-bbb.jx3dat", 100);

        var group = Assert.Single(TeamMonGrouper.Group(dir));
        Assert.True(group.IsIncomplete);
    }

    [Fact]
    public void 组按修改时间倒序()
    {
        using var client = new TempClient();
        var relative = @"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote";
        var dir = client.Dir(relative);

        var now = DateTime.UtcNow;
        client.File($@"{relative}\r-old.jx3dat", 10, now.AddDays(-10));
        client.File($@"{relative}\r-old.meta.jx3dat", 10, now.AddDays(-10));
        client.File($@"{relative}\r-new.jx3dat", 10, now.AddDays(-1));
        client.File($@"{relative}\r-new.meta.jx3dat", 10, now.AddDays(-1));

        var groups = TeamMonGrouper.Group(dir);

        Assert.Equal(2, groups.Count);
        Assert.Equal("r-new", groups[0].Key);
        Assert.Equal("r-old", groups[1].Key);
    }

    [Fact]
    public void 目录不存在时返回空集合() =>
        Assert.Empty(TeamMonGrouper.Group(@"C:\definitely\not\exists\remote"));
}
