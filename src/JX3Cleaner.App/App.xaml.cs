using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using JX3Cleaner.Core;
using JX3Cleaner.Core.Environment;

namespace JX3Cleaner.App;

public partial class App : Application
{
    /// <summary>全局唯一的服务门面。配置全部落在程序目录，不写注册表与 AppData。</summary>
    public static CleanerService Service { get; } = new();

    /// <summary>调试用：--shot &lt;png 路径&gt; 扫描完成后把窗口渲染成图片并退出。</summary>
    public static string? ScreenshotPath { get; private set; }

    /// <summary>调试用：--tab media 截图时先切到「截图与云图」页。</summary>
    public static bool ShotMediaTab { get; private set; }

    /// <summary>调试用：--month N 截图前点击左侧第 N 个月份，验证时间导航定位。</summary>
    public static int ShotMonth { get; private set; } = -1;

    /// <summary>调试用：--settings 截图对象改为设置页。</summary>
    public static bool ShotSettings { get; private set; }

    /// <summary>调试用：--days N 截图前把「文件时间」切到对应天数，验证过滤提示。</summary>
    public static int ShotDays { get; private set; }

    /// <summary>调试用：--runtime 渲染「运行环境自检」弹窗（用构造出来的失败场景）。</summary>
    public static bool ShotRuntime { get; private set; }

    /// <summary>启动时自检的结果，设置页底部显示一行摘要。</summary>
    public static RuntimeCheckResult? RuntimeCheck { get; private set; }

    public App()
    {
        // 在应用启动的极早期强制软件渲染：
        // 牺牲部分图形性能，换取对老旧显卡 / 远程桌面 / 虚拟机的极高稳定性。
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ParseArgs(e.Args);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "发生未处理的错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);

        // 启动期间会先弹设置页、后开主窗口，中间「没有任何窗口」不能触发退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 运行环境自检放在最前面：
        // 缺运行时 / 系统过旧 / 位数不对这些情况必须给出中文可操作的结论，而不是崩在别处。
        RuntimeCheck = RuntimeRequirements.Evaluate(RuntimeProbe.Capture());

        if (ShotRuntime)
        {
            ShotRuntimeDialog(SimulatedFailure());
            return;
        }

        if (RuntimeCheck.HasErrors)
        {
            RuntimeCheckDialog.Show(RuntimeCheck, fatal: true);
            Shutdown(2);
            return;
        }

        Service.Initialize();

        if (ShotSettings)
        {
            ShotSettingsPage();
            return;
        }

        // 首次启动 / 配置文件被删 / 记录的目录已失效 → 必须先过设置页
        if (Service.NeedsSetup && !ShowFirstRunSettings())
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private static void ParseArgs(string[] args)
    {
        var shotIndex = Array.IndexOf(args, "--shot");
        if (shotIndex >= 0 && shotIndex + 1 < args.Length)
            ScreenshotPath = args[shotIndex + 1];

        ShotMediaTab = args.Contains("--tab") && args.Contains("media");
        ShotSettings = args.Contains("--settings");
        ShotRuntime = args.Contains("--runtime");

        var monthIndex = Array.IndexOf(args, "--month");
        if (monthIndex >= 0 && monthIndex + 1 < args.Length &&
            int.TryParse(args[monthIndex + 1], out var parsed))
            ShotMonth = parsed;

        var daysIndex = Array.IndexOf(args, "--days");
        if (daysIndex >= 0 && daysIndex + 1 < args.Length &&
            int.TryParse(args[daysIndex + 1], out var days))
            ShotDays = days;
    }

    /// <summary>首次启动设置页：保存后返回 true，直接关窗视为放弃（退出程序）。</summary>
    private static bool ShowFirstRunSettings()
    {
        var dialog = new SettingsWindow(firstRun: true);
        dialog.SettingsSaved += settings => Service.SaveSettings(settings);

        var ok = dialog.ShowDialog() == true;
        return ok && !Service.NeedsSetup;
    }

    /// <summary>
    /// 调试用：构造一个「框架依赖 + 运行时版本过低」的假快照，
    /// 保证在自检全绿的开发机上也能截到失败态弹窗。
    /// </summary>
    private static RuntimeCheckResult SimulatedFailure() => RuntimeRequirements.Evaluate(new RuntimeSnapshot
    {
        FrameworkDescription = ".NET 8.0.0",
        RuntimeVersion = new Version(8, 0, 0),
        OsVersion = new Version(10, 0, 19045),
        OsDescription = "Microsoft Windows 10 专业版 10.0.19045",
        IsWindows = true,
        Is64BitProcess = true,
        ProcessArchitecture = Architecture.X64,
        OsArchitecture = Architecture.X64,
        Deployment = DeploymentMode.FrameworkDependent,
        InstalledDesktopRuntimes = [new Version(8, 0, 0)],
        DotNetRoot = @"C:\Program Files\dotnet"
    });

    /// <summary>调试断言：把环境自检弹窗渲染成 PNG 后退出。</summary>
    private async void ShotRuntimeDialog(RuntimeCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(ScreenshotPath))
        {
            Shutdown();
            return;
        }

        var dialog = new RuntimeCheckDialog(result, fatal: result.HasErrors)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 40,
            Top = 40
        };

        dialog.Show();
        await Task.Delay(500);

        try
        {
            WindowShot.Save(dialog, ScreenshotPath);
        }
        finally
        {
            Shutdown();
        }
    }

    /// <summary>调试断言：把设置页渲染成 PNG 后退出。</summary>
    private async void ShotSettingsPage()
    {
        if (string.IsNullOrWhiteSpace(ScreenshotPath))
        {
            Shutdown();
            return;
        }

        var window = new SettingsWindow(firstRun: Service.NeedsSetup)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 40,
            Top = 40
        };

        window.Show();
        await Task.Delay(700);   // 等布局与字体完成

        try
        {
            WindowShot.Save(window, ScreenshotPath);
        }
        finally
        {
            Shutdown();
        }
    }
}
