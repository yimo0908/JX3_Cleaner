using JX3Cleaner.Core.Rules;

namespace JX3Cleaner.Tests;

/// <summary>媒体文件名里的拍摄时间解析（图片页月份分组依赖它）。</summary>
public sealed class MediaNamingTests
{
    [Theory]
    [InlineData("2026-10-24_20-24-55.mp4")]
    [InlineData("2026-10-24-20-24-55.mp4")]
    [InlineData("2026-10-24_20-24-55_幻境云图.png")]
    public void 标准命名可解析(string fileName)
    {
        var time = MediaNaming.TimestampFromName(fileName);

        Assert.NotNull(time);
        Assert.Equal(new DateTime(2026, 10, 24, 20, 24, 55), time!.Value);
    }

    [Fact]
    public void 月和日不补零也能解析()
    {
        var time = MediaNaming.TimestampFromName("2025-1-7-9-3-2_截图.png");

        Assert.Equal(new DateTime(2025, 1, 7, 9, 3, 2), time);
    }

    [Theory]
    [InlineData("ScreenShot.png")]
    [InlineData("2026-10-24.png")]                 // 只有日期没有时间
    [InlineData("DCIM_2026-10-24_20-24-55.jpg")]   // 时间不在开头
    [InlineData("")]
    [InlineData(null)]
    public void 无法解析时返回空(string? fileName) =>
        Assert.Null(MediaNaming.TimestampFromName(fileName));

    [Theory]
    [InlineData("2026-13-24_20-24-55.jpg")]        // 月份越界
    [InlineData("2026-10-32_20-24-55.jpg")]        // 日期越界
    [InlineData("2026-10-24_25-24-55.jpg")]        // 小时越界
    public void 越界时间返回空(string fileName) =>
        Assert.Null(MediaNaming.TimestampFromName(fileName));

    [Fact]
    public void 月份键格式为yyyy_MM()
    {
        Assert.Equal("2026-01", MediaNaming.MonthKeyOf(new DateTime(2026, 1, 9)));
        Assert.Equal("2025-12", MediaNaming.MonthKeyOf(new DateTime(2025, 12, 31, 23, 59, 59)));
    }
}
