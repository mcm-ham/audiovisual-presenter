using Presenter.Core;
using Presenter.Engine.Com.Interop;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace Presenter.Engine.Com;

/// <summary>
/// COM helpers ported from App_Code/Util.cs. The extensions operate on the running
/// PowerPoint presentation and carry hard-won workarounds for PowerPoint 2010 bugs.
/// </summary>
internal static class ComUtil
{
    /// <summary>
    /// Workaround for PowerPoint 2010 bug where the SlideShowWindow property returns
    /// the last slideshow window instead of the one belonging to the presentation.
    /// </summary>
    public static readonly Dictionary<PP.Presentation, PP.SlideShowWindow> SlideShowWindows = new();

    public static PP.SlideShowWindow SlideShowWindow(this PP.Presentation key)
    {
        return SlideShowWindows.TryGetValue(key, out var wnd) ? wnd : key.SlideShowWindow;
    }

    /// <summary>
    /// Goto specified PowerPoint slide index. Workaround for PowerPoint 2010 bug where
    /// animations do not work (window must receive WM_SETFOCUS first).
    /// </summary>
    public static void GotoSlide(this PP.Presentation pres, int index)
    {
        if (Util.Parse<double>(pres.Application.Version) >= 14)
            User32.SendMessage(new IntPtr(pres.SlideShowWindow().HWND), User32.WM_SETFOCUS, IntPtr.Zero, UIntPtr.Zero);
        pres.SlideShowWindow().View.GotoSlide(index);
    }

    /// <summary>Goto next PowerPoint slide or animation (same 2010 workaround).</summary>
    public static void Next(this PP.Presentation pres)
    {
        if (Util.Parse<double>(pres.Application.Version) >= 14)
            User32.SendMessage(new IntPtr(pres.SlideShowWindow().HWND), User32.WM_SETFOCUS, IntPtr.Zero, UIntPtr.Zero);
        pres.SlideShowWindow().View.Next();
    }

    /// <summary>Goto previous PowerPoint slide or animation (same 2010 workaround).</summary>
    public static void Previous(this PP.Presentation pres)
    {
        if (Util.Parse<double>(pres.Application.Version) >= 14)
            User32.SendMessage(new IntPtr(pres.SlideShowWindow().HWND), User32.WM_SETFOCUS, IntPtr.Zero, UIntPtr.Zero);
        pres.SlideShowWindow().View.Previous();
    }

    /// <summary>Translates a WPF color to an Ole color value.</summary>
    public static int ToOle(System.Windows.Media.Color c)
    {
        return c.R | c.G << 8 | c.B << 16;
    }
}
