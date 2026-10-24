namespace JX3Cleaner.Core.Models;

/// <summary>一个可删除条目（文件或目录）。</summary>
public sealed record TargetItem(string Path, long SizeBytes, DateTime LastWriteUtc, bool IsDirectory)
{
    public string DisplaySize => FormatSize(SizeBytes);

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
    };
}

public sealed record DirStat(long SizeBytes, int FileCount, DateTime LastWriteUtc)
{
    public static readonly DirStat Empty = new(0, 0, DateTime.MinValue);
}
