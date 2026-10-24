using System.Reflection;
using System.Runtime.InteropServices;

namespace JX3Cleaner.Core.Environment;

/// <summary>
/// 探测当前进程的运行环境。只读、无副作用、不抛异常（失败项按“未知”处理），
/// 保证它能在启动最早期、任何怪异机器上安全跑完。
/// </summary>
public static class RuntimeProbe
{
    /// <summary>程序集元数据里写入发布形态的键名，见 App.csproj 的 AssemblyMetadata。</summary>
    public const string DeploymentMetadataKey = "DeploymentMode";

    public static RuntimeSnapshot Capture(Assembly? entryAssembly = null)
    {
        var entry = entryAssembly ?? Assembly.GetEntryAssembly();
        var (desktopRuntimes, dotnetRoot) = FindDesktopRuntimes();

        return new RuntimeSnapshot
        {
            FrameworkDescription = Safe(() => RuntimeInformation.FrameworkDescription, ".NET（未知版本）"),
            RuntimeVersion = Safe(() => System.Environment.Version, new Version(0, 0)),
            OsVersion = Safe(() => System.Environment.OSVersion.Version, new Version(0, 0)),
            OsDescription = Safe(() => RuntimeInformation.OSDescription, string.Empty),
            IsWindows = Safe(OperatingSystem.IsWindows, false),
            Is64BitProcess = Safe(() => System.Environment.Is64BitProcess, false),
            ProcessArchitecture = Safe(() => RuntimeInformation.ProcessArchitecture, Architecture.X64),
            OsArchitecture = Safe(() => RuntimeInformation.OSArchitecture, Architecture.X64),
            Deployment = DetectDeploymentMode(entry),
            InstalledDesktopRuntimes = desktopRuntimes,
            DotNetRoot = dotnetRoot
        };
    }

    // ---------------- 发布形态 ----------------

    /// <summary>
    /// 发布形态必须判准：判错的代价是「把能正常跑的自包含程序拦下来」。
    /// 因此优先读编译期写入的程序集元数据（100% 准确），元数据缺失才退化为路径推断。
    /// </summary>
    public static DeploymentMode DetectDeploymentMode(Assembly? entry)
    {
        var fromMetadata = InterpretMetadata(ReadMetadata(entry));
        if (fromMetadata != DeploymentMode.Unknown) return fromMetadata;

        // 兜底：单文件发布下 Assembly.Location 为空，只有非单文件才能这样比较
        var corelib = Safe(() => typeof(object).Assembly.Location, string.Empty);
        if (!string.IsNullOrEmpty(corelib))
        {
            var baseDir = AppContext.BaseDirectory;
            return corelib.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)
                ? DeploymentMode.SelfContained
                : DeploymentMode.FrameworkDependent;
        }

        return DeploymentMode.Unknown;
    }

    /// <summary>把元数据取值映射为枚举，单独抽出来便于单测。</summary>
    public static DeploymentMode InterpretMetadata(string? value) => value switch
    {
        "SelfContained" => DeploymentMode.SelfContained,
        "FrameworkDependent" => DeploymentMode.FrameworkDependent,
        _ => DeploymentMode.Unknown
    };

    private static string? ReadMetadata(Assembly? assembly)
    {
        if (assembly is null) return null;

        return Safe(() => assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == DeploymentMetadataKey)?.Value, null);
    }

    // ---------------- 已安装的桌面运行时 ----------------

    /// <summary>
    /// 扫共享框架目录拿 Microsoft.WindowsDesktop.App 版本列表。
    /// 只看 <c>shared\Microsoft.WindowsDesktop.App</c> 这一层子目录，不递归，毫秒级。
    /// </summary>
    public static (IReadOnlyList<Version> Versions, string? Root) FindDesktopRuntimes()
    {
        var versions = new List<Version>();
        string? hitRoot = null;

        foreach (var root in CandidateDotNetRoots())
        {
            var shared = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
            if (!Safe(() => Directory.Exists(shared), false)) continue;

            hitRoot ??= root;

            var dirs = Safe(() => Directory.GetDirectories(shared), []);
            foreach (var dir in dirs)
            {
                if (Version.TryParse(Path.GetFileName(dir), out var version))
                    versions.Add(version);
            }
        }

        // 也把「当前正在跑的运行时」算进来：框架依赖时它一定存在，
        // 即便装在非常规目录（私有 DOTNET_ROOT）也不会误报缺失。
        var running = RunningFrameworkVersion();
        if (running is not null && !versions.Contains(running)) versions.Add(running);

        return (versions.Distinct().OrderByDescending(v => v).ToList(), hitRoot);
    }

    /// <summary>各个可能的 dotnet 安装根目录，按可信度排序。</summary>
    public static IReadOnlyList<string> CandidateDotNetRoots()
    {
        var roots = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var trimmed = path.TrimEnd('\\', '/');
            if (trimmed.Length == 0) return;
            if (roots.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) return;
            roots.Add(trimmed);
        }

        // 1) 当前运行时所在目录向上回溯到根（最可靠，装在哪都能找到）
        Add(RootFromFrameworkPath(Safe(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory, string.Empty)));

        // 2) 环境变量
        Add(System.Environment.GetEnvironmentVariable("DOTNET_ROOT"));
        Add(System.Environment.GetEnvironmentVariable("DOTNET_ROOT(x64)"));
        Add(System.Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"));

        // 3) 默认安装位置
        var programFiles = Safe(() => System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), string.Empty);
        var programFilesX86 = Safe(() => System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), string.Empty);
        var localAppData = Safe(() => System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), string.Empty);

        if (programFiles.Length > 0) Add(Path.Combine(programFiles, "dotnet"));
        if (programFilesX86.Length > 0) Add(Path.Combine(programFilesX86, "dotnet"));
        if (localAppData.Length > 0) Add(Path.Combine(localAppData, "Microsoft", "dotnet"));

        return roots;
    }

    /// <summary>
    /// 从 <c>…\shared\Microsoft.NETCore.App\10.0.0\</c> 反推 <c>…</c>。
    /// 单文件 / 自包含下这个路径可能在程序目录里，那时会得到程序目录本身，属于正常情况。
    /// </summary>
    public static string? RootFromFrameworkPath(string? frameworkPath)
    {
        if (string.IsNullOrWhiteSpace(frameworkPath)) return null;

        var index = frameworkPath.IndexOf(@"\shared\", StringComparison.OrdinalIgnoreCase);
        if (index <= 0) return null;

        return frameworkPath[..index];
    }

    /// <summary>框架依赖时能拿到真实运行时版本；自包含/异常时返回 null。</summary>
    private static Version? RunningFrameworkVersion()
    {
        var dir = Safe(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory, string.Empty);
        if (string.IsNullOrWhiteSpace(dir)) return null;

        // 目录形如 …\Microsoft.NETCore.App\10.0.0\，取到的版本代表实际运行时
        var name = Path.GetFileName(dir.TrimEnd('\\'));
        return Version.TryParse(name, out var version) ? version : null;
    }

    // ---------------- 工具 ----------------

    private static T Safe<T>(Func<T> probe, T fallback)
    {
        try { return probe(); }
        catch { return fallback; }
    }
}
