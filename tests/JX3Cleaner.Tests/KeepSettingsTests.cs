using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Tests;

/// <summary>设置页五项保留策略：默认值、映射到分类、越界收敛。</summary>
public sealed class KeepSettingsTests
{
    [Fact]
    public void 默认值符合设置页文案()
    {
        var keep = KeepSettings.Default;

        Assert.Equal(2, keep.ChatLogCount);
        Assert.Equal(30, keep.FightStatDays);
        Assert.Equal(30, keep.CombatLogDays);
        Assert.Equal(90, keep.GkpDays);
        Assert.Equal(5, keep.TeamMonGroups);
    }

    [Fact]
    public void 五个分类各自映射到对应规则()
    {
        var keep = new KeepSettings
        {
            ChatLogCount = 3,
            FightStatDays = 10,
            CombatLogDays = 20,
            GkpDays = 40,
            TeamMonGroups = 6
        };

        Assert.Equal(new KeepRule(KeepMode.LatestCount, 3), keep.RuleFor(CleanCategory.ChatLog));
        Assert.Equal(new KeepRule(KeepMode.LatestDays, 10), keep.RuleFor(CleanCategory.FightStat));
        Assert.Equal(new KeepRule(KeepMode.LatestDays, 20), keep.RuleFor(CleanCategory.CombatLog));
        Assert.Equal(new KeepRule(KeepMode.LatestDays, 40), keep.RuleFor(CleanCategory.Gkp));
        Assert.Equal(new KeepRule(KeepMode.LatestCount, 6), keep.RuleFor(CleanCategory.CloudTeamMon));
    }

    [Theory]
    [InlineData(CleanCategory.Blueprint)]
    [InlineData(CleanCategory.RoleData)]
    [InlineData(CleanCategory.Screenshot)]
    public void 其它分类没有保留规则(CleanCategory id)
    {
        Assert.False(KeepSettings.Default.RuleFor(id).IsActive);
    }

    [Fact]
    public void 越界值被收敛到合法区间()
    {
        var clamped = new KeepSettings
        {
            ChatLogCount = -5,
            FightStatDays = 99999,
            CombatLogDays = 0,
            GkpDays = -1,
            TeamMonGroups = 5000
        }.Clamped();

        Assert.Equal(0, clamped.ChatLogCount);
        Assert.Equal(KeepSettings.MaxDays, clamped.FightStatDays);
        Assert.Equal(0, clamped.CombatLogDays);
        Assert.Equal(0, clamped.GkpDays);
        Assert.Equal(KeepSettings.MaxCount, clamped.TeamMonGroups);
    }

    [Fact]
    public void AppSettings与KeepSettings往返一致()
    {
        var settings = new AppSettings
        {
            ChatLogKeepCount = 4,
            FightStatKeepDays = 15,
            CombatLogKeepDays = 25,
            GkpKeepDays = 35,
            TeamMonKeepGroups = 8
        };

        var keep = settings.ToKeepSettings();

        Assert.Equal(4, keep.ChatLogCount);
        Assert.Equal(15, keep.FightStatDays);
        Assert.Equal(25, keep.CombatLogDays);
        Assert.Equal(35, keep.GkpDays);
        Assert.Equal(8, keep.TeamMonGroups);

        settings.ApplyKeep(new KeepSettings
        {
            ChatLogCount = 1,
            FightStatDays = 5,
            CombatLogDays = 6,
            GkpDays = 7,
            TeamMonGroups = 9
        });

        Assert.Equal(1, settings.ChatLogKeepCount);
        Assert.Equal(5, settings.FightStatKeepDays);
        Assert.Equal(6, settings.CombatLogKeepDays);
        Assert.Equal(7, settings.GkpKeepDays);
        Assert.Equal(9, settings.TeamMonKeepGroups);
    }

    [Fact]
    public void 副本互不影响()
    {
        var original = new AppSettings { ChatLogKeepCount = 2, ClientRoot = @"C:\jx3" };
        var copy = original.Copy();

        copy.ChatLogKeepCount = 9;
        copy.ClientRoot = @"D:\other";

        Assert.Equal(2, original.ChatLogKeepCount);
        Assert.Equal(@"C:\jx3", original.ClientRoot);
    }

    [Fact]
    public void 保留规则描述文案()
    {
        Assert.Equal("保留最近 2 个", KeepRule.Latest(2).Describe());
        Assert.Equal("保留最近 30 天", KeepRule.Days(30).Describe());
        Assert.Equal(string.Empty, KeepRule.None.Describe());
    }
}
