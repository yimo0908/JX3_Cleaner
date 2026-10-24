namespace JX3Cleaner.Core.Models;

/// <summary>归一化后的客户端根目录信息。</summary>
public sealed record ClientRoot(string Root, string BinDir, string ZhcnDir)
{
    public string MyDataDir => Path.Combine(ZhcnDir, "interface", "my#data");
    public string InterfaceDir => Path.Combine(ZhcnDir, "interface");
    public string UserDataDir => Path.Combine(ZhcnDir, "userdata");
    public string Bin64Dir => Path.Combine(ZhcnDir, "bin64");

    public string Rel(params string[] parts) => Path.Combine([ZhcnDir, .. parts]);
}

public sealed record CheckItem(string Name, string Path, bool Exists, bool Required, string? Hint = null);

/// <summary>目录校验结果。硬性项失败即阻断，软性项只影响对应分类可用性。</summary>
public sealed class ClientValidation
{
    public ClientRoot? Root { get; init; }
    public List<CheckItem> Checks { get; init; } = [];
    public List<string> HardErrors { get; init; } = [];

    public bool IsValid => HardErrors.Count == 0 && Root is not null;
    public IEnumerable<CheckItem> SoftWarnings => Checks.Where(c => !c.Required && !c.Exists);

    public string Summary => IsValid
        ? SoftWarnings.Any()
            ? $"已校验，{SoftWarnings.Count()} 个目录未检测到"
            : "已校验"
        : HardErrors.FirstOrDefault() ?? "未校验";
}
