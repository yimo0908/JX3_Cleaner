using JX3Cleaner.Core.Interop;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;

namespace JX3Cleaner.Core.Executing;

/// <summary>
/// 「移动到回收站」执行器。走 Windows Shell 的 IFileOperation，
/// 支持长路径与目录，删除后可从未回收站还原。
/// </summary>
public sealed class RecycleBinExecutor : ICleanExecutor
{
    public CleanMode Mode => CleanMode.RecycleBin;

    public Task<CleanOutcome> ExecuteAsync(CleanPlan plan,
                                          IProgress<CleanProgress>? progress = null,
                                          CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<CleanOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Shell COM 在 STA 线程上最稳妥
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(Run(plan, progress, ct));
            }
            catch (OperationCanceledException)
            {
                tcs.SetCanceled(ct);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "JX3Cleaner.RecycleBin"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    private static CleanOutcome Run(CleanPlan plan, IProgress<CleanProgress>? progress, CancellationToken ct)
    {
        var total = plan.Items.Count;
        var existing = new List<TargetItem>();

        for (var i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = plan.Items[i];
            progress?.Report(new CleanProgress(i, total, item.Path));

            var exists = item.IsDirectory ? Directory.Exists(item.Path) : File.Exists(item.Path);
            if (!exists) continue;

            FsAttributes.ClearRecursive(item.Path);
            existing.Add(item);
        }

        var skippedItems = plan.Items
            .Where(i => !existing.Contains(i))
            .Select(i => new CleanFailure(i.Path, "条目已不存在"))
            .ToList();

        if (existing.Count == 0)
            return new CleanOutcome(CleanMode.RecycleBin, 0, skippedItems.Count, 0, 0, [], skippedItems);

        var (hr, aborted) = ShellFileOps.DeleteToRecycleBin(existing.Select(i => i.Path));

        var succeeded = 0;
        var failed = 0;
        long freed = 0;
        var failures = new List<CleanFailure>();

        foreach (var item in existing)
        {
            var stillThere = item.IsDirectory ? Directory.Exists(item.Path) : File.Exists(item.Path);
            if (!stillThere)
            {
                succeeded++;
                freed += item.SizeBytes;
            }
            else
            {
                failed++;
                failures.Add(new CleanFailure(item.Path, ShellFileOps.DescribeHResult(hr)));
            }
        }

        if (aborted)
            failures.Add(new CleanFailure("(操作被中止)", "回收站操作被系统中止，部分条目可能未处理"));

        progress?.Report(new CleanProgress(total, total, string.Empty));
        return new CleanOutcome(CleanMode.RecycleBin, succeeded, skippedItems.Count, failed, freed, failures, skippedItems);
    }
}
