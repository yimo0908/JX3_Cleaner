using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Planning;

public sealed record GuardResult(bool Ok, string? Reason = null)
{
    public static readonly GuardResult Pass = new(true);
    public static GuardResult Fail(string reason) => new(false, reason);
}

/// <summary>
/// 删除路径守卫。任何要删除的路径都必须先过这里：
/// 规范化 → 必须在 bin\zhcn_hd 之下 → 不能在保护名单 → 不能是 reparse point / 系统文件。
/// </summary>
public sealed class PathGuard
{
    private readonly string _rootPrefix;
    private readonly HashSet<string> _protected;

    public PathGuard(ClientRoot client)
    {
        _rootPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(client.ZhcnDir)) + Path.DirectorySeparatorChar;

        _protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            client.ZhcnDir,
            client.InterfaceDir,
            client.MyDataDir,
            client.UserDataDir,
            Path.Combine(client.ZhcnDir, "homelanddir"),
            Path.Combine(client.ZhcnDir, "hairdyeingdata"),
            Path.Combine(client.ZhcnDir, "sharestationdata"),
            // 云团监的本地数据，绝不删除
            Path.Combine(client.MyDataDir, "!all-users@zhcn_hd", "userdata", "team_mon", "local.jx3dat"),
            Path.Combine(client.MyDataDir, "!all-users@zhcn_hd", "userdata", "team_mon", "metalist.jx3dat"),
            Path.Combine(client.MyDataDir, "!all-users@zhcn_hd", "userdata", "team_mon", "audio"),
        }.Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)))
         .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public GuardResult Check(TargetItem item) => Check(item.Path);

    public GuardResult Check(string candidate)
    {
        string full;
        try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)); }
        catch { return GuardResult.Fail("路径非法"); }

        if (!full.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
            return GuardResult.Fail("路径不在客户端 bin\\zhcn_hd 之内");

        if (_protected.Contains(full))
            return GuardResult.Fail("受保护路径，禁止删除");

        try
        {
            var attr = File.GetAttributes(full);
            if (attr.HasFlag(FileAttributes.ReparsePoint)) return GuardResult.Fail("是符号链接 / junction，已跳过");
            if (attr.HasFlag(FileAttributes.System)) return GuardResult.Fail("系统属性文件，已跳过");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { return GuardResult.Fail("条目已不存在"); }

        return GuardResult.Pass;
    }
}
