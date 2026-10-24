using System.Runtime.InteropServices;

namespace JX3Cleaner.Core.Environment;

/// <summary>
/// 环境自检规则（纯函数）。刻意与探测代码分开：
/// <see cref="RuntimeProbe"/> 负责“看到什么”，这里只负责“看到的东西够不够”。
/// </summary>
public static class RuntimeRequirements
{
    /// <summary>目标框架大版本，跟 csproj 的 net10.0-windows 对齐。</summary>
    public const int TargetMajor = 10;

    /// <summary>.NET 10 / WPF 官方支持的最低 Windows：Windows 10 1607（build 14393）。</summary>
    public static readonly Version MinimumWindows = new(10, 0, 14393);

    /// <summary>Win11 起始版本；低于它只是圆角与新控件视觉不生效，不影响功能。</summary>
    public static readonly Version Windows11 = new(10, 0, 22000);

    public const string DesktopRuntimeUrl = "https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0";

    public static RuntimeCheckResult Evaluate(RuntimeSnapshot s)
    {
        var issues = new List<RuntimeIssue>();

        // ---- 1. 平台与系统版本：不满足就直接拦下 ----
        if (!s.IsWindows)
        {
            issues.Add(new RuntimeIssue(
                "E-OS-NONWIN", RuntimeIssueLevel.Error,
                "当前系统不是 Windows",
                $"本程序依赖 WPF，只能在 Windows 上运行；实际系统：{s.OsDescription}。"));
        }
        else if (s.OsVersion.CompareTo(MinimumWindows) < 0)
        {
            issues.Add(new RuntimeIssue(
                "E-OS-VER", RuntimeIssueLevel.Error,
                "Windows 版本过低",
                $"检测到 {VersionText(s.OsVersion)}，最低需要 Windows 10 1607（{VersionText(MinimumWindows)}）。"));
        }
        else if (s.OsVersion.CompareTo(Windows11) < 0)
        {
            issues.Add(new RuntimeIssue(
                "W-OS-W10", RuntimeIssueLevel.Warning,
                "当前系统低于 Windows 11",
                $"检测到 {VersionText(s.OsVersion)}：圆角窗口等 Win11 视觉不会生效，功能不受影响。"));
        }

        // ---- 2. 位数与架构 ----
        if (!s.Is64BitProcess)
        {
            issues.Add(new RuntimeIssue(
                "E-ARCH-BITS", RuntimeIssueLevel.Error,
                "程序以 32 位方式运行",
                "本程序只提供 win-x64 构建，请使用 JX3Cleaner.exe（x64）而不是被重定向的副本。"));
        }

        if (s.OsArchitecture == Architecture.Arm64 && s.ProcessArchitecture != Architecture.Arm64)
        {
            issues.Add(new RuntimeIssue(
                "W-ARCH-EMU", RuntimeIssueLevel.Warning,
                "正在 ARM64 系统上通过模拟运行",
                "x64 程序在 ARM64 Windows 上走二进制翻译，扫描与缩略图会明显变慢。"));
        }

        // ---- 3. 发布形态决定「要不要系统装 .NET」 ----
        switch (s.Deployment)
        {
            case DeploymentMode.SelfContained:
                issues.Add(new RuntimeIssue(
                    "I-SC", RuntimeIssueLevel.Info,
                    "已内置 .NET 运行时",
                    "自包含发布，目标机无需安装任何 .NET 运行时。"));
                break;

            case DeploymentMode.FrameworkDependent:
                if (s.RuntimeVersion.Major < TargetMajor)
                {
                    issues.Add(new RuntimeIssue(
                        "E-RT-OLD", RuntimeIssueLevel.Error,
                        ".NET 版本低于程序要求",
                        $"程序面向 .NET {TargetMajor}，当前运行时为 {s.RuntimeVersion}。",
                        DesktopRuntimeUrl));
                }
                else if (s.RuntimeVersion.Major > TargetMajor)
                {
                    issues.Add(new RuntimeIssue(
                        "W-ROLLFWD", RuntimeIssueLevel.Warning,
                        "正在更高版本的 .NET 上运行",
                        $"程序面向 .NET {TargetMajor}，实际由 {s.RuntimeVersion} 接管（RollForward）。"
                        + "功能一般正常，若遇到异常界面行为可安装 .NET 10 桌面运行时。",
                        DesktopRuntimeUrl));
                }

                // 能跑到这里说明桌面运行时一定在（否则 WPF 根本加载不了）；
                // 探不到只说明装在了非常规位置，不能因此拦住用户。
                if (s.InstalledDesktopRuntimes.Count == 0)
                {
                    issues.Add(new RuntimeIssue(
                        "W-RT-NOTFOUND", RuntimeIssueLevel.Warning,
                        "未在常规位置找到 .NET 桌面运行时",
                        "程序仍能运行（可能使用了私有 DOTNET_ROOT），仅作记录。"));
                }
                else
                {
                    issues.Add(new RuntimeIssue(
                        "I-FDD", RuntimeIssueLevel.Info,
                        "已检测到 .NET 桌面运行时",
                        "版本：" + string.Join("、", s.InstalledDesktopRuntimes.Select(v => v.ToString()))));
                }
                break;

            default:
                issues.Add(new RuntimeIssue(
                    "W-DEPLOY-UNKNOWN", RuntimeIssueLevel.Warning,
                    "无法判断是否为自包含发布",
                    "缺少程序集元数据且框架路径不可读；自检结果仅供参考。"));
                break;
        }

        return new RuntimeCheckResult { Snapshot = s, Issues = issues };
    }

    private static string VersionText(Version v) =>
        v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
}
