using System.Runtime.InteropServices;
using DirectShowLib;

namespace VisionFlow.Devices;

/// <summary>
/// 弹出过滤器自带的属性页对话框（由驱动提供）。
/// </summary>
public static class PropertyPage
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int OleCreatePropertyFrame(
        IntPtr hwndOwner,
        int x, int y,
        string caption,
        int cObjects,
        [MarshalAs(UnmanagedType.Interface)] ref object ppUnk,
        int cPages,
        IntPtr pPageClsId,
        int lcid,
        int dwReserved,
        IntPtr pvReserved);

    /// <summary>
    /// 弹出指定过滤器的原生属性页。用户关闭对话框后方法返回。
    /// 如果过滤器不支持属性页，直接返回 false。
    /// </summary>
    public static bool Show(IBaseFilter filter, IntPtr ownerHwnd, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter is not ISpecifyPropertyPages specifyPages)
        {
            return false;
        }

        var hr = specifyPages.GetPages(out var pages);
        if (hr < 0 || pages.cElems == 0)
        {
            return false;
        }

        try
        {
            object filterObj = filter;
            hr = OleCreatePropertyFrame(
                ownerHwnd,
                0, 0,
                caption ?? "属性",
                1,
                ref filterObj,
                (int)pages.cElems,
                pages.pElems,
                0, 0,
                IntPtr.Zero);

            return hr >= 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pages.pElems);
        }
    }
}