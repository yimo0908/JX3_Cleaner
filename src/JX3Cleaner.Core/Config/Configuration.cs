using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Config;

/// <summary>一类可清理数据的定义。路径相对 bin\zhcn_hd，可用 {gid} 占位。</summary>
public sealed record CategoryDefinition
{
    public required CleanCategory Id { get; init; }
    public required string Name { get; init; }

    /// <summary>所属分组，界面左右两栏都按组划分。</summary>
    public CategoryGroup Group { get; init; } = CategoryGroup.Game;

    public required string RelativePath { get; init; }
    public string? Glob { get; init; }
    public Granularity Granularity { get; init; } = Granularity.File;

    /// <summary>目录/文件不存在时是否直接隐藏该分类。</summary>
    public bool RequireExists { get; init; } = true;

    public bool DefaultSelected { get; init; }
    public RiskLevel Risk { get; init; } = RiskLevel.Low;

    /// <summary>
    /// 文件名正则。用于把目录里「不该动」的文件排除在清理范围外
    /// （如金团记录的 current.gkp.jx3dat 为当前正在进行的记录）。
    /// </summary>
    public string? NamePattern { get; init; }

    /// <summary>媒体扩展名（截图 / 云图用）。</summary>
    public string[] Media { get; init; } = [];

    /// <summary>
    /// 已弃用的分类（懒人插件、懒人插件数据、旧版团监数据）。
    /// 界面上标题后补黄色「（已弃用）」；目录不存在时左右两栏都不出现。
    /// </summary>
    public bool Deprecated { get; init; }

    /// <summary>已弃用分类的标题后缀。</summary>
    public const string DeprecatedLabel = "（已弃用）";

    /// <summary>
    /// 只由「截图与云图」页处理，不在「数据清理」页出现。
    /// 扫描时直接跳过：这类目录动辄上千个文件，没必要为它建树节点。
    /// </summary>
    public bool MediaOnly { get; init; }
}

public static class CategoryCatalog
{
    public static readonly string[] DefaultMedia =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4"];

    public static readonly IReadOnlyList<CategoryDefinition> Default =
    [
        // ---------- 茗伊插件 ----------
        new()
        {
            Id = CleanCategory.ChatLog, Name = "聊天记录", Group = CategoryGroup.Mingyi,
            RelativePath = @"interface\my#data\{gid}@zhcn_hd\userdata\chat_log",
            Glob = "*.db*", Granularity = Granularity.Account,
            DefaultSelected = true, Risk = RiskLevel.Medium
        },
        new()
        {
            Id = CleanCategory.FightStat, Name = "战斗记录", Group = CategoryGroup.Mingyi,
            RelativePath = @"interface\my#data\{gid}@zhcn_hd\userdata\fight_stat",
            Glob = "*.jx3dat", Granularity = Granularity.Account, Risk = RiskLevel.Low
        },
        new()
        {
            // 原文未列出，实测单账号可达 8.8 GB，是全盘最大项，故默认不勾选交由用户决定
            Id = CleanCategory.CombatLog, Name = "JCL战斗日志", Group = CategoryGroup.Mingyi,
            RelativePath = @"interface\my#data\{gid}@zhcn_hd\userdata\combat_logs",
            Glob = "*.jcl", Granularity = Granularity.Account, Risk = RiskLevel.Medium
        },
        new()
        {
            // 文件名形态：2021-08-07-21-45-33_25人普通白帝江关.gkp.jx3dat
            // current.gkp.jx3dat 是当前正在进行的金团记录，不在清理范围内
            Id = CleanCategory.Gkp, Name = "金团记录", Group = CategoryGroup.Mingyi,
            RelativePath = @"interface\my#data\{gid}@zhcn_hd\userdata\gkp",
            // 用宽 glob + 正则精筛：Win32 通配对 *.gkp.jx3dat 这种复合后缀不可靠
            Glob = "*.jx3dat", NamePattern = @"^\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}_",
            Granularity = Granularity.Account, Risk = RiskLevel.Low
        },
        new()
        {
            Id = CleanCategory.CloudTeamMon, Name = "云团监数据", Group = CategoryGroup.Mingyi,
            RelativePath = @"interface\my#data\!all-users@zhcn_hd\userdata\team_mon\remote",
            Granularity = Granularity.Group, Risk = RiskLevel.Low
        },
        new()
        {
            // 存在才勾选：目录不存在时整条分类不出现
            Id = CleanCategory.LegacyTeamMon, Name = "旧版团监数据", Group = CategoryGroup.Mingyi,
            RelativePath = @"interface\my#data\!all-users@zhcn_hd\userdata\TeamMon",
            Granularity = Granularity.Directory,
            DefaultSelected = true, Risk = RiskLevel.Low, Deprecated = true
        },

        // ---------- 懒人插件 ----------
        new()
        {
            // 存在才勾选
            Id = CleanCategory.LazyPlugin, Name = "懒人插件", Group = CategoryGroup.Lazy,
            RelativePath = @"interface\LR_Plugin", Granularity = Granularity.Directory,
            DefaultSelected = true, Risk = RiskLevel.Low, Deprecated = true
        },
        new()
        {
            Id = CleanCategory.LazyPluginData, Name = "懒人插件数据", Group = CategoryGroup.Lazy,
            RelativePath = @"interface\LR_Plugin@DATA", Granularity = Granularity.Directory,
            DefaultSelected = true, Risk = RiskLevel.Low, Deprecated = true
        },

        // ---------- 游戏数据 ----------
        new()
        {
            Id = CleanCategory.RoleData, Name = "角色数据", Group = CategoryGroup.Game,
            RelativePath = @"userdata", Granularity = Granularity.RoleTree, Risk = RiskLevel.High
        },
        new()
        {
            // 只属于「截图与云图」页：数据清理页不显示，扫描也不生成节点
            Id = CleanCategory.Screenshot, Name = "截图", Group = CategoryGroup.Game,
            RelativePath = @"ScreenShot", Granularity = Granularity.File,
            Risk = RiskLevel.High, Media = DefaultMedia, MediaOnly = true
        },
        new()
        {
            Id = CleanCategory.Dcim, Name = "幻境云图", Group = CategoryGroup.Game,
            RelativePath = @"DCIM", Granularity = Granularity.File,
            Risk = RiskLevel.High, Media = DefaultMedia, MediaOnly = true
        },
        new()
        {
            Id = CleanCategory.Blueprint, Name = "家园蓝图", Group = CategoryGroup.Game,
            RelativePath = @"homelanddir\Blueprints", Granularity = Granularity.File, Risk = RiskLevel.High
        },
        new()
        {
            Id = CleanCategory.HairDye, Name = "染发方案", Group = CategoryGroup.Game,
            RelativePath = @"hairdyeingdata", Granularity = Granularity.File, Risk = RiskLevel.Medium
        },
        new()
        {
            Id = CleanCategory.ShareStation, Name = "设计站图片", Group = CategoryGroup.Game,
            RelativePath = @"sharestationdata", Granularity = Granularity.File,
            DefaultSelected = true, Risk = RiskLevel.Low, Media = DefaultMedia
        }
    ];

    /// <summary>组的展示名与顺序。</summary>
    public static readonly IReadOnlyList<(CategoryGroup Group, string Title)> GroupOrder =
    [
        (CategoryGroup.Mingyi, "茗伊插件"),
        (CategoryGroup.Lazy, "懒人插件"),
        (CategoryGroup.Game, "游戏数据")
    ];

    public static string GroupTitle(CategoryGroup group) =>
        GroupOrder.First(g => g.Group == group).Title;

    public static IEnumerable<CategoryDefinition> OfGroup(CategoryGroup group) =>
        Default.Where(c => c.Group == group);

    /// <summary>「数据清理」页展示的分类（排除只归图片页处理的截图 / 幻境云图）。</summary>
    public static IEnumerable<CategoryDefinition> OfDataTab(CategoryGroup group) =>
        OfGroup(group).Where(c => !c.MediaOnly);

    /// <summary>由「截图与云图」页处理的两类。</summary>
    public static readonly IReadOnlyList<CategoryDefinition> MediaCategories =
        Default.Where(c => c.MediaOnly).ToList();

    public static bool IsMediaOnly(CleanCategory id) => Get(id).MediaOnly;

    public static CategoryDefinition Get(CleanCategory id) =>
        Default.First(c => c.Id == id);
}

/// <summary>有效大区白名单，配置化以便官方再次合并区服时更新。</summary>
public sealed class RegionConfig
{
    public int Version { get; set; } = 1;

    public List<string> Valid { get; set; } = ["电信区", "双线区", "无界区"];

    public string LegacyHint { get; set; } = "不属于当前客户端有效区服，可能来自区服合并前的旧版本";

    public bool IsValid(string regionName) =>
        Valid.Any(v => string.Equals(v, regionName, StringComparison.Ordinal));
}

/// <summary>用户设置。持久化在程序目录 config\settings.json，随程序搬移。</summary>
public sealed class AppSettings
{
    public string? ClientRoot { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public CleanMode DefaultMode { get; set; } = CleanMode.RecycleBin;

    public TimeWindow TimeWindow { get; set; } = TimeWindow.All;

    // ---- 设置页里的五项保留策略（N / X / Y / Z / M） ----

    /// <summary>N：聊天记录保留最近几个。</summary>
    public int ChatLogKeepCount { get; set; } = 2;

    /// <summary>X：战斗记录保留最近几天。</summary>
    public int FightStatKeepDays { get; set; } = 30;

    /// <summary>Y：JCL 战斗日志保留最近几天。</summary>
    public int CombatLogKeepDays { get; set; } = 30;

    /// <summary>Z：金团记录保留最近几天。</summary>
    public int GkpKeepDays { get; set; } = 90;

    /// <summary>M：云团监数据保留最近几组。</summary>
    public int TeamMonKeepGroups { get; set; } = 5;

    /// <summary>扫描引擎使用的只读快照。</summary>
    public KeepSettings ToKeepSettings() => new()
    {
        ChatLogCount = ChatLogKeepCount,
        FightStatDays = FightStatKeepDays,
        CombatLogDays = CombatLogKeepDays,
        GkpDays = GkpKeepDays,
        TeamMonGroups = TeamMonKeepGroups
    };

    public void ApplyKeep(KeepSettings keep)
    {
        var k = keep.Clamped();
        ChatLogKeepCount = k.ChatLogCount;
        FightStatKeepDays = k.FightStatDays;
        CombatLogKeepDays = k.CombatLogDays;
        GkpKeepDays = k.GkpDays;
        TeamMonKeepGroups = k.TeamMonGroups;
    }

    /// <summary>把越界值收敛回合法区间（配置文件被手改过时的兜底）。</summary>
    public void Clamp() => ApplyKeep(ToKeepSettings());

    public AppSettings Copy() => new()
    {
        ClientRoot = ClientRoot,
        VerifiedAt = VerifiedAt,
        DefaultMode = DefaultMode,
        TimeWindow = TimeWindow,
        ChatLogKeepCount = ChatLogKeepCount,
        FightStatKeepDays = FightStatKeepDays,
        CombatLogKeepDays = CombatLogKeepDays,
        GkpKeepDays = GkpKeepDays,
        TeamMonKeepGroups = TeamMonKeepGroups
    };
}

internal static class JsonIo
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T? Read<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch
        {
            return null; // 配置损坏时回退默认值，不让程序起不来
        }
    }

    public static void Write<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }
}

public sealed class SettingsStore
{
    public AppSettings Current { get; private set; } = new();

    /// <summary>配置文件是否存在且成功读到 —— 首次启动判断用。</summary>
    public bool Exists { get; private set; }

    public void Load()
    {
        var loaded = JsonIo.Read<AppSettings>(PortablePaths.SettingsFile);
        Exists = loaded is not null;
        Current = loaded ?? new AppSettings();
        Current.Clamp();
    }

    public void Save()
    {
        Current.Clamp();
        JsonIo.Write(PortablePaths.SettingsFile, Current);
        Exists = true;
    }

    /// <summary>整体替换（设置页保存时用）：先落盘再更新内存，避免半边生效。</summary>
    public void Replace(AppSettings settings)
    {
        settings.Clamp();
        JsonIo.Write(PortablePaths.SettingsFile, settings);
        Current = settings;
        Exists = true;
    }
}

public static class RegionConfigStore
{
    public static RegionConfig Load()
    {
        var cfg = JsonIo.Read<RegionConfig>(PortablePaths.RegionsFile);
        if (cfg is null)
        {
            cfg = new RegionConfig();
            JsonIo.Write(PortablePaths.RegionsFile, cfg);
        }
        return cfg;
    }
}
