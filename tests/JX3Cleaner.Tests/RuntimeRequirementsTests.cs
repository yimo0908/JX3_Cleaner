using System.Runtime.InteropServices;
using JX3Cleaner.Core.Environment;

namespace JX3Cleaner.Tests;

/// <summary>启动时的运行环境自检：规则判定 + 探测不炸。</summary>
public sealed class RuntimeRequirementsTests
{
    /// <summary>一台“完全正常”的基线机器：Win11 + .NET 10 桌面运行时 + 框架依赖 + x64。</summary>
    private static RuntimeSnapshot Healthy() => new()
    {
        FrameworkDescription = ".NET 10.0.0",
        RuntimeVersion = new Version(10, 0, 0),
        OsVersion = new Version(10, 0, 22631),
        OsDescription = "Microsoft Windows 11 专业版 10.0.22631",
        IsWindows = true,
        Is64BitProcess = true,
        ProcessArchitecture = Architecture.X64,
        OsArchitecture = Architecture.X64,
        Deployment = DeploymentMode.FrameworkDependent,
        InstalledDesktopRuntimes = [new Version(10, 0, 0)],
        DotNetRoot = @"C:\Program Files\dotnet"
    };

    private static RuntimeCheckResult Check(RuntimeSnapshot snapshot) =>
        RuntimeRequirements.Evaluate(snapshot);

    private static bool Has(RuntimeCheckResult result, string code) =>
        result.Issues.Any(i => i.Code == code);

    // ---------------- 通过场景 ----------------

    [Fact]
    public void 正常机器直接通过()
    {
        var result = Check(Healthy());

        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Contains("自检通过", result.Headline);
        Assert.Contains(result.Issues, i => i.Code == "I-FDD");
    }

    [Fact]
    public void 自包含发布不要求系统安装NET()
    {
        var result = Check(Healthy() with
        {
            Deployment = DeploymentMode.SelfContained,
            InstalledDesktopRuntimes = [],
            DotNetRoot = null
        });

        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Contains(result.Issues, i => i.Code == "I-SC");
    }

    [Fact]
    public void 框架依赖但探不到桌面运行时只记提示不拦截()
    {
        // 能跑到自检说明运行时一定在（否则 WPF 根本加载不了），
        // 装到私有 DOTNET_ROOT 时探不到很常见，绝不能因此拦住用户。
        var result = Check(Healthy() with { InstalledDesktopRuntimes = [], DotNetRoot = null });

        Assert.False(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "W-RT-NOTFOUND");
    }

    // ---------------- 拦截场景 ----------------

    [Fact]
    public void 非Windows直接拦截()
    {
        var result = Check(Healthy() with { IsWindows = false, OsDescription = "Linux 6.8" });

        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "E-OS-NONWIN");
    }

    [Fact]
    public void Windows7版本过低被拦截()
    {
        var result = Check(Healthy() with { OsVersion = new Version(6, 1, 7601) });

        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "E-OS-VER");
    }

    [Fact]
    public void 最低版本1607正好放行()
    {
        var result = Check(Healthy() with { OsVersion = RuntimeRequirements.MinimumWindows });

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void 三十二位进程被拦截()
    {
        var result = Check(Healthy() with { Is64BitProcess = false, ProcessArchitecture = Architecture.X86 });

        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "E-ARCH-BITS");
    }

    [Fact]
    public void 运行时低于目标大版本被拦截并给下载地址()
    {
        var result = Check(Healthy() with
        {
            RuntimeVersion = new Version(8, 0, 0),
            FrameworkDescription = ".NET 8.0.0",
            InstalledDesktopRuntimes = [new Version(8, 0, 0)]
        });

        var issue = Assert.Single(result.Errors);
        Assert.Equal("E-RT-OLD", issue.Code);
        Assert.Equal(RuntimeRequirements.DesktopRuntimeUrl, issue.HelpUrl);
    }

    // ---------------- 提示场景 ----------------

    [Fact]
    public void Win10只提示圆角不生效()
    {
        var result = Check(Healthy() with { OsVersion = new Version(10, 0, 19045) });

        Assert.False(result.HasErrors);
        var issue = Assert.Single(result.Warnings);
        Assert.Equal("W-OS-W10", issue.Code);
    }

    [Fact]
    public void ARM64上模拟执行给提示()
    {
        var result = Check(Healthy() with { OsArchitecture = Architecture.Arm64 });

        Assert.False(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "W-ARCH-EMU");
    }

    [Fact]
    public void ARM64原生进程不算模拟()
    {
        var result = Check(Healthy() with
        {
            OsArchitecture = Architecture.Arm64,
            ProcessArchitecture = Architecture.Arm64
        });

        Assert.DoesNotContain(result.Issues, i => i.Code == "W-ARCH-EMU");
    }

    [Fact]
    public void 借用更高大版本运行时给提示()
    {
        var result = Check(Healthy() with
        {
            RuntimeVersion = new Version(11, 0, 0),
            FrameworkDescription = ".NET 11.0.0",
            InstalledDesktopRuntimes = [new Version(11, 0, 0)]
        });

        Assert.False(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "W-ROLLFWD");
    }

    [Fact]
    public void 形态未知只提示()
    {
        var result = Check(Healthy() with { Deployment = DeploymentMode.Unknown });

        Assert.False(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Code == "W-DEPLOY-UNKNOWN");
    }

    // ---------------- 文本输出 ----------------

    [Fact]
    public void 诊断文本包含关键字段与编号()
    {
        var result = Check(Healthy() with { OsVersion = new Version(6, 1, 7601) });

        var text = result.ToDiagnosticText(new DateTimeOffset(2026, 10, 25, 0, 7, 0, TimeSpan.FromHours(8)));

        Assert.Contains("JX3Cleaner 运行环境自检", text);
        Assert.Contains("2026-10-25 00:07:00", text);
        Assert.Contains(".NET 10.0.0", text);
        Assert.Contains("框架依赖", text);
        Assert.Contains("E-OS-VER", text);
        Assert.Contains("结论：", text);
    }

    [Fact]
    public void 一行摘要随严重程度变化()
    {
        Assert.Contains("正常", Check(Healthy()).OneLine);
        Assert.Contains("项提示", Check(Healthy() with { OsVersion = new Version(10, 0, 19045) }).OneLine);
        Assert.Contains("不满足", Check(Healthy() with { Is64BitProcess = false }).OneLine);
    }

    // ---------------- 探测层 ----------------

    [Theory]
    [InlineData("SelfContained", DeploymentMode.SelfContained)]
    [InlineData("FrameworkDependent", DeploymentMode.FrameworkDependent)]
    [InlineData("", DeploymentMode.Unknown)]
    [InlineData(null, DeploymentMode.Unknown)]
    [InlineData("随便写的", DeploymentMode.Unknown)]
    public void 元数据取值映射(string? value, DeploymentMode expected) =>
        Assert.Equal(expected, RuntimeProbe.InterpretMetadata(value));

    [Fact]
    public void 探测当前进程不抛异常且字段有效()
    {
        var snapshot = RuntimeProbe.Capture();

        Assert.True(snapshot.RuntimeVersion.Major > 0);
        Assert.True(snapshot.OsVersion.Major > 0);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.FrameworkDescription));
        Assert.NotEqual(DeploymentMode.Unknown, snapshot.Deployment);   // 测试宿主一定落在框架依赖
        Assert.DoesNotContain(RuntimeProbe.CandidateDotNetRoots(), string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void 扫描桌面运行时不会抛异常()
    {
        var (versions, _) = RuntimeProbe.FindDesktopRuntimes();

        Assert.NotNull(versions);
        Assert.All(versions, v => Assert.True(v.Major > 0));
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.9\", @"C:\Program Files\dotnet")]
    [InlineData(@"D:\dotnet\shared\Microsoft.NETCore.App\9.0.0", @"D:\dotnet")]
    [InlineData(@"C:\Program Files\dotnet", null)]
    [InlineData("", null)]
    public void 从框架目录反推dotnet根(string path, string? expected) =>
        Assert.Equal(expected, RuntimeProbe.RootFromFrameworkPath(path));
}
