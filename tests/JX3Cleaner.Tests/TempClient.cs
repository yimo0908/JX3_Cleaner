using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Tests;

/// <summary>
/// 在临时目录里搭一个最小可用的客户端结构，供扫描 / 路径守卫测试使用。
/// 结构：{root}\bin\zhcn_hd\bin64 存在，其余目录按需创建。
/// </summary>
public sealed class TempClient : IDisposable
{
    public TempClient()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "jx3cleaner-test-" + Guid.NewGuid().ToString("N")[..10]);
        var zhcn = Path.Combine(RootDir, "bin", "zhcn_hd");
        Directory.CreateDirectory(Path.Combine(zhcn, "bin64"));

        Root = new ClientRoot(RootDir, Path.Combine(RootDir, "bin"), zhcn);
    }

    public string RootDir { get; }

    public ClientRoot Root { get; }

    /// <summary>创建目录，返回完整路径。</summary>
    public string Dir(string relative)
    {
        var full = Path.Combine(Root.ZhcnDir, relative);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>创建文件（可指定大小与 UTC 修改时间），返回完整路径。</summary>
    public string File(string relative, long bytes = 1024, DateTime? lastWriteUtc = null)
    {
        var full = Path.Combine(Root.ZhcnDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllBytes(full, new byte[bytes]);

        if (lastWriteUtc is { } utc) System.IO.File.SetLastWriteTimeUtc(full, utc);
        return full;
    }

    /// <summary>茗伊账号目录：{gid}@zhcn_hd，并在其下建角色名空目录。</summary>
    public string Account(string gid = "1000000000000000001", string playerName = "角色甲")
    {
        var dir = Dir($@"interface\my#data\{gid}@zhcn_hd");
        if (!string.IsNullOrEmpty(playerName)) Directory.CreateDirectory(Path.Combine(dir, playerName));
        return dir;
    }

    public void Dispose()
    {
        try { Directory.Delete(RootDir, true); }
        catch { /* 临时目录清理失败不影响测试结论 */ }
    }
}
