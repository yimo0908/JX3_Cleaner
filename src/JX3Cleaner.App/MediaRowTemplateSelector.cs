using System.Windows;
using System.Windows.Controls;
using JX3Cleaner.App.ViewModels;

namespace JX3Cleaner.App;

/// <summary>按行类型挑模板：月份标题行 / 卡片行。</summary>
public sealed class MediaRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }

    public DataTemplate? RowTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is MediaRowVm { IsHeader: true } ? HeaderTemplate : RowTemplate;
}
