using System.Runtime.InteropServices;

namespace JX3Cleaner.Core.Interop;

[ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperation
{
    [PreserveSig] int Advise(IntPtr pfops, out uint cookie);
    [PreserveSig] int Unadvise(uint cookie);
    [PreserveSig] int SetOperationFlags(uint dwOperationFlags);
    [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
    [PreserveSig] int SetProgressDialog(IntPtr popd);
    [PreserveSig] int SetProperties(IntPtr pproparray);
    [PreserveSig] int SetOwnerWindow(IntPtr hwndOwner);
    [PreserveSig] int ApplyPropertiesToItem(IShellItem psiItem);
    [PreserveSig] int ApplyPropertiesToItems(IntPtr punkItems);
    [PreserveSig] int RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
    [PreserveSig] int RenameItems(IntPtr punkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    [PreserveSig] int MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
    [PreserveSig] int MoveItems(IntPtr punkItems, IShellItem psiDestinationFolder);
    [PreserveSig] int CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, IntPtr pfopsItem);
    [PreserveSig] int CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
    [PreserveSig] int DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
    [PreserveSig] int DeleteItems(IntPtr punkItems);
    [PreserveSig] int NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes,
                              [MarshalAs(UnmanagedType.LPWStr)] string pszName,
                              [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName,
                              IntPtr pfopsItem);
    [PreserveSig] int PerformOperations();
    [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
}

[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetParent(out IShellItem ppsi);
    [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
    [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
}

/// <summary>Windows Shell 文件操作封装（回收站删除）。</summary>
internal static class ShellFileOps
{
    internal const uint FOF_SILENT = 0x0004;
    internal const uint FOF_NOCONFIRMATION = 0x0010;
    internal const uint FOF_ALLOWUNDO = 0x0040;
    internal const uint FOF_NOCONFIRMMKDIR = 0x0200;
    internal const uint FOF_NOERRORUI = 0x0400;

    private static readonly Guid ClsidFileOperation = new("3AD05575-8857-4850-9277-11B85BDB8E09");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern IShellItem SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid);

    private static readonly Guid IID_IShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    internal static IFileOperation Create()
    {
        var type = Type.GetTypeFromCLSID(ClsidFileOperation, throwOnError: true)!;
        return (IFileOperation)Activator.CreateInstance(type)!;
    }

    internal static IShellItem CreateItem(string path)
    {
        var iid = IID_IShellItem;
        return SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid);
    }

    /// <summary>把一批路径送进回收站。返回首个失败的 HRESULT（0 表示全部成功）。</summary>
    internal static (int HResult, bool Aborted) DeleteToRecycleBin(IEnumerable<string> paths)
    {
        var op = Create();
        var hr = op.SetOperationFlags(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMMKDIR);
        if (hr != 0) return (hr, false);

        foreach (var path in paths)
        {
            var item = CreateItem(path);
            hr = op.DeleteItem(item, IntPtr.Zero);
            if (hr != 0) return (hr, false);
            Marshal.ReleaseComObject(item);
        }

        hr = op.PerformOperations();
        if (hr != 0) return (hr, false);

        op.GetAnyOperationsAborted(out var aborted);
        return (0, aborted);
    }

    internal static string DescribeHResult(int hr) => hr switch
    {
        0 => "成功",
        unchecked((int)0x80070020) => "文件被占用",
        unchecked((int)0x80070005) => "拒绝访问（权限不足）",
        unchecked((int)0x80070003) => "路径不存在",
        unchecked((int)0x80070057) => "参数错误（路径可能过长）",
        _ => $"HRESULT 0x{hr:X8}"
    };
}
