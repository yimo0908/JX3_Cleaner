using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JX3Cleaner.App;

/// <summary>调试辅助：把窗口渲染成 PNG（软件渲染模式下 RenderTargetBitmap 一定可用）。</summary>
public static class WindowShot
{
    public static void Save(FrameworkElement visual, string path)
    {
        var width = (int)Math.Max(1, visual.ActualWidth);
        var height = (int)Math.Max(1, visual.ActualHeight);

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
