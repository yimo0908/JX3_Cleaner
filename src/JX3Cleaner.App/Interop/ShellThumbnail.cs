using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace JX3Cleaner.App.Interop;

/// <summary>
/// 用 Windows Shell 取缩略图。
/// 好处是图片和 mp4 走同一条路径：视频首帧由系统已安装的解码器提供，
/// 不必引 MediaFoundation，也不依赖第三方库。
/// 缺解码器时返回 null，由界面降级成占位块。
/// </summary>
internal static class ShellThumbnail
{
    private const uint SIIGBF_BIGGERSIZEOK = 0x01;
    private const uint SIIGBF_THUMBNAILONLY = 0x08;

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, uint flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>取缩略图；失败返回 null（调用方显示占位块）。必须在线程池调用。</summary>
    public static BitmapSource? Get(string path, int width = 320, int height = 240)
    {
        IShellItemImageFactory factory;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, iid, out var item);
            factory = (IShellItemImageFactory)item;
        }
        catch
        {
            return null;
        }

        var size = new SIZE { cx = width, cy = height };

        // 优先「只要缩略图」；视频/异常文件取不到时退回「任意可用图像」（系统图标）
        if (TryGet(factory, size, SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK, out var bmp)) return bmp;
        if (TryGet(factory, size, SIIGBF_BIGGERSIZEOK, out bmp)) return bmp;
        return null;
    }

    private static bool TryGet(IShellItemImageFactory factory, SIZE size, uint flags, out BitmapSource? result)
    {
        result = null;
        var hBitmap = IntPtr.Zero;

        try
        {
            if (factory.GetImage(size, flags, out hBitmap) != 0 || hBitmap == IntPtr.Zero) return false;

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();          // 冻结后才能跨线程交给 UI
            result = source;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
        }
    }
}
