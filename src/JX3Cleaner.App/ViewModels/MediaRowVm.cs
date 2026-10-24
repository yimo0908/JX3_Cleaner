using JX3Cleaner.Core.Models;

namespace JX3Cleaner.App.ViewModels;

/// <summary>
/// 缩略图网格的一行。虚拟化以「行」为单位：一次只实体化可视范围内的行，
/// 行内的卡片才随之创建、才去请求缩略图。
/// </summary>
public sealed class MediaRowVm
{
    /// <summary>月份标题行：Items 为空。</summary>
    public bool IsHeader => Items.Count == 0;

    public string HeaderText { get; init; } = string.Empty;

    public string HeaderSubText { get; init; } = string.Empty;

    public IReadOnlyList<MediaItemVm> Items { get; init; } = [];

    public int Columns { get; init; } = 1;

    /// <summary>本行首项在整个列表中的下标；标题行为 -1，用于缩放时保持滚动位置。</summary>
    public int StartIndex { get; init; } = -1;
}

/// <summary>左侧时间导航的一项（一个自然月）。</summary>
public sealed class MediaMonthVm
{
    public required string Key { get; init; }

    public required string HeaderText { get; init; }

    public int Count { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>该月在行列表中的起始行号，点击导航时据此定位。</summary>
    public int RowIndex { get; set; } = -1;

    public string Display => Key;

    public string Summary => $"{Count} 项 · {TargetItem.FormatSize(SizeBytes)}";
}
