using System.Text;
using JX3Cleaner.Core;
using JX3Cleaner.Core.Environment;
using JX3Cleaner.Core.Executing;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;
using JX3Cleaner.Core.Scanning;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        JX3Cleaner 诊断工具

          scan      --root <客户端根目录> [--days 30] [--detail]
                    扫描并打印结构报告（不修改任何文件）

          plan      --root <客户端根目录> [--days 30]
                    在 scan 基础上生成清理计划（仍不删除）

          selftest  在临时目录里验证回收站与 direct delete 两条执行链路

          env       打印运行环境自检报告（.NET 版本、发布形态、系统版本）
        """);
    return 0;
}

var cmd = args[0];
var rootArg = Arg(args, "--root");
var days = int.TryParse(Arg(args, "--days"), out var d) ? d : 0;
var detail = args.Contains("--detail");
var window = days switch
{
    7 => TimeWindow.Days7,
    30 => TimeWindow.Days30,
    90 => TimeWindow.Days90,
    180 => TimeWindow.Days180,
    365 => TimeWindow.Days365,
    _ => TimeWindow.All
};

return cmd switch
{
    "scan" => Scan(rootArg, window, detail, plan: false),
    "plan" => Scan(rootArg, window, detail, plan: true),
    "selftest" => await SelfTest(),
    "env" => EnvCheck(),
    _ => Fail($"未知命令：{cmd}")
};

static int EnvCheck()
{
    var result = RuntimeRequirements.Evaluate(RuntimeProbe.Capture());
    Console.WriteLine(result.ToDiagnosticText());
    return result.HasErrors ? 2 : 0;
}

static string? Arg(string[] a, string name)
{
    var i = Array.IndexOf(a, name);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

static int Fail(string msg)
{
    Console.WriteLine($"✗ {msg}");
    return 1;
}

static int Scan(string? root, TimeWindow window, bool detail, bool plan)
{
    var service = new CleanerService();
    service.Initialize();

    if (string.IsNullOrWhiteSpace(root))
    {
        root = service.AutoDetect() ?? service.Settings.Current.ClientRoot;
        if (string.IsNullOrWhiteSpace(root)) return Fail("未指定 --root，且自动探测失败");
        Console.WriteLine($"自动探测：{root}");
    }

    var validation = service.Validate(root);
    Console.WriteLine($"目录：{validation.Root?.Root}");
    Console.WriteLine($"校验：{(validation.IsValid ? "✓ 通过" : "✗ 失败")}  {validation.Summary}");
    if (!validation.IsValid) return 1;

    foreach (var c in validation.Checks)
        Console.WriteLine($"   {(c.Exists ? "✓" : "·")} {c.Name,-22} {c.Path}");

    Console.WriteLine();
    var result = service.Scan(new ScanOptions { Window = window, Now = DateTime.Now });
    Console.WriteLine($"账号目录：{result.AccountCount}    耗时：{result.Elapsed.TotalMilliseconds:F0} ms");
    Console.WriteLine($"可识别总量：{TargetItem.FormatSize(result.TotalBytes)}  /  {result.TotalFiles} 文件");
    Console.WriteLine();

    foreach (var cat in result.Categories)
    {
        Console.WriteLine($"■ {cat.Title}  —  {TargetItem.FormatSize(cat.SizeBytes)} / {cat.FileCount} 文件 / {cat.Children.Count} 项");
        if (detail) PrintNodes(cat.Children, 1);
        else PrintSummaryLine(cat);
    }

    if (result.Missing.Count > 0)
        Console.WriteLine($"\n未检测到的分类：{string.Join("、", result.Missing)}");

    if (!result.Filtered.IsEmpty)
        Console.WriteLine($"\n时间窗口过滤：{result.Filtered.Count} 项，约 {TargetItem.FormatSize(result.Filtered.Bytes)}");

    foreach (var w in result.Warnings) Console.WriteLine($"! {w}");

    Console.WriteLine($"\n默认勾选可释放：{TargetItem.FormatSize(result.DefaultSelectedBytes)}");

    if (plan)
    {
        var selected = result.Categories
            .SelectMany(c => new[] { c }.Concat(c.Descendants()))
            .Where(n => n.DefaultSelected && n.Targets.Count > 0);

        var built = service.BuildPlan(selected);
        Console.WriteLine($"\n清理计划：{built.SummaryText}   被拦截 {built.Rejected.Count} 项");
        foreach (var r in built.Rejected.Take(10)) Console.WriteLine($"   ✗ {r.Reason}  {r.Path}");
    }

    return 0;
}

static void PrintNodes(IEnumerable<ScanNode> nodes, int depth)
{
    foreach (var n in nodes)
    {
        var indent = new string(' ', depth * 2);
        var flag = n.Flags.HasFlag(NodeFlags.InvalidAccount) ? " [错误账号]"
                 : n.Flags.HasFlag(NodeFlags.LegacyRegion) ? " [过期区服]"
                 : n.Flags.HasFlag(NodeFlags.IncompleteGroup) ? " [不完整]"
                 : string.Empty;

        Console.WriteLine($"{indent}{n.Title}{flag}  ({TargetItem.FormatSize(n.SizeBytes)}/{n.FileCount})"
                          + (n.Note is null ? "" : $"  {n.Note}")
                          + (n.DefaultSelected ? "  ✓默认" : ""));

        if (n.Children.Count > 0) PrintNodes(n.Children, depth + 1);
    }
}

static void PrintSummaryLine(ScanNode cat)
{
    if (cat.Children.Count == 0) return;

    var sample = cat.Children.Take(6)
        .Select(c => c.RoleSummary is { Length: > 0 } r ? $"{c.Title}（{r}）" : c.Title);
    Console.WriteLine($"   {string.Join("  |  ", sample)}"
                      + (cat.Children.Count > 6 ? $"  … 共 {cat.Children.Count} 项" : ""));
}

static async Task<int> SelfTest()
{
    var baseDir = Path.Combine(Path.GetTempPath(), "jx3cleaner-selftest-" + Guid.NewGuid().ToString("N")[..6]);
    var zhcn = Path.Combine(baseDir, "bin", "zhcn_hd");

    Directory.CreateDirectory(Path.Combine(zhcn, "bin64"));
    Directory.CreateDirectory(Path.Combine(zhcn, "interface", "my#data", "1000000000000000001@zhcn_hd", "userdata", "chat_log"));
    Directory.CreateDirectory(Path.Combine(zhcn, "userdata", "testaccount", "电信区", "测试服", "测试角色"));

    var f1 = Path.Combine(zhcn, "interface", "my#data", "1000000000000000001@zhcn_hd", "userdata", "chat_log", "a.db");
    var f2 = Path.Combine(zhcn, "interface", "my#data", "1000000000000000001@zhcn_hd", "userdata", "chat_log", "b.db");
    await File.WriteAllTextAsync(f1, new string('x', 4096));
    await File.WriteAllTextAsync(f2, new string('y', 2048));

    var locator = new JX3Cleaner.Core.Discovery.ClientLocator();
    var v = locator.Validate(baseDir);
    Console.WriteLine($"校验：{(v.IsValid ? "✓" : "✗")} {v.Summary}");
    if (!v.IsValid) return 1;

    var guard = new PathGuard(v.Root!);
    Console.WriteLine($"越界路径拦截：{(guard.Check(@"C:\Windows\System32\drivers\etc\hosts").Ok ? "✗ 未拦截" : "✓ 已拦截")}");
    Console.WriteLine($"根目录拦截：{(guard.Check(zhcn).Ok ? "✗ 未拦截" : "✓ 已拦截")}");

    var planRecycle = CleanPlanBuilder.Build(
    [
        new ScanNode
        {
            Kind = NodeKind.File, Title = "a.db",
            Targets = { new TargetItem(f1, 4096, DateTime.UtcNow, false) }
        }
    ], guard);

    var outcome1 = await new RecycleBinExecutor().ExecuteAsync(planRecycle);
    Console.WriteLine($"回收站：成功 {outcome1.Succeeded} 失败 {outcome1.Failed} → 文件{(File.Exists(f1) ? "仍在 ✗" : "已移除 ✓")}");
    foreach (var f in outcome1.Failures) Console.WriteLine($"   ✗ {f.Reason}  {f.Path}");

    var planDel = CleanPlanBuilder.Build(
    [
        new ScanNode
        {
            Kind = NodeKind.File, Title = "b.db",
            Targets = { new TargetItem(f2, 2048, DateTime.UtcNow, false) }
        }
    ], guard);

    var outcome2 = await new DirectDeleteExecutor().ExecuteAsync(planDel);
    Console.WriteLine($"直接删除：成功 {outcome2.Succeeded} 失败 {outcome2.Failed} → 文件{(File.Exists(f2) ? "仍在 ✗" : "已移除 ✓")}");
    foreach (var f in outcome2.Failures) Console.WriteLine($"   ✗ {f.Reason}  {f.Path}");

    var target = Path.Combine(zhcn, "userdata", "testaccount", "电信区", "测试服", "测试角色");
    var planDir = CleanPlanBuilder.Build(
    [
        new ScanNode
        {
            Kind = NodeKind.Item, Title = "测试角色",
            Targets = { new TargetItem(target, 0, DateTime.UtcNow, true) }
        }
    ], guard);
    var outcome3 = await new RecycleBinExecutor().ExecuteAsync(planDir);
    Console.WriteLine($"回收站删目录：成功 {outcome3.Succeeded} 失败 {outcome3.Failed} → 目录{(Directory.Exists(target) ? "仍在 ✗" : "已移除 ✓")}");

    try { Directory.Delete(baseDir, true); } catch { }
    Console.WriteLine($"\n临时目录已清理：{baseDir}");
    return 0;
}
