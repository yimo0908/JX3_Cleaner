using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Executing;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;

namespace JX3Cleaner.Core.Auditing;

/// <summary>审计日志：每次清理落一份 JSON 到程序目录 logs\，路径与结果可事后复核。</summary>
public static class AuditWriter
{
    public static string Write(CleanPlan plan, CleanOutcome outcome, ClientRoot root)
    {
        PortablePaths.EnsureDirectories();

        var record = new
        {
            startedAt = DateTimeOffset.Now,
            clientRoot = root.Root,
            mode = outcome.Mode.ToString(),
            summary = new
            {
                entries = plan.TotalEntries,
                files = plan.TotalFiles,
                bytes = plan.TotalBytes,
                succeeded = outcome.Succeeded,
                skipped = outcome.Skipped,
                failed = outcome.Failed,
                freedBytes = outcome.FreedBytes
            },
            rejected = plan.Rejected.Select(r => new { path = r.Path, reason = r.Reason }),
            items = plan.Items.Select(i => new
            {
                path = i.Path,
                bytes = i.SizeBytes,
                isDirectory = i.IsDirectory,
                lastWriteUtc = i.LastWriteUtc
            }),
            failures = outcome.Failures.Select(f => new { path = f.Path, reason = f.Reason })
        };

        var file = Path.Combine(PortablePaths.LogsDir, $"clean-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        JsonIo.Write(file, record);
        return file;
    }
}
