using JX3Cleaner.Core.Rules;

namespace JX3Cleaner.Tests;

/// <summary>账号目录解析：目录名识别、展示名来源、固定子目录排除。</summary>
public sealed class AccountResolverTests
{
    [Theory]
    [InlineData("1000000000000000001@zhcn_hd", "1000000000000000001")]
    [InlineData("1000000000000000001@zhcn", "1000000000000000001")]
    [InlineData("123456@ZHCN_HD", "123456")]                  // 大小写不敏感
    public void 账号目录名可解析出global_id(string dirName, string expected)
    {
        Assert.True(AccountResolver.TryParseAccountDir(dirName, out var gid));
        Assert.Equal(expected, gid);
    }

    [Theory]
    [InlineData("!all-users@zhcn_hd")]      // 公共数据目录
    [InlineData("#cache")]                  // 缓存目录
    [InlineData("#电信区@zhcn_hd")]          // 区服缓存目录
    [InlineData("12345@zhcn_hd")]           // 位数不足
    [InlineData("abcdef@zhcn_hd")]
    [InlineData("1000000000000000001@other")]
    public void 非账号目录被识别为非账号(string dirName) =>
        Assert.False(AccountResolver.TryParseAccountDir(dirName, out _));

    [Fact]
    public void 枚举账号目录时跳过公共与缓存目录()
    {
        using var client = new TempClient();
        var myData = client.Dir(@"interface\my#data");

        client.Account("1000000000000000001", "角色甲");
        client.Account("1000000000000000002", "角色乙");
        Directory.CreateDirectory(Path.Combine(myData, "!all-users@zhcn_hd"));
        Directory.CreateDirectory(Path.Combine(myData, "#cache"));
        Directory.CreateDirectory(Path.Combine(myData, "#电信区@zhcn_hd"));

        var dirs = AccountResolver.EnumerateAccountDirs(myData).Select(Path.GetFileName).ToList();

        Assert.Equal(2, dirs.Count);
        Assert.Contains("1000000000000000001@zhcn_hd", dirs);
        Assert.Contains("1000000000000000002@zhcn_hd", dirs);
    }

    [Fact]
    public void 界面展示名取玩家名空目录()
    {
        using var client = new TempClient();
        var account = client.Account("1000000000000000001", "角色甲");

        var identity = new AccountResolver().Resolve(account);

        Assert.Equal("角色甲", identity.Display);
        Assert.Equal("1000000000000000001", identity.GlobalId);
        Assert.False(identity.IsUnknown);
    }

    [Fact]
    public void 多个玩家名以顿号连接_超过三个折叠()
    {
        using var client = new TempClient();
        var account = client.Account("1000000000000000001", "一");
        foreach (var name in new[] { "二", "三", "四", "五" })
            Directory.CreateDirectory(Path.Combine(account, name));

        var identity = new AccountResolver().Resolve(account);

        // 目录枚举顺序由文件系统决定，只断言数量与折叠规则
        Assert.Equal(5, identity.Candidates.Count);
        Assert.EndsWith("+2", identity.Display);
        Assert.Equal(identity.Display, identity.RoleSummary);
        Assert.Equal(2, identity.Display.Count(c => c == '、'));
    }

    [Fact]
    public void 固定子目录与纯数字目录不算玩家名()
    {
        using var client = new TempClient();
        var account = client.Account("1000000000000000001", playerName: "");

        foreach (var fixedDir in new[] { "audio", "cache", "config", "data", "logs", "userdata" })
            Directory.CreateDirectory(Path.Combine(account, fixedDir));

        Directory.CreateDirectory(Path.Combine(account, "1000000000000000001"));  // 与 gid 同名的空目录

        var identity = new AccountResolver().Resolve(account);

        Assert.True(identity.IsUnknown);
        Assert.StartsWith("未知账号", identity.Display);
        Assert.Contains("0001", identity.Display);      // 用 global_id 后四位兜底
    }

    [Fact]
    public void 账号目录不存在时按未知账号处理()
    {
        var identity = new AccountResolver().Resolve(@"C:\definitely\not\exists\1234567890@zhcn_hd");

        Assert.True(identity.IsUnknown);
        Assert.Equal("1234567890", identity.GlobalId);
    }
}
