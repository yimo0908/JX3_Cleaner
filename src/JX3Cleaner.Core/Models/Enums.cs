namespace JX3Cleaner.Core.Models;

/// <summary>可清理的数据分类。顺序即界面展示顺序。</summary>
public enum CleanCategory
{
    ChatLog,
    FightStat,
    /// <summary>茗伊战斗日志（.jcl）。原文未涉及，实测为体积最大项。</summary>
    CombatLog,
    /// <summary>茗伊金团记录（.gkp.jx3dat），按账号聚合。</summary>
    Gkp,
    LazyPlugin,
    LazyPluginData,
    CloudTeamMon,
    LegacyTeamMon,
    Blueprint,
    Screenshot,
    Dcim,
    RoleData,
    HairDye,
    ShareStation
}

/// <summary>分类所属的组，界面左右两栏都按组划分。</summary>
public enum CategoryGroup
{
    /// <summary>茗伊插件（含云团监、旧版团监、JCL 日志、金团记录）</summary>
    Mingyi,

    /// <summary>懒人插件</summary>
    Lazy,

    /// <summary>游戏自带数据</summary>
    Game
}

public enum NodeKind
{
    Category,
    Account,
    Region,
    Server,
    Character,
    Item,
    Group,
    File
}

[Flags]
public enum NodeFlags
{
    None = 0,
    /// <summary>账号目录下无任何区服子目录</summary>
    InvalidAccount = 1,
    /// <summary>大区名不在白名单内，疑似区服合并前的残留</summary>
    LegacyRegion = 2,
    /// <summary>茗伊账号目录未能解析出玩家名</summary>
    UnknownOwner = 4,
    /// <summary>云团监只有一半文件（缺 .meta 或 .jx3dat）</summary>
    IncompleteGroup = 8,
    /// <summary>目录不存在</summary>
    Missing = 16,

    /// <summary>受保留策略保护（如最近 2 个聊天记录），"全选"时跳过</summary>
    Retained = 32
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}

/// <summary>清理方式。</summary>
public enum CleanMode
{
    /// <summary>移动到回收站，可还原。</summary>
    RecycleBin,

    /// <summary>调用 cmd.exe del，不可恢复。</summary>
    DirectDelete
}

public enum TimeWindow
{
    All,
    Days7,
    Days30,
    Days90,
    Days180,
    Days365
}

/// <summary>条目粒度。</summary>
public enum Granularity
{
    /// <summary>整目录（如旧版团监）</summary>
    Directory,

    /// <summary>单个文件</summary>
    File,

    /// <summary>按账号聚合（茗伊数据）</summary>
    Account,

    /// <summary>四级树（角色数据）</summary>
    RoleTree,

    /// <summary>成组（云团监：.jx3dat + .meta.jx3dat）</summary>
    Group
}
