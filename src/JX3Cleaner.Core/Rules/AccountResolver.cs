using System.Text.RegularExpressions;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Rules;

public sealed record AccountIdentity(
    string DirName,
    string GlobalId,
    string Display,
    IReadOnlyList<string> Candidates,
    bool IsUnknown)
{
    /// <summary>界面用：多个角色名以「、」连接，超过 3 个折叠为 +n。</summary>
    public string RoleSummary
    {
        get
        {
            if (Candidates.Count == 0) return string.Empty;
            if (Candidates.Count <= 3) return string.Join("、", Candidates);
            return string.Join("、", Candidates.Take(3)) + $"+{Candidates.Count - 3}";
        }
    }
}

/// <summary>
/// R-01 账号名解析。
/// 实测依据：账号目录内会存在以玩家名命名的空目录；
/// 但 audio/cache/logs 等固定目录本身也常为空，因此必须靠白名单排除。
/// </summary>
public sealed partial class AccountResolver
{
    /// <summary>账号目录下的固定子目录，不视为角色名。</summary>
    public static readonly HashSet<string> FixedSubDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio", "cache", "config", "data", "export", "font", "logs",
        "temporary", "userdata", "img", "lang", "licenses", "src", "bin"
    };

    private static readonly char[] IllegalChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    [GeneratedRegex(@"^(\d{6,})@zhcn(_hd)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AccountDirRegex();

    public static bool TryParseAccountDir(string dirName, out string globalId)
    {
        var m = AccountDirRegex().Match(dirName);
        if (m.Success)
        {
            globalId = m.Groups[1].Value;
            return true;
        }
        globalId = string.Empty;
        return false;
    }

    /// <summary>枚举 my#data 下所有账号目录（排除 !all-users、#cache、#区服 等）。</summary>
    public static IEnumerable<string> EnumerateAccountDirs(string myDataDir)
    {
        if (!Directory.Exists(myDataDir)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(myDataDir))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith('!') || name.StartsWith('#')) continue;
            if (TryParseAccountDir(name, out _)) yield return dir;
        }
    }

    /// <summary>解析单个账号目录的展示名。</summary>
    public AccountIdentity Resolve(string accountDir)
    {
        var dirName = Path.GetFileName(accountDir);
        TryParseAccountDir(dirName, out var gid);

        var candidates = new List<string>();
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(accountDir))
            {
                var name = Path.GetFileName(sub);
                if (FixedSubDirs.Contains(name)) continue;
                if (name.Length is < 1 or > 16) continue;
                if (name.All(char.IsAsciiDigit)) continue;          // 实测存在与 gid 同名的空目录
                if (name.IndexOfAny(IllegalChars) >= 0) continue;
                candidates.Add(name);
            }
        }
        catch { /* 权限不足时按未知账号处理 */ }

        candidates = candidates.Distinct(StringComparer.Ordinal).ToList();

        var display = candidates.Count > 0
            ? string.Join("、", candidates.Take(3)) + (candidates.Count > 3 ? $"+{candidates.Count - 3}" : "")
            : $"未知账号…{Tail(gid)}";

        return new AccountIdentity(dirName, gid, display, candidates, candidates.Count == 0);
    }

    private static string Tail(string gid) => gid.Length >= 4 ? gid[^4..] : gid;
}
