using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Config;

/// <summary>保留策略的类型。</summary>
public enum KeepMode
{
    /// <summary>不保留，全部纳入清理范围。</summary>
    None,

    /// <summary>按「最近几个」保留（聊天记录 / 云团监分组）。</summary>
    LatestCount,

    /// <summary>按「最近多少天」保留（战斗记录 / JCL 日志 / 金团记录）。</summary>
    LatestDays
}

/// <summary>
/// 一类数据的保留规则。条数按「最新的前 N 个」，天数按「最近 N 天内产生的不动」。
/// </summary>
public readonly record struct KeepRule(KeepMode Mode, int Value)
{
    public static readonly KeepRule None = new(KeepMode.None, 0);

    public static KeepRule Latest(int count) => new(KeepMode.LatestCount, Math.Max(0, count));

    public static KeepRule Days(int days) => new(KeepMode.LatestDays, Math.Max(0, days));

    /// <summary>Value 为 0 时等同不保留，界面上也按「不保留」解释。</summary>
    public bool IsActive => Mode != KeepMode.None && Value > 0;

    public string Describe() => Mode switch
    {
        KeepMode.LatestCount => $"保留最近 {Value} 个",
        KeepMode.LatestDays => $"保留最近 {Value} 天",
        _ => string.Empty
    };
}

/// <summary>
/// 五类数据的保留设置（对应设置页的 N / X / Y / Z / M）。
/// 纯数据对象，界面与扫描引擎共用同一份，避免两处各写一遍默认值。
/// </summary>
public sealed class KeepSettings
{
    /// <summary>N：聊天记录保留最近几个（每次登录生成一个 .db）。</summary>
    public int ChatLogCount { get; init; } = 2;

    /// <summary>X：战斗记录保留最近多少天。</summary>
    public int FightStatDays { get; init; } = 30;

    /// <summary>Y：JCL 战斗日志保留最近多少天。</summary>
    public int CombatLogDays { get; init; } = 30;

    /// <summary>Z：金团记录保留最近多少天。</summary>
    public int GkpDays { get; init; } = 90;

    /// <summary>M：云团监数据保留最近几组。</summary>
    public int TeamMonGroups { get; init; } = 5;

    public static KeepSettings Default => new();

    /// <summary>设置项允许的取值范围：0 表示不保留，上限防止误输入天文数字。</summary>
    public const int MaxCount = 999;
    public const int MaxDays = 3650;

    /// <summary>把外部写入的值收敛到合法区间。</summary>
    public KeepSettings Clamped() => new()
    {
        ChatLogCount = Math.Clamp(ChatLogCount, 0, MaxCount),
        FightStatDays = Math.Clamp(FightStatDays, 0, MaxDays),
        CombatLogDays = Math.Clamp(CombatLogDays, 0, MaxDays),
        GkpDays = Math.Clamp(GkpDays, 0, MaxDays),
        TeamMonGroups = Math.Clamp(TeamMonGroups, 0, MaxCount)
    };

    public KeepRule RuleFor(CleanCategory id) => id switch
    {
        CleanCategory.ChatLog => KeepRule.Latest(ChatLogCount),
        CleanCategory.FightStat => KeepRule.Days(FightStatDays),
        CleanCategory.CombatLog => KeepRule.Days(CombatLogDays),
        CleanCategory.Gkp => KeepRule.Days(GkpDays),
        CleanCategory.CloudTeamMon => KeepRule.Latest(TeamMonGroups),
        _ => KeepRule.None
    };
}
