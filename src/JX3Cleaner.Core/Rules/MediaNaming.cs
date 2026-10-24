using System.Globalization;
using System.Text.RegularExpressions;

namespace JX3Cleaner.Core.Rules;

/// <summary>
/// 媒体文件命名规则。游戏生成的截图 / 录屏以拍摄时间开头，例如：
///   2026-10-24_20-24-55.mp4
///   2025-1-7-9-3-2_幻境云图.png
/// 分组与排序必须用文件名里的拍摄时间：文件被复制、从备份还原后 mtime 会变，
/// 只有文件名记录的是真正的拍摄时刻。
/// </summary>
public static partial class MediaNaming
{
    [GeneratedRegex(@"^(\d{4})-(\d{1,2})-(\d{1,2})[_-](\d{1,2})-(\d{1,2})-(\d{1,2})")]
    private static partial Regex NameTimeRegex();

    /// <summary>从文件名解析拍摄时间；解析不出或月日越界时返回 null。</summary>
    public static DateTime? TimestampFromName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var m = NameTimeRegex().Match(fileName);
        if (!m.Success) return null;

        try
        {
            return new DateTime(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>月份分组键（形如 2026-10），用于图片页的时间导航。</summary>
    public static string MonthKeyOf(DateTime timestamp) =>
        timestamp.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
