using System.Windows;
using System.Windows.Media;

namespace JX3Cleaner.App;

/// <summary>
/// 通用二次确认弹窗。用于三处：过期区服数据转移确认、云团监备份提示、直接删除不可撤销确认。
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog()
    {
        InitializeComponent();
        Interop.WindowChrome.EnableRoundedCorners(this);
    }

    /// <summary>返回 true 表示用户确认。</summary>
    public static bool Ask(Window? owner,
                           string title,
                           string message,
                           string ackText,
                           string okText = "确认",
                           bool danger = false,
                           bool requireAck = true)
    {
        var dlg = new ConfirmDialog { Owner = owner };
        dlg.TitleText.Text = title;
        dlg.MessageText.Text = message;

        if (requireAck)
        {
            dlg.AckBox.Content = ackText;
        }
        else
        {
            dlg.AckPanel.Visibility = Visibility.Collapsed;
            dlg.OkButton.IsEnabled = true;
        }

        dlg.OkButton.Content = okText;

        if (danger)
        {
            dlg.OkButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
            dlg.OkButton.Foreground = Brushes.White;
            dlg.OkButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
        }

        return dlg.ShowDialog() == true;
    }

    private void AckChanged(object sender, RoutedEventArgs e)
    {
        if (OkButton is not null) OkButton.IsEnabled = AckBox.IsChecked == true;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
