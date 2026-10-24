using System.Text.RegularExpressions;
using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Rules;

namespace JX3Cleaner.Core.Scanning;

public sealed class ScanOptions
{
    public TimeWindow Window { get; init; } = TimeWindow.All;

    public DateTime Now { get; init; } = DateTime.Now;

    /// <summary>五类数据的保留策略（设置页可调）。</summary>
    public KeepSettings Keep { get; init; } = KeepSettings.Default;

    /// <summary>单分类最多枚举的文件节点数，防止海量文件拖垮界面。</summary>
    public int MaxFileNodes { get; init; } = 800;

    /// <summary>时间窗口对应的截止时间；All 返回 null 表示不限制。</summary>
    public DateTime? Cutoff => Window switch
    {
        TimeWindow.Days7 => Now.AddDays(-7),
        TimeWindow.Days30 => Now.AddDays(-30),
        TimeWindow.Days90 => Now.AddDays(-90),
        TimeWindow.Days180 => Now.AddDays(-180),
        TimeWindow.Days365 => Now.AddDays(-365),
        _ => null
    };

    /// <summary>截止时间的 UTC 形式：文件时间戳都是 UTC，比较前必须换算。</summary>
    public DateTime? CutoffUtc => Cutoff?.ToUniversalTime();

    public bool WithinWindow(DateTime utc) => Cutoff is null || utc <= Cutoff.Value.ToUniversalTime();
}

public sealed class ScanResult
{
    public required ClientRoot Root { get; init; }
    public List<ScanNode> Categories { get; } = [];
    public HashSet<CleanCategory> Missing { get; } = [];
    public List<string> Warnings { get; } = [];
    public int AccountCount { get; set; }
    public TimeSpan Elapsed { get; set; }

    /// <summary>因「文件时间」筛选而被排除在清理范围外的条目（条数与体积）。</summary>
    public FilterStats Filtered { get; } = new();

    public long TotalBytes => Categories.Sum(c => c.SizeBytes);
    public int TotalFiles => Categories.Sum(c => c.FileCount);

    public long DefaultSelectedBytes =>
        Categories.SelectMany(c => new[] { c }.Concat(c.Descendants()))
                  .Where(n => n.DefaultSelected && n.Targets.Count > 0)
                  .SelectMany(n => n.Targets)
                  .DistinctBy(t => t.Path)
                  .Sum(t => t.SizeBytes);
}

/// <summary>
/// 扫描引擎：把客户端目录翻译成可展示、可勾选的树。
/// 只做只读枚举，不修改任何文件。
/// </summary>
public sealed class ScanEngine
{
    private readonly RegionConfig _regions;
    private readonly IReadOnlyList<CategoryDefinition> _catalog;

    public ScanEngine(RegionConfig regions, IReadOnlyList<CategoryDefinition>? catalog = null)
    {
        _regions = regions;
        _catalog = catalog ?? CategoryCatalog.Default;
    }

    public ScanResult Scan(ClientRoot root, ScanOptions options,
                           CancellationToken ct = default,
                           Action<string>? progress = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ScanResult { Root = root };

        var resolver = new AccountResolver();
        var accountDirs = AccountResolver.EnumerateAccountDirs(root.MyDataDir).ToList();
        result.AccountCount = accountDirs.Count;

        foreach (var def in _catalog)
        {
            ct.ThrowIfCancellationRequested();

            // 截图 / 幻境云图 由「截图与云图」页独立枚举，
            // 这里跳过，避免为上千个媒体文件建树节点。
            if (def.MediaOnly) continue;

            progress?.Invoke($"正在扫描：{def.Name}");

            var node = Build(root, def, options, resolver, accountDirs, result, ct);
            if (node is null) continue;

            node.Aggregate();
            result.Categories.Add(node);
        }

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    private ScanNode? Build(ClientRoot root, CategoryDefinition def, ScanOptions opt,
                            AccountResolver resolver, List<string> accountDirs,
                            ScanResult result, CancellationToken ct)
    {
        return def.Granularity switch
        {
            Granularity.Account => BuildAccountScoped(root, def, opt, resolver, accountDirs, result, ct),
            Granularity.Group => BuildTeamMonGroups(root, def, opt, result),
            Granularity.RoleTree => BuildRoleTree(root, def, opt, result, ct),
            Granularity.Directory => BuildSingleDirectory(root, def, opt, result),
            _ => BuildFileList(root, def, opt, result)
        };
    }

    // ---------- 茗伊：按账号聚合 ----------

    private ScanNode? BuildAccountScoped(ClientRoot root, CategoryDefinition def, ScanOptions opt,
                                         AccountResolver resolver, List<string> accountDirs,
                                         ScanResult result, CancellationToken ct)
    {
        var categoryNode = NewCategory(root, def);
        var rule = opt.Keep.RuleFor(def.Id);

        foreach (var accountDir in accountDirs)
        {
            ct.ThrowIfCancellationRequested();

            // 把 {gid}@zhcn_hd 占位替换为该账号的实际目录名（兼容 @zhcn 旧后缀）
            var rel = def.RelativePath.Replace("{gid}@zhcn_hd", Path.GetFileName(accountDir), StringComparison.OrdinalIgnoreCase);
            var targetDir = Path.Combine(root.ZhcnDir, rel);
            if (!Directory.Exists(targetDir)) continue;

            List<FileInfo> files;
            try
            {
                files = new DirectoryInfo(targetDir).EnumerateFiles(def.Glob ?? "*")
                                                   .Where(f => Matches(def, f.Name))
                                                   .OrderByDescending(f => f.LastWriteTimeUtc)
                                                   .ToList();
            }
            catch { continue; }

            if (files.Count == 0) continue;

            var identity = resolver.Resolve(accountDir);

            // 先按「最近几个 / 最近几天」保留，剩下的再看时间窗口
            var split = RetentionPolicy.Split(
                files.Select(f => f.LastWriteTimeUtc).ToList(), rule, opt.Now.ToUniversalTime(), opt.CutoffUtc);

            var kept = files.Take(split.KeepCount).ToList();
            var candidates = split.CleanIndexes.Select(i => files[i]).ToList();

            // 茗伊各类数据只展示账号名本身：不显示括号角色名与 global_id
            var node = new ScanNode
            {
                Kind = NodeKind.Account,
                Category = def.Id,
                Title = identity.Display,
                FullPath = targetDir,
                Risk = def.Risk,
                Flags = identity.IsUnknown ? NodeFlags.UnknownOwner : NodeFlags.None,
                SizeBytes = files.Sum(f => f.Length),
                FileCount = files.Count,
                LastWriteUtc = files.Max(f => f.LastWriteTimeUtc),
                DefaultSelected = def.DefaultSelected && candidates.Count > 0
            };

            if (kept.Count > 0 && rule.IsActive)
                node.Note = rule.Describe();

            foreach (var f in candidates)
                node.Targets.Add(new TargetItem(f.FullName, f.Length, f.LastWriteTimeUtc, false));

            result.Filtered.Add(split.FilteredCount,
                split.FilteredIndexes.Sum(i => files[i].Length));

            categoryNode.Add(node);
        }

        return FinishCategory(categoryNode, def, result);
    }

    // ---------- 云团监：按组 ----------

    private ScanNode? BuildTeamMonGroups(ClientRoot root, CategoryDefinition def, ScanOptions opt, ScanResult result)
    {
        var dir = Path.Combine(root.ZhcnDir, def.RelativePath);
        if (!Directory.Exists(dir))
        {
            result.Missing.Add(def.Id);
            return null;
        }

        var groups = TeamMonGrouper.Group(dir);
        if (groups.Count == 0)
        {
            result.Missing.Add(def.Id);
            return null;
        }

        var categoryNode = NewCategory(root, def);
        var rule = opt.Keep.RuleFor(def.Id);
        var keepLatest = rule.Mode == KeepMode.LatestCount ? rule.Value : 0;

        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var retained = keepLatest > 0 && i < keepLatest;

            var node = new ScanNode
            {
                Kind = NodeKind.Group,
                Category = def.Id,
                Title = g.Key,
                // 灰色括号时间：云团监分组名是哈希，靠修改时间才能辨认是哪一次团
                RoleSummary = g.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Note = $"{g.Files.Count} 个文件" + (g.IsIncomplete ? " · 不完整" : string.Empty)
                       + (retained ? " · 保留" : string.Empty),
                FullPath = Path.Combine(dir, g.Key + ".jx3dat"),
                Risk = def.Risk,
                Flags = (g.IsIncomplete ? NodeFlags.IncompleteGroup : NodeFlags.None)
                        | (retained ? NodeFlags.Retained : NodeFlags.None),
                SizeBytes = g.SizeBytes,
                FileCount = g.Files.Count,
                LastWriteUtc = g.LastWriteUtc,
                DefaultSelected = false
            };

            if (retained)
            {
                // 保留的组不产生任何目标
            }
            else if (opt.WithinWindow(g.LastWriteUtc))
            {
                foreach (var f in g.Files)
                {
                    var fi = new FileInfo(f);
                    node.Targets.Add(new TargetItem(fi.FullName, fi.Length, fi.LastWriteTimeUtc, false));
                }
            }
            else
            {
                result.Filtered.Add(g.Files.Count, g.SizeBytes);
            }

            categoryNode.Add(node);
        }

        // 时间窗口下若所有组都被过滤，仍保留分类以便用户调整筛选
        return FinishCategory(categoryNode, def, result, keepWhenEmpty: true);
    }

    // ---------- 角色数据：账号-大区-服务器-角色 ----------

    private ScanNode? BuildRoleTree(ClientRoot root, CategoryDefinition def, ScanOptions opt,
                                    ScanResult result, CancellationToken ct)
    {
        var userDataDir = Path.Combine(root.ZhcnDir, def.RelativePath);
        if (!Directory.Exists(userDataDir))
        {
            result.Missing.Add(def.Id);
            return null;
        }

        var categoryNode = NewCategory(root, def);

        foreach (var accountDir in Directory.EnumerateDirectories(userDataDir))
        {
            ct.ThrowIfCancellationRequested();
            var accountName = Path.GetFileName(accountDir);
            if (IsReservedUserDataEntry(accountName)) continue;

            var regions = Directory.EnumerateDirectories(accountDir).ToList();
            var stat = Fs.StatDirectory(accountDir);

            // R-03 错误账号：无任何区服子目录
            if (regions.Count == 0)
            {
                var bad = new ScanNode
                {
                    Kind = NodeKind.Account,
                    Category = def.Id,
                    Title = accountName,
                    Note = "无区服文件 · 错误账号",
                    FullPath = accountDir,
                    Risk = RiskLevel.Low,
                    Flags = NodeFlags.InvalidAccount,
                    SizeBytes = stat.SizeBytes,
                    FileCount = stat.FileCount,
                    LastWriteUtc = stat.LastWriteUtc,
                    DefaultSelected = true
                };
                bad.Targets.Add(new TargetItem(accountDir, stat.SizeBytes, stat.LastWriteUtc, true));
                categoryNode.Add(bad);
                continue;
            }

            // 过期区服：大区名不在白名单内（如「电信五区」「双线一区」）
            var legacy = regions.Select(region => Path.GetFileName(region) ?? string.Empty)
                                .Where(name => !_regions.IsValid(name))
                                .Distinct(StringComparer.Ordinal)
                                .ToList();

            // 以账号文件夹为整体：不再展开到大区 / 服务器 / 角色三级
            var node = new ScanNode
            {
                Kind = NodeKind.Account,
                Category = def.Id,
                Title = accountName,
                RoleSummary = RoleSummaryOf(regions),
                Note = legacy.Count > 0 ? string.Join("、", legacy) : null,
                LegacyDetail = legacy.Count > 0 ? string.Join("、", legacy) : null,
                FullPath = accountDir,
                Risk = def.Risk,
                Flags = legacy.Count > 0 ? NodeFlags.LegacyRegion : NodeFlags.None,
                SizeBytes = stat.SizeBytes,
                FileCount = stat.FileCount,
                LastWriteUtc = stat.LastWriteUtc,
                DefaultSelected = false
            };

            if (opt.WithinWindow(stat.LastWriteUtc))
            {
                node.Targets.Add(new TargetItem(accountDir, stat.SizeBytes, stat.LastWriteUtc, true));
            }
            else
            {
                // 角色数据的时间单位就是账号文件夹本身，子项不再重复计数
                result.Filtered.Add(1, stat.SizeBytes);
            }

            // 有过期区服时允许展开，逐个大区单独勾选（其余大区不生成节点）
            foreach (var regionDir in regions.Where(r => !_regions.IsValid(Path.GetFileName(r))))
            {
                var regionStat = Fs.StatDirectory(regionDir);
                var regionNode = new ScanNode
                {
                    Kind = NodeKind.Region,
                    Category = def.Id,
                    Title = Path.GetFileName(regionDir),
                    Note = "过期区服",
                    FullPath = regionDir,
                    Risk = def.Risk,
                    Flags = NodeFlags.LegacyRegion,
                    SizeBytes = regionStat.SizeBytes,
                    FileCount = regionStat.FileCount,
                    LastWriteUtc = regionStat.LastWriteUtc,
                    DefaultSelected = false
                };

                if (opt.WithinWindow(regionStat.LastWriteUtc))
                    regionNode.Targets.Add(
                        new TargetItem(regionDir, regionStat.SizeBytes, regionStat.LastWriteUtc, true));

                node.Add(regionNode);
            }

            categoryNode.Add(node);
        }

        return FinishCategory(categoryNode, def, result);
    }

    /// <summary>
    /// 账号下全部角色名（大区\服务器\角色 的第四层），用于账号行后的灰色括号。
    /// 超过 3 个折叠为 +n。
    /// </summary>
    private static string? RoleSummaryOf(List<string> regionDirs)
    {
        var names = new List<string>();

        foreach (var region in regionDirs)
        {
            try
            {
                foreach (var server in Directory.EnumerateDirectories(region))
                    foreach (var character in Directory.EnumerateDirectories(server))
                        names.Add(Path.GetFileName(character));
            }
            catch { /* 权限不足时跳过该大区 */ }
        }

        names = names.Distinct(StringComparer.Ordinal).ToList();
        if (names.Count == 0) return null;

        return names.Count <= 3
            ? string.Join("、", names)
            : string.Join("、", names.Take(3)) + $"+{names.Count - 3}";
    }

    private static readonly HashSet<string> ReservedUserDataEntries =
        new(StringComparer.OrdinalIgnoreCase) { "fight_stat", "role_statistics", "config" };

    private static bool IsReservedUserDataEntry(string name) => ReservedUserDataEntries.Contains(name);

    // ---------- 单目录（懒人插件、旧团监） ----------

    private ScanNode? BuildSingleDirectory(ClientRoot root, CategoryDefinition def, ScanOptions opt, ScanResult result)
    {
        var dir = Path.Combine(root.ZhcnDir, def.RelativePath);
        if (!Directory.Exists(dir))
        {
            result.Missing.Add(def.Id);
            return null; // 不存在则不展示（懒人插件即此时不默认勾选）
        }

        var stat = Fs.StatDirectory(dir);

        // 整目录分类不再生成同名的子节点：分类行本身就是唯一可勾选条目
        var categoryNode = NewCategory(root, def);
        categoryNode.Note = "整目录";
        categoryNode.SizeBytes = stat.SizeBytes;
        categoryNode.FileCount = stat.FileCount;
        categoryNode.LastWriteUtc = stat.LastWriteUtc;
        categoryNode.DefaultSelected = def.DefaultSelected;   // 存在才勾选

        if (!opt.WithinWindow(stat.LastWriteUtc))
        {
            // 整目录只有一个时间基准：被时间窗口挡住就整目录都不进清理范围
            result.Filtered.Add(1, stat.SizeBytes);
            return categoryNode;
        }

        categoryNode.Targets.Add(new TargetItem(dir, stat.SizeBytes, stat.LastWriteUtc, true));
        return categoryNode;
    }

    // ---------- 文件列表（截图 / 云图 / 蓝图 / 染发 / 设计站） ----------

    private ScanNode? BuildFileList(ClientRoot root, CategoryDefinition def, ScanOptions opt, ScanResult result)
    {
        var dir = Path.Combine(root.ZhcnDir, def.RelativePath);
        if (!Directory.Exists(dir))
        {
            result.Missing.Add(def.Id);
            return null;
        }

        var categoryNode = NewCategory(root, def);
        var allowed = def.Media.Length > 0
            ? new HashSet<string>(def.Media, StringComparer.OrdinalIgnoreCase)
            : null;

        List<FileInfo> files;
        try
        {
            files = new DirectoryInfo(dir)
                .EnumerateFiles("*", new EnumerationOptions
                {
                    RecurseSubdirectories = def.Id == CleanCategory.ShareStation,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                })
                .Where(f => allowed is null || allowed.Contains(f.Extension))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
        }
        catch
        {
            result.Missing.Add(def.Id);
            return null;
        }

        if (files.Count > opt.MaxFileNodes)
        {
            result.Warnings.Add($"{def.Name}：共 {files.Count} 个文件，界面仅列出最近 {opt.MaxFileNodes} 个。");
            files = files.Take(opt.MaxFileNodes).ToList();
        }

        var rule = opt.Keep.RuleFor(def.Id);
        var split = RetentionPolicy.Split(
            files.Select(f => f.LastWriteTimeUtc).ToList(), rule, opt.Now.ToUniversalTime(), opt.CutoffUtc);

        var cleanable = split.CleanIndexes.ToHashSet();

        for (var i = 0; i < files.Count; i++)
        {
            var f = files[i];
            var canClean = cleanable.Contains(i);

            var node = new ScanNode
            {
                Kind = NodeKind.File,
                Category = def.Id,
                Title = f.Name,
                FullPath = f.FullName,
                Risk = def.Risk,
                SizeBytes = f.Length,
                FileCount = 1,
                LastWriteUtc = f.LastWriteTimeUtc,
                DefaultSelected = def.DefaultSelected && canClean,
                Note = i < split.KeepCount && rule.IsActive ? rule.Describe() : null
            };

            if (canClean)
                node.Targets.Add(new TargetItem(f.FullName, f.Length, f.LastWriteTimeUtc, false));

            categoryNode.Add(node);
        }

        result.Filtered.Add(split.FilteredCount, split.FilteredIndexes.Sum(i => files[i].Length));

        return FinishCategory(categoryNode, def, result, keepWhenEmpty: true);
    }

    // ---------- helpers ----------

    /// <summary>分类若声明了文件名正则，只保留匹配的文件（如金团排除 current.gkp.jx3dat）。</summary>
    private static bool Matches(CategoryDefinition def, string fileName) =>
        def.NamePattern is null || Regex.IsMatch(fileName, def.NamePattern);

    private static ScanNode NewCategory(ClientRoot root, CategoryDefinition def) => new()
    {
        Kind = NodeKind.Category,
        Category = def.Id,
        Group = def.Group,
        Title = def.Name,
        RelativePathText = def.RelativePath,
        BrowsePath = BrowsePathOf(root, def),
        Deprecated = def.Deprecated,
        Risk = def.Risk,
        Selectable = true
    };

    /// <summary>
    /// 「点击路径打开目录」的目标：无 {gid} 占位就是目录本身；
    /// 有占位则退到占位之前的公共父目录（如 my#data，能一次看到所有账号）。
    /// </summary>
    private static string BrowsePathOf(ClientRoot root, CategoryDefinition def)
    {
        var rel = def.RelativePath;
        var idx = rel.IndexOf("{gid}", StringComparison.OrdinalIgnoreCase);

        var target = idx < 0
            ? Path.Combine(root.ZhcnDir, rel)
            : Path.Combine(root.ZhcnDir, rel[..idx].TrimEnd('\\', '/'));

        return Directory.Exists(target) ? target : root.ZhcnDir;
    }

    private static ScanNode? FinishCategory(ScanNode node, CategoryDefinition def, ScanResult result,
                                            bool keepWhenEmpty = false)
    {
        if (node.Children.Count == 0 && !keepWhenEmpty)
        {
            result.Missing.Add(def.Id);
            return null;
        }
        return node;
    }
}

internal static class Fs
{
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public static DirStat StatDirectory(string dir)
    {
        long size = 0;
        var count = 0;
        var max = DateTime.MinValue;

        try
        {
            foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", Recursive))
            {
                size += f.Length;
                count++;
                if (f.LastWriteTimeUtc > max) max = f.LastWriteTimeUtc;
            }
        }
        catch { /* 忽略无权限子目录 */ }

        return new DirStat(size, count, max);
    }
}
