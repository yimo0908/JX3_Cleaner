using System.Diagnostics;
using System.Text;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;

namespace JX3Cleaner.Core.Executing;

public sealed record CleanProgress(int Done, int Total, string CurrentPath);

public sealed record CleanFailure(string Path, string Reason);

public sealed record CleanOutcome(
    CleanMode Mode,
    int Succeeded,
    int Skipped,
    int Failed,
    long FreedBytes,
    List<CleanFailure> Failures,
    List<CleanFailure> SkippedItems);

public interface ICleanExecutor
{
    CleanMode Mode { get; }

    Task<CleanOutcome> ExecuteAsync(CleanPlan plan,
                                   IProgress<CleanProgress>? progress = null,
                                   CancellationToken ct = default);
}

internal static class FsAttributes
{
    /// <summary>递归清除只读 / 隐藏 / 系统属性，否则 del 与回收站都可能失败。</summary>
    public static void ClearRecursive(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                return;
            }
            if (!Directory.Exists(path)) return;

            foreach (var f in Directory.EnumerateFiles(path, "*", new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = true,
                         AttributesToSkip = FileAttributes.ReparsePoint
                     }))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
            }
            try { File.SetAttributes(path, FileAttributes.Directory); } catch { }
        }
        catch { /* 属性清理失败不阻断，交给后续删除流程判定 */ }
    }
}

/// <summary>
/// 「直接删除」执行器：调用 cmd.exe del。
/// 注意：del 只能删文件，目录必须配合 rmdir；del 对不存在的文件也返回 0，
/// 因此成功与否以「执行后条目是否消失」为准，而非退出码。
/// </summary>
public sealed class DirectDeleteExecutor : ICleanExecutor
{
    private static readonly Encoding CmdEncoding = ResolveCmdEncoding();

    static DirectDeleteExecutor()
    {
        // .NET 默认不带 GBK(936)，需要注册代码页提供程序，否则读 cmd 输出会抛异常
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { /* 已注册或不可用时忽略 */ }
    }

    private static Encoding ResolveCmdEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(936);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    public CleanMode Mode => CleanMode.DirectDelete;

    public async Task<CleanOutcome> ExecuteAsync(CleanPlan plan,
                                                IProgress<CleanProgress>? progress = null,
                                                CancellationToken ct = default)
    {
        var succeeded = 0;
        var skipped = 0;
        var failed = 0;
        long freed = 0;
        var failures = new List<CleanFailure>();
        var skippedItems = new List<CleanFailure>();
        var total = plan.Items.Count;

        for (var i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = plan.Items[i];
            progress?.Report(new CleanProgress(i, total, item.Path));

            if (!Exists(item))
            {
                skipped++;
                skippedItems.Add(new CleanFailure(item.Path, "条目已不存在"));
                continue;
            }

            FsAttributes.ClearRecursive(item.Path);

            try
            {
                var (exitCode, stderr) = await RunDelAsync(item, ct);

                if (!Exists(item))
                {
                    succeeded++;
                    freed += item.SizeBytes;
                }
                else
                {
                    failed++;
                    failures.Add(new CleanFailure(item.Path,
                        string.IsNullOrWhiteSpace(stderr)
                            ? $"删除未生效（cmd 退出码 {exitCode}），可能被占用或无权限"
                            : stderr.Trim()));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                failures.Add(new CleanFailure(item.Path, ex.Message));
            }
        }

        progress?.Report(new CleanProgress(total, total, string.Empty));
        return new CleanOutcome(Mode, succeeded, skipped, failed, freed, failures, skippedItems);
    }

    private static bool Exists(TargetItem item) =>
        item.IsDirectory ? Directory.Exists(item.Path) : File.Exists(item.Path);

    private static async Task<(int ExitCode, string Stderr)> RunDelAsync(TargetItem item, CancellationToken ct)
    {
        var lastExit = 0;
        var stderr = string.Empty;

        if (item.IsDirectory)
        {
            // del 只能删文件，目录还要 rmdir；分两次调用比用 & 串联更可控
            var r1 = await RunCmdAsync(ct, "del", "/f", "/q", "/s", Path.Combine(item.Path, "*"));
            lastExit = r1.ExitCode;
            stderr = r1.Stderr;

            if (Directory.Exists(item.Path))
            {
                var r2 = await RunCmdAsync(ct, "rmdir", "/s", "/q", item.Path);
                lastExit = r2.ExitCode;
                if (r2.Stderr.Length > 0) stderr = r2.Stderr;
            }
        }
        else
        {
            var r = await RunCmdAsync(ct, "del", "/f", "/q", item.Path);
            lastExit = r.ExitCode;
            stderr = r.Stderr;
        }

        return (lastExit, stderr);
    }

    /// <summary>
    /// 参数逐个交给 ArgumentList，由运行时负责引号，
    /// 绝不用字符串拼接 —— 路径里的空格、&amp;、^ 由各参数边界隔离。
    /// </summary>
    private static async Task<(int ExitCode, string Stderr)> RunCmdAsync(CancellationToken ct, params string[] tokens)
    {
        var psi = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = CmdEncoding,
            StandardErrorEncoding = CmdEncoding
        };

        psi.ArgumentList.Add("/d");   // 跳过 AutoRun
        psi.ArgumentList.Add("/c");
        foreach (var t in tokens) psi.ArgumentList.Add(t);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 cmd.exe");
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;
        _ = await stdoutTask;

        return (proc.ExitCode, stderr);
    }
}
