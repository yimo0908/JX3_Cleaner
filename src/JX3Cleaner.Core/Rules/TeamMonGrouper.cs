namespace JX3Cleaner.Core.Rules;

/// <summary>云团监的一组：r-xxxx.jx3dat + r-xxxx.meta.jx3dat。</summary>
public sealed record TeamMonGroup(
    string Key,
    IReadOnlyList<string> Files,
    long SizeBytes,
    DateTime LastWriteUtc,
    bool IsIncomplete);

/// <summary>
/// R-05 云团监分组。
/// 文件名形如 r-02b1c7bd19102d8f.jx3dat / r-02b1c7bd19102d8f.meta.jx3dat，
/// 去掉两级后缀后同名的文件属于同一组。
/// </summary>
public static class TeamMonGrouper
{
    private const string MetaSuffix = ".meta.jx3dat";
    private const string DataSuffix = ".jx3dat";

    public static string? KeyOf(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        if (n.EndsWith(MetaSuffix, StringComparison.Ordinal)) return n[..^MetaSuffix.Length];
        if (n.EndsWith(DataSuffix, StringComparison.Ordinal)) return n[..^DataSuffix.Length];
        return null;
    }

    public static List<TeamMonGroup> Group(string remoteDir)
    {
        var result = new List<TeamMonGroup>();
        if (!Directory.Exists(remoteDir)) return result;

        var buckets = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
        foreach (var file in new DirectoryInfo(remoteDir).EnumerateFiles())
        {
            var key = KeyOf(file.Name);
            if (key is null) continue;
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = [];
            list.Add(file);
        }

        foreach (var (key, files) in buckets)
        {
            var hasData = files.Any(f => f.Name.EndsWith(DataSuffix, StringComparison.OrdinalIgnoreCase)
                                         && !f.Name.EndsWith(MetaSuffix, StringComparison.OrdinalIgnoreCase));
            var hasMeta = files.Any(f => f.Name.EndsWith(MetaSuffix, StringComparison.OrdinalIgnoreCase));

            result.Add(new TeamMonGroup(
                Key: key,
                Files: files.Select(f => f.FullName).ToList(),
                SizeBytes: files.Sum(f => f.Length),
                LastWriteUtc: files.Max(f => f.LastWriteTimeUtc),
                IsIncomplete: !(hasData && hasMeta)));
        }

        return result.OrderByDescending(g => g.LastWriteUtc).ToList();
    }
}
