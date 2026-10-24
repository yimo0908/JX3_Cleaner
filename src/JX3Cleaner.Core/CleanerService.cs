using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Discovery;
using JX3Cleaner.Core.Executing;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Planning;
using JX3Cleaner.Core.Scanning;

namespace JX3Cleaner.Core;

/// <summary>对外门面：校验 → 扫描 → 计划 → 执行，界面层只跟它打交道。</summary>
public sealed class CleanerService
{
    private readonly ClientLocator _locator = new();

    public SettingsStore Settings { get; } = new();
    public RegionConfig Regions { get; private set; } = new();
    public ClientRoot? Root { get; private set; }

    /// <summary>配置里没记下有效客户端目录 —— 首次启动或配置被删，必须先走设置页。</summary>
    public bool NeedsSetup => Root is null;

    public void Initialize()
    {
        PortablePaths.EnsureDirectories();
        Settings.Load();
        Regions = RegionConfigStore.Load();

        if (!string.IsNullOrWhiteSpace(Settings.Current.ClientRoot))
        {
            var validation = _locator.Validate(Settings.Current.ClientRoot);
            if (validation.IsValid) Root = validation.Root;
            else Root = null;   // 目录被移动 / 卸载：要求重新定位
        }
    }

    public ClientValidation Validate(string path)
    {
        var validation = _locator.Validate(path);
        if (validation.IsValid)
        {
            Root = validation.Root;
            Settings.Current.ClientRoot = validation.Root!.Root;
            Settings.Current.VerifiedAt = DateTimeOffset.Now;
            Settings.Save();
        }
        return validation;
    }

    /// <summary>设置页保存：整体替换设置（保留策略 + 默认清理方式 + 客户端目录）。</summary>
    public void SaveSettings(AppSettings settings)
    {
        Settings.Replace(settings);

        if (!string.IsNullOrWhiteSpace(settings.ClientRoot))
        {
            var validation = _locator.Validate(settings.ClientRoot);
            Root = validation.IsValid ? validation.Root : null;
        }
        else
        {
            Root = null;
        }
    }

    public string? AutoDetect() => _locator.AutoDetect();

    /// <summary>扫盘：在固定磁盘上做一次有限深度的搜索，仅返回通过校验的路径。</summary>
    public string? DeepDetect(IProgress<string>? progress = null, CancellationToken ct = default) =>
        _locator.DeepDetect(progress, ct);

    public ScanResult Scan(ScanOptions? options = null, Action<string>? progress = null, CancellationToken ct = default)
    {
        if (Root is null) throw new InvalidOperationException("尚未定位客户端目录");

        options ??= new ScanOptions
        {
            Window = Settings.Current.TimeWindow,
            Keep = Settings.Current.ToKeepSettings()
        };

        var engine = new ScanEngine(Regions);
        return engine.Scan(Root, options, ct, progress);
    }

    public CleanPlan BuildPlan(IEnumerable<ScanNode> selected)
    {
        if (Root is null) throw new InvalidOperationException("尚未定位客户端目录");
        return CleanPlanBuilder.Build(selected, new PathGuard(Root));
    }

    public static ICleanExecutor CreateExecutor(CleanMode mode) =>
        mode == CleanMode.RecycleBin ? new RecycleBinExecutor() : new DirectDeleteExecutor();

    public Task<CleanOutcome> ExecuteAsync(CleanPlan plan, CleanMode mode,
                                           IProgress<CleanProgress>? progress = null,
                                           CancellationToken ct = default) =>
        CreateExecutor(mode).ExecuteAsync(plan, progress, ct);
}
