namespace JX3Cleaner.Core.Config;

/// <summary>
/// 便携式路径解析。所有配置、日志、缓存一律落在程序所在目录（或下层），
/// 不使用 %LOCALAPPDATA% / %APPDATA%，保证整个程序可随文件夹整体搬移。
/// </summary>
public static class PortablePaths
{
    /// <summary>
    /// 程序根目录。单文件发布时 AppContext.BaseDirectory 即为 exe 所在目录；
    /// 不使用 Environment.ProcessPath，避免 dotnet run 场景指向 dotnet.exe。
    /// </summary>
    public static string BaseDir { get; } = ResolveBaseDir();

    public static string ConfigDir => Path.Combine(BaseDir, "config");
    public static string LogsDir => Path.Combine(BaseDir, "logs");
    public static string CacheDir => Path.Combine(BaseDir, "cache");

    public static string SettingsFile => Path.Combine(ConfigDir, "settings.json");
    public static string PathsFile => Path.Combine(ConfigDir, "paths.json");
    public static string RegionsFile => Path.Combine(ConfigDir, "regions.json");

    private static string ResolveBaseDir()
    {
        var dir = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(dir)) dir = Directory.GetCurrentDirectory();
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(LogsDir);
    }

    /// <summary>把相对路径解析到程序目录下（用于配置里写的相对日志目录等）。</summary>
    public static string Resolve(string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(BaseDir, relativeOrAbsolute);
}
