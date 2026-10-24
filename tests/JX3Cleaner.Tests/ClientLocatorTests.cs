using JX3Cleaner.Core.Discovery;

namespace JX3Cleaner.Tests;

/// <summary>客户端目录定位与结构校验。</summary>
public sealed class ClientLocatorTests
{
    [Fact]
    public void 选择客户端根目录可归一化()
    {
        using var client = new TempClient();

        var root = ClientLocator.Normalize(client.RootDir);

        Assert.NotNull(root);
        Assert.Equal(client.Root.ZhcnDir, root!.ZhcnDir);
    }

    [Fact]
    public void 选择bin目录可归一化()
    {
        using var client = new TempClient();

        var root = ClientLocator.Normalize(Path.Combine(client.RootDir, "bin"));

        Assert.NotNull(root);
        Assert.Equal(client.Root.ZhcnDir, root!.ZhcnDir);
    }

    [Fact]
    public void 直接选择zhcn_hd目录可归一化()
    {
        using var client = new TempClient();

        var root = ClientLocator.Normalize(client.Root.ZhcnDir);

        Assert.NotNull(root);
        Assert.Equal(client.Root.Root, root!.Root);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\definitely\not\exists\jx3")]
    public void 非法输入不归一化(string? picked) =>
        Assert.Null(ClientLocator.Normalize(picked));

    [Fact]
    public void 校验通过并列出缺失目录作为软性提示()
    {
        using var client = new TempClient();
        client.File(@"userdata\acc\电信区\服\角色\x.dat");

        var validation = new ClientLocator().Validate(client.RootDir);

        Assert.True(validation.IsValid);
        Assert.NotEmpty(validation.SoftWarnings);

        var warnings = validation.SoftWarnings.Select(w => w.Name).ToList();
        Assert.Contains("懒人插件", warnings);
        Assert.DoesNotContain("角色数据", warnings);
    }

    [Fact]
    public void 缺少bin64时校验失败()
    {
        using var client = new TempClient();
        client.File(@"userdata\acc\电信区\服\角色\x.dat");
        Directory.Delete(Path.Combine(client.Root.ZhcnDir, "bin64"));

        var validation = new ClientLocator().Validate(client.RootDir);

        Assert.False(validation.IsValid);
        Assert.Contains("bin64", validation.HardErrors.First());
    }

    [Fact]
    public void 没有任何可清理目录时校验失败()
    {
        using var client = new TempClient();

        var validation = new ClientLocator().Validate(client.RootDir);

        Assert.False(validation.IsValid);
        Assert.Contains("未检测到任何可清理目录", validation.HardErrors.First());
    }

    [Fact]
    public void 缺失的可清理目录只作为软性提示()
    {
        using var client = new TempClient();
        client.File(@"userdata\acc\电信区\服\角色\x.dat");

        var validation = new ClientLocator().Validate(client.RootDir);

        Assert.True(validation.IsValid);
        Assert.NotEmpty(validation.SoftWarnings);

        var warnings = validation.SoftWarnings.Select(w => w.Name).ToList();
        Assert.Contains("懒人插件", warnings);
        Assert.DoesNotContain("角色数据", warnings);
    }
}
