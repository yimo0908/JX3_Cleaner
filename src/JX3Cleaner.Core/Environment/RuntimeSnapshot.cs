using System.Runtime.InteropServices;
using System.Text;

namespace JX3Cleaner.Core.Environment;

/// <summary>程序被打包成哪种形态，决定运行时要不要依赖系统安装的 .NET。</summary>
public enum DeploymentMode
{
    /// <summary>判断不出来（既没写程序集元数据，也拿不到框架程序集路径）。</summary>
    Unknown = 0,

    /// <summary>框架依赖：目标机必须装 .NET 桌面运行时，否则 exe 根本起不来。</summary>
    FrameworkDependent = 1,

    /// <summary>自包含：.NET 已打进产物，目标机无需安装任何运行时。</summary>
    SelfContained = 2
}

public enum RuntimeIssueLevel
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>一条环境自检结论。</summary>
/// <param name="Code">稳定的机器可读编号，便于排查与单测。</param>
/// <param name="Level">Error 会阻止启动，Warning/Info 只作提示。</param>
/// <param name="Title">一句话结论，直接显示在弹窗列表里。</param>
/// <param name="Detail">补充说明或修复建议。</param>
/// <param name="HelpUrl">需要下载/查资料时给可点击链接。</param>
public sealed record RuntimeIssue(
    string Code,
    RuntimeIssueLevel Level,
    string Title,
    string Detail,
    string? HelpUrl = null);

/// <summary>
/// 启动瞬间的运行环境快照。纯数据，便于把 <see cref="RuntimeRequirements.Evaluate"/>
/// 做成可单测的纯函数。
/// </summary>
public sealed record RuntimeSnapshot
{
    /// <summary>形如 ".NET 10.0.0"。</summary>
    public string FrameworkDescription { get; init; } = string.Empty;

    /// <summary>实际运行的 .NET 版本（<see cref="System.Environment.Version"/>）。</summary>
    public Version RuntimeVersion { get; init; } = new(0, 0);

    /// <summary>系统版本，Windows 上即 10.0.xxxxx。</summary>
    public Version OsVersion { get; init; } = new(0, 0);

    /// <summary>形如 "Microsoft Windows 11 专业版 10.0.22631"。</summary>
    public string OsDescription { get; init; } = string.Empty;

    public bool IsWindows { get; init; } = true;

    public bool Is64BitProcess { get; init; } = true;

    public Architecture ProcessArchitecture { get; init; } = Architecture.X64;

    public Architecture OsArchitecture { get; init; } = Architecture.X64;

    public DeploymentMode Deployment { get; init; } = DeploymentMode.Unknown;

    /// <summary>在任何 dotnet 根目录下探测到的 Microsoft.WindowsDesktop.App 版本，倒序。</summary>
    public IReadOnlyList<Version> InstalledDesktopRuntimes { get; init; } = [];

    /// <summary>探测到的 dotnet 根目录（可能为 null，例如自包含运行在没装 .NET 的机器上）。</summary>
    public string? DotNetRoot { get; init; }
}

/// <summary>自检结果：问题列表 + 是否可通过 + 可复制的诊断文本。</summary>
public sealed class RuntimeCheckResult
{
    public required RuntimeSnapshot Snapshot { get; init; }

    public IReadOnlyList<RuntimeIssue> Issues { get; init; } = [];

    public IEnumerable<RuntimeIssue> Errors =>
        Issues.Where(i => i.Level == RuntimeIssueLevel.Error);

    public IEnumerable<RuntimeIssue> Warnings =>
        Issues.Where(i => i.Level == RuntimeIssueLevel.Warning);

    /// <summary>有 Error 时不允许继续启动。</summary>
    public bool HasErrors => Issues.Any(i => i.Level == RuntimeIssueLevel.Error);

    public bool HasWarnings => Issues.Any(i => i.Level == RuntimeIssueLevel.Warning);

    public string Headline => HasErrors
        ? "运行环境不满足要求"
        : HasWarnings ? "运行环境自检通过，有需要留意的地方" : "运行环境自检通过";

    /// <summary>一行摘要，给设置页 / 状态栏用。</summary>
    public string OneLine
    {
        get
        {
            var s = Snapshot;
            var deploy = s.Deployment switch
            {
                DeploymentMode.SelfContained => "自包含",
                DeploymentMode.FrameworkDependent => "框架依赖",
                _ => "形态未知"
            };

            var arch = s.ProcessArchitecture.ToString().ToLowerInvariant();
            var text = $"{s.FrameworkDescription}（{arch}） · {deploy}";

            if (HasErrors) return text + " · 不满足最低要求";
            return HasWarnings ? text + $" · {Warnings.Count()} 项提示" : text + " · 正常";
        }
    }

    /// <summary>完整诊断文本：直接写日志、复制给用户、Diag 输出都用这一份。</summary>
    public string ToDiagnosticText(DateTimeOffset? at = null)
    {
        var s = Snapshot;
        var sb = new StringBuilder();

        sb.AppendLine("JX3Cleaner 运行环境自检");
        sb.AppendLine($"时间：{(at ?? DateTimeOffset.Now):yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"运行时：{s.FrameworkDescription}（{s.RuntimeVersion}，{s.ProcessArchitecture}）");
        sb.AppendLine($"发布形态：{s.Deployment switch
        {
            DeploymentMode.SelfContained => "自包含（已内置 .NET）",
            DeploymentMode.FrameworkDependent => "框架依赖（需要系统安装 .NET 桌面运行时）",
            _ => "未知"
        }}");
        sb.AppendLine($"操作系统：{s.OsDescription}（{s.OsVersion}，{s.OsArchitecture}）");
        sb.AppendLine($"dotnet 根目录：{s.DotNetRoot ?? "未找到"}");
        sb.AppendLine($"已装桌面运行时：{(s.InstalledDesktopRuntimes.Count == 0
            ? "无"
            : string.Join("、", s.InstalledDesktopRuntimes.Select(v => v.ToString())))}");
        sb.AppendLine($"结论：{Headline}");

        foreach (var issue in Issues)
        {
            var tag = issue.Level switch
            {
                RuntimeIssueLevel.Error => "错误",
                RuntimeIssueLevel.Warning => "提示",
                _ => "信息"
            };
            sb.AppendLine($"[{tag}] {issue.Code} {issue.Title}");
            if (!string.IsNullOrWhiteSpace(issue.Detail)) sb.AppendLine($"        {issue.Detail}");
            if (!string.IsNullOrWhiteSpace(issue.HelpUrl)) sb.AppendLine($"        {issue.HelpUrl}");
        }

        return sb.ToString().TrimEnd();
    }
}
