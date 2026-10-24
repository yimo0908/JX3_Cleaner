using System.IO;
using System.Windows;
using System.Windows.Media;
using JX3Cleaner.App.Interop;
using JX3Cleaner.Core;
using JX3Cleaner.Core.Config;
using JX3Cleaner.Core.Discovery;
using JX3Cleaner.Core.Models;
using Microsoft.Win32;

namespace JX3Cleaner.App;

/// <summary>
/// 设置页。七个设置项各一行：客户端地址、默认清理方式，以及五项保留策略。
/// 首次启动（或配置文件被删）时先弹这一页，校验通过并保存后才进主界面。
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly Brush OkBrush = Freeze("#2E7D32");
    private static readonly Brush ErrorBrush = Freeze("#C62828");
    private static readonly Brush MutedBrush = Freeze("#8C8C8C");
    private static readonly Brush DarkText = Freeze("#1F1F1F");

    private readonly ClientLocator _locator = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly AppSettings _working;

    /// <summary>已通过结构校验的客户端根目录；null 表示还没定位成功。</summary>
    private string? _validatedRoot;

    private bool _scanning;
    private bool _loading = true;

    public SettingsWindow(bool firstRun)
    {
        InitializeComponent();

        WindowChrome.EnableRoundedCorners(this);

        // 编辑副本，取消（直接关窗）时原始设置不受影响
        _working = App.Service.Settings.Current.Copy();

        if (firstRun)
        {
            IntroText.Text = "第一次使用，先确认客户端地址与清理偏好。";
            FirstRunBanner.Visibility = Visibility.Visible;
            HintText.Text = "定位成功后点「保存」进入主界面。";
        }

        LoadFrom(_working);
        UpdateModeVisual();
        ShowRuntimeSummary();

        // 已有有效目录时直接显示为已校验，省得用户重新选一次
        if (App.Service.Root is { } root) SetValidated(root.Root, revalidate: false);
    }

    /// <summary>保存成功时为 true。</summary>
    public bool Saved { get; private set; }

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>显示启动自检结论：正常灰色、有提示橙色、不通过红色；悬停看完整诊断。</summary>
    private void ShowRuntimeSummary()
    {
        var check = App.RuntimeCheck;
        if (check is null)
        {
            RuntimeText.Text = "未执行环境自检";
            return;
        }

        RuntimeText.Text = check.OneLine;
        RuntimeText.ToolTip = check.ToDiagnosticText();
        RuntimeText.Foreground = check.HasErrors ? ErrorBrush
            : check.HasWarnings ? Freeze("#B26A00")
            : MutedBrush;
    }

    // ---------------- 载入 / 读取 ----------------

    private void LoadFrom(AppSettings settings)
    {
        _loading = true;
        try
        {
            RecycleRadio.IsChecked = settings.DefaultMode == CleanMode.RecycleBin;
            DeleteRadio.IsChecked = settings.DefaultMode == CleanMode.DirectDelete;

            ChatLogStepper.Value = settings.ChatLogKeepCount;
            FightStatStepper.Value = settings.FightStatKeepDays;
            CombatLogStepper.Value = settings.CombatLogKeepDays;
            GkpStepper.Value = settings.GkpKeepDays;
            TeamMonStepper.Value = settings.TeamMonKeepGroups;
        }
        finally
        {
            _loading = false;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientRoot) && Directory.Exists(settings.ClientRoot))
            SetPathText(settings.ClientRoot);
    }

    private void SetPathText(string path)
    {
        PathText.Text = path;
        PathText.Foreground = Freeze("#1F1F1F");
        PathBox.ToolTip = path;
    }

    // ---------------- 定位 ----------------

    private void Locate_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning) return;

        MessageBox.Show(this,
            "请定位至旗舰客户端zhcn_hd目录",
            "手动定位", MessageBoxButton.OK, MessageBoxImage.Information);

        var dialog = new OpenFolderDialog
        {
            Title = "请定位至旗舰客户端zhcn_hd目录",
            Multiselect = false
        };

        var current = _validatedRoot ?? _working.ClientRoot;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
            dialog.InitialDirectory = current;

        if (dialog.ShowDialog(this) != true) return;

        ApplyCandidate(dialog.FolderName);
    }

    /// <summary>校验候选目录；失败给出原因，不写入任何持久化状态。</summary>
    private bool ApplyCandidate(string picked)
    {
        var validation = _locator.Validate(picked);

        if (!validation.IsValid)
        {
            var reason = validation.HardErrors.FirstOrDefault() ?? "目录校验失败";
            SetPathText(picked);
            PathStatusText.Text = reason;
            PathStatusText.Foreground = ErrorBrush;

            MessageBox.Show(this, reason, "无法识别客户端目录",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        SetValidated(validation.Root!.Root, validation, revalidate: false);
        return true;
    }

    private void SetValidated(string root, ClientValidation? validation = null, bool revalidate = true)
    {
        if (revalidate)
        {
            validation = _locator.Validate(root);
            if (!validation.IsValid)
            {
                _validatedRoot = null;
                PathStatusText.Text = validation.HardErrors.FirstOrDefault() ?? "目录校验失败";
                PathStatusText.Foreground = ErrorBrush;
                return;
            }
        }

        _validatedRoot = root;
        SetPathText(root);

        var missing = validation?.SoftWarnings.Select(w => w.Name).ToList() ?? [];
        var zhcn = validation?.Root?.ZhcnDir ?? Path.Combine(root, "bin", "zhcn_hd");

        PathStatusText.Foreground = OkBrush;
        PathStatusText.Text = missing.Count == 0
            ? $"已校验 · {zhcn}"
            : $"已校验 · {zhcn}（{missing.Count} 个数据目录未检测到）";

        PathBox.ToolTip = $"{root}\n{zhcn}";
    }

    // ---------------- 扫盘 ----------------

    private async void ScanDisk_Click(object sender, RoutedEventArgs e)
    {
        if (_scanning) return;

        _scanning = true;
        ScanDiskButton.IsEnabled = false;
        LocateButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        ScanDiskButton.Content = "扫描中…";
        PathStatusText.Foreground = MutedBrush;
        PathStatusText.Text = "正在扫描本机固定磁盘…";

        try
        {
            var progress = new Progress<string>(text =>
            {
                PathStatusText.Foreground = MutedBrush;
                PathStatusText.Text = text;
            });

            var found = await Task.Run(() => _locator.DeepDetect(progress, _cts.Token), _cts.Token);

            if (found is null)
            {
                PathStatusText.Foreground = ErrorBrush;
                PathStatusText.Text = "未找到客户端目录，请点「手动定位」。";
                MessageBox.Show(this,
                    "没有在本机固定磁盘中找到剑网3客户端目录，请使用「手动定位」。",
                    "扫盘结束", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplyCandidate(found);
        }
        catch (OperationCanceledException)
        {
            /* 关窗取消，无需提示 */
        }
        catch (Exception ex)
        {
            PathStatusText.Foreground = ErrorBrush;
            PathStatusText.Text = ex.Message;
        }
        finally
        {
            _scanning = false;
            ScanDiskButton.IsEnabled = true;
            LocateButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
            ScanDiskButton.Content = "扫盘";
        }
    }

    // ---------------- 清理方式 ----------------

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UpdateModeVisual();
    }

    /// <summary>直接删除用红色标注，和应用内其它页面保持一致。</summary>
    private void UpdateModeVisual()
    {
        if (DeleteRadio is null) return;
        DeleteRadio.Foreground = DeleteRadio.IsChecked == true ? ErrorBrush : DarkText;
    }

    private CleanMode SelectedMode =>
        DeleteRadio.IsChecked == true ? CleanMode.DirectDelete : CleanMode.RecycleBin;

    // ---------------- 保存 ----------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_validatedRoot is null)
        {
            MessageBox.Show(this,
                "请先定位客户端地址，校验通过后才能保存。",
                "客户端地址未就绪", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 保存前再校验一次：防止这期间目录被移动 / 卸载
        var validation = _locator.Validate(_validatedRoot);
        if (!validation.IsValid)
        {
            _validatedRoot = null;
            PathStatusText.Foreground = ErrorBrush;
            PathStatusText.Text = validation.HardErrors.FirstOrDefault() ?? "目录校验失败";

            MessageBox.Show(this,
                "客户端目录已失效，请重新定位。",
                "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = _working.Copy();
        settings.ClientRoot = validation.Root!.Root;
        settings.VerifiedAt = DateTimeOffset.Now;
        settings.DefaultMode = SelectedMode;
        settings.ChatLogKeepCount = ChatLogStepper.Value;
        settings.FightStatKeepDays = FightStatStepper.Value;
        settings.CombatLogKeepDays = CombatLogStepper.Value;
        settings.GkpKeepDays = GkpStepper.Value;
        settings.TeamMonKeepGroups = TeamMonStepper.Value;

        SettingsSaved?.Invoke(settings);

        Saved = true;
        DialogResult = true;
        Close();
    }

    /// <summary>保存时把最终设置交给调用方（首次启动与主界面共用同一条落盘路径）。</summary>
    public event Action<AppSettings>? SettingsSaved;

    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel();
        _cts.Dispose();
        base.OnClosed(e);
    }
}
