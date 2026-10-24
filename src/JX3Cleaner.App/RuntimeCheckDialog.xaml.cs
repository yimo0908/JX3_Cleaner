using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using JX3Cleaner.Core.Environment;

namespace JX3Cleaner.App;

/// <summary>
/// 启动时的运行环境自检弹窗。只在「自检发现致命问题」时出现：
/// 缺 .NET 桌面运行时 / 系统版本过低 / 位数不对 / 程序被 32 位重定向。
/// 有错误时只给「退出」，没有错误时可继续。
/// </summary>
public partial class RuntimeCheckDialog : Window
{
    private static readonly Brush ErrorBrush = Freeze("#C62828");
    private static readonly Brush WarningBrush = Freeze("#B26A00");
    private static readonly Brush InfoBrush = Freeze("#8C8C8C");

    private readonly RuntimeCheckResult _result;

    public RuntimeCheckDialog(RuntimeCheckResult result, bool fatal)
    {
        InitializeComponent();

        _result = result;
        Interop.WindowChrome.EnableRoundedCorners(this);

        if (fatal)
        {
            Title = "运行环境自检 · 无法启动";
            HeadlineText.Text = result.Headline;
            HeadlineText.Foreground = ErrorBrush;
            HeadlineText.FontWeight = FontWeights.Medium;
            PrimaryButton.Content = "退出";
        }
        else
        {
            HeadlineText.Text = result.Headline;
            HeadlineText.Foreground = WarningBrush;
            PrimaryButton.Content = "继续";
        }

        OneLineText.Text = result.OneLine;

        IssueList.ItemsSource = result.Issues
            .OrderByDescending(i => i.Level)
            .Select(i => new IssueRow(
                i.Level switch
                {
                    RuntimeIssueLevel.Error => ErrorBrush,
                    RuntimeIssueLevel.Warning => WarningBrush,
                    _ => InfoBrush
                },
                i.Title,
                i.Detail))
            .ToList();

        DownloadButton.Visibility = result.Issues.Any(i => !string.IsNullOrWhiteSpace(i.HelpUrl))
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>显示自检结果并返回是否允许继续（fatal 恒为 false）。</summary>
    public static bool Show(RuntimeCheckResult result, bool fatal)
    {
        var dialog = new RuntimeCheckDialog(result, fatal);
        return dialog.ShowDialog() == true && !fatal;
    }

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_result.ToDiagnosticText());
            CopyButton.Content = "已复制";
        }
        catch
        {
            CopyButton.Content = "复制失败";
        }
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        var url = _result.Issues.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.HelpUrl))?.HelpUrl;
        if (url is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(url, "请手动打开以下地址", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = PrimaryButton.Content as string == "继续";
        Close();
    }

    /// <summary>列表行视图模型（只读展示，不需要通知）。</summary>
    private sealed record IssueRow(Brush Marker, string Title, string Detail);
}
