using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using JX3Cleaner.Core.Models;
using JX3Cleaner.Core.Rules;

namespace JX3Cleaner.App.ViewModels;

/// <summary>图片浏览页的一个媒体条目（图片或 mp4），同时是清理选中项。</summary>
public sealed class MediaItemVm : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;
    private bool _isChecked;

    public MediaItemVm(FileInfo file, CleanCategory category)
    {
        Category = category;
        FullPath = file.FullName;
        Name = file.Name;
        SizeBytes = file.Length;
        LastWrite = file.LastWriteTime;
        LastWriteUtc = file.LastWriteTimeUtc;
        Extension = file.Extension.TrimStart('.').ToUpperInvariant();
        IsVideo = file.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);

        // 分组与排序优先用文件名里的拍摄时间（规则在 Core，便于单测）
        Timestamp = MediaNaming.TimestampFromName(Name) ?? file.LastWriteTime;
        MonthKey = MediaNaming.MonthKeyOf(Timestamp);
    }

    public CleanCategory Category { get; }

    public string FullPath { get; }

    public string Name { get; }

    public string Extension { get; }

    public long SizeBytes { get; }

    /// <summary>文件本身的修改时间（界面展示用）。</summary>
    public DateTime LastWrite { get; }

    public DateTime LastWriteUtc { get; }

    /// <summary>分组 / 排序时间：文件名里的拍摄时间，取不到则退回 mtime。</summary>
    public DateTime Timestamp { get; }

    public bool IsVideo { get; }

    /// <summary>左侧时间导航用的月份键，形如 2026-10。</summary>
    public string MonthKey { get; }

    /// <summary>缩略图已请求过（不论成功失败），避免重复解码。</summary>
    public bool ThumbRequested { get; private set; }

    public void MarkThumbRequested() => ThumbRequested = true;

    /// <summary>滚出可视范围后释放缩略图，控制内存峰值。</summary>
    public void ReleaseThumbnail()
    {
        ThumbRequested = false;
        Thumbnail = null;
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasThumbnail));
        }
    }

    public bool HasThumbnail => _thumbnail is not null;

    public string DisplayName => Name;

    public string SubText => $"{TargetItem.FormatSize(SizeBytes)} · {LastWrite:yyyy-MM-dd HH:mm}";

    public string ToolTipText => $"{Name}\n{TargetItem.FormatSize(SizeBytes)}\n{LastWrite:yyyy-MM-dd HH:mm:ss}\n{FullPath}";

    /// <summary>无缩略图时占位块里显示的扩展名。</summary>
    public string FallbackText => Extension;

    /// <summary>转成清理节点，复用主流程的计划构建 / 路径守卫 / 审计。</summary>
    public ScanNode ToNode()
    {
        var node = new ScanNode
        {
            Kind = NodeKind.File,
            Category = Category,
            Title = Name,
            FullPath = FullPath,
            SizeBytes = SizeBytes,
            FileCount = 1,
            LastWriteUtc = LastWriteUtc
        };

        node.Targets.Add(new TargetItem(FullPath, SizeBytes, LastWriteUtc, false));
        return node;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
