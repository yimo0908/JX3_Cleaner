using Microsoft.Win32;
using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Discovery;

/// <summary>
/// 客户端目录定位与结构校验。
/// 硬性要求：能归一化出 bin\zhcn_hd，且 zhcn_hd\bin64 存在。
/// 软性要求：各清理目录存在性，缺失只影响对应分类。
/// </summary>
public sealed class ClientLocator
{
    private static readonly (string Name, string Relative, bool Required)[] RequiredDirs =
    [
        ("茗伊 / 云团监 / 旧团监", @"interface\my#data", false),
        ("懒人插件", @"interface\LR_Plugin", false),
        ("懒人插件数据", @"interface\LR_Plugin@DATA", false),
        ("角色数据", "userdata", false),
        ("截图", "ScreenShot", false),
        ("幻境云图", "DCIM", false),
        ("家园蓝图", @"homelanddir\Blueprints", false),
        ("染发方案", "hairdyeingdata", false),
        ("设计站图片", "sharestationdata", false)
    ];

    /// <summary>
    /// 接受三种输入：客户端根（…\JX3）、bin 目录（…\JX3\bin）、zhcn_hd 目录。
    /// 均归一化为 ClientRoot。
    /// </summary>
    public static ClientRoot? Normalize(string? picked)
    {
        if (string.IsNullOrWhiteSpace(picked)) return null;

        string p;
        try { p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(picked)); }
        catch { return null; }

        // ① 直接选了 zhcn_hd
        if (Path.GetFileName(p).Equals("zhcn_hd", StringComparison.OrdinalIgnoreCase))
        {
            var bin = Path.GetDirectoryName(p);
            var root = bin is null ? null : Path.GetDirectoryName(bin);
            if (bin is not null && root is not null)
                return new ClientRoot(root, bin, p);
        }

        // ② 选了 bin 目录
        if (Path.GetFileName(p).Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            var zhcn = Path.Combine(p, "zhcn_hd");
            if (Directory.Exists(zhcn) && Directory.Exists(Path.Combine(zhcn, "bin64")))
                return new ClientRoot(Path.GetDirectoryName(p) ?? p, p, zhcn);
        }

        // ③ 选了客户端根
        var candidate = Path.Combine(p, "bin", "zhcn_hd");
        if (Directory.Exists(candidate))
            return new ClientRoot(p, Path.Combine(p, "bin"), candidate);

        return null;
    }

    public ClientValidation Validate(string? picked)
    {
        var checks = new List<CheckItem>();
        var errors = new List<string>();

        var root = Normalize(picked);
        if (root is null)
        {
            errors.Add("所选目录下未找到 bin\\zhcn_hd，请选择剑网3客户端根目录。");
            return new ClientValidation { Root = null, Checks = checks, HardErrors = errors };
        }

        var bin64 = root.Bin64Dir;
        var hasBin64 = Directory.Exists(bin64);
        checks.Add(new CheckItem("bin64", bin64, hasBin64, true,
            hasBin64 ? null : "缺少 bin64，可能不是 V4 客户端或版本过旧"));
        if (!hasBin64) errors.Add("缺少 bin64 目录，可能不是 V4 客户端或版本过旧。");

        foreach (var (name, rel, required) in RequiredDirs)
        {
            var full = Path.Combine(root.ZhcnDir, rel);
            checks.Add(new CheckItem(name, full, Directory.Exists(full), required));
        }

        if (checks.Count(c => !c.Required && c.Exists) == 0)
            errors.Add("未检测到任何可清理目录，请确认选择的是剑网3客户端目录。");

        return new ClientValidation { Root = root, Checks = checks, HardErrors = errors };
    }

    /// <summary>
    /// 自动探测。仅做尽力而为：注册表 + 常见安装位置。
    /// 探测失败时必须回落到手动定位，不得使用任何写死的路径。
    /// </summary>
    public string? AutoDetect()
    {
        foreach (var p in ProbeRegistry()) if (Validate(p).IsValid) return p;

        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.RootDirectory.FullName);

        foreach (var drive in drives)
        {
            foreach (var rel in new[] { @"SeasunGame\Game\JX3", @"SeasunGame\JX3", @"Program Files\SeasunGame\JX3", @"Games\JX3" })
            {
                var p = Path.Combine(drive, rel);
                if (Validate(p).IsValid) return p;
            }
        }

        return null;
    }

    private static IEnumerable<string> ProbeRegistry()
    {
        (RegistryHive Hive, string Path)[] keys =
        [
            (RegistryHive.CurrentUser, @"Software\Kingsoft\JX3"),
            (RegistryHive.CurrentUser, @"Software\Seasun\JX3"),
            (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Kingsoft\JX3"),
            (RegistryHive.LocalMachine, @"SOFTWARE\Kingsoft\JX3")
        ];

        foreach (var (hive, path) in keys)
        {
            string? value = null;
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(path);
                value = key?.GetValue("InstallPath") as string
                        ?? key?.GetValue("Path") as string
                        ?? key?.GetValue("GamePath") as string;
            }
            catch { /* 注册表不可读时忽略 */ }

            if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }
    }

    /// <summary>扫盘时一跳到底的深度上限：根\厂商\Game\JX3\bin\zhcn_hd 正好 5 层。</summary>
    private const int DeepMaxDepth = 5;

    /// <summary>明显不可能放客户端的目录，扫盘时直接跳过，避免白跑几十分钟。</summary>
    private static readonly HashSet<string> DeepSkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
        "$WinREAgent", "Config.Msi", "WindowsApps", "ProgramData", "node_modules",
        ".git", ".svn", "AppData", "Temp", "tmp"
    };

    /// <summary>
    /// 扫盘：先走注册表与常见安装位置，再对固定磁盘做有限深度（<see cref="DeepMaxDepth"/> 层）
    /// 的目录搜索，只认「…\bin\zhcn_hd\bin64」这一结构。
    /// 只读枚举，找到即返回；用户可随时取消。
    /// </summary>
    public string? DeepDetect(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var quick = AutoDetect();
        if (quick is not null) return quick;

        foreach (var drive in DriveInfo.GetDrives()
                                       .Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"正在扫描 {drive.Name} …");

            var found = SearchDrive(drive.RootDirectory.FullName, ct, progress);
            if (found is not null) return found;
        }

        return null;
    }

    private string? SearchDrive(string root, CancellationToken ct, IProgress<string>? progress)
    {
        var level = new List<string> { root };

        for (var depth = 0; depth < DeepMaxDepth && level.Count > 0; depth++)
        {
            var next = new List<string>();

            foreach (var dir in level)
            {
                ct.ThrowIfCancellationRequested();

                IEnumerable<string> children;
                try
                {
                    children = Directory.EnumerateDirectories(dir);
                }
                catch { continue; }   // 无权限的目录整体跳过

                foreach (var child in children)
                {
                    var name = Path.GetFileName(child);

                    // 命中：bin 下就是 zhcn_hd
                    if (name.Equals("zhcn_hd", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileName(dir).Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                        Directory.Exists(Path.Combine(child, "bin64")))
                    {
                        var clientRoot = Path.GetDirectoryName(dir);
                        if (!string.IsNullOrEmpty(clientRoot) && Validate(clientRoot).IsValid)
                            return clientRoot;
                    }

                    if (DeepSkipDirs.Contains(name)) continue;

                    try
                    {
                        if (new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    }
                    catch { continue; }

                    next.Add(child);
                }
            }

            progress?.Report($"正在扫描 {root} … 第 {depth + 1} 层（{next.Count} 个目录）");
            level = next;
        }

        return null;
    }
}
