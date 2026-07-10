using System.Runtime.Versioning;
using Presenter.Engine.Uno.Interop;

namespace Presenter.Engine.Uno;

/// <summary>
/// OS-specific handling of the Impress slideshow window. Windows needs several
/// workarounds (locating the window among the soffice process's windows, suppressing
/// its taskbar tab, probing foreground focus for the anti-steal guard, z-ordering
/// without activation). macOS has no taskbar and identifies the show by the soffice
/// process instead of a window handle.
/// </summary>
internal interface IShowWindowController
{
    /// <summary>
    /// Finds the OS token for the running slideshow: an HWND on Windows (biggest
    /// visible window of the soffice process that owns the edit window), the soffice
    /// pid on macOS. Null while the show window has not appeared yet.
    /// </summary>
    int? FindShowWindow(long editWindowHandle, int sofficePid);

    /// <summary>One-time preparation once the show window appeared.</summary>
    void PrepareShowWindow(int token);

    /// <summary>Whether the show currently holds foreground focus (focus-guard probe).</summary>
    bool IsForeground(int token);

    /// <summary>Raises the show above sibling shows on the projector screen.</summary>
    void BringToFront(int token, int left, int top);

    /// <summary>Hides the native slideshow application/window.</summary>
    void Hide(int token);

    /// <summary>
    /// True when <see cref="BringToFront"/> steals focus (macOS can only raise
    /// another app's window by activating it); the engine then re-activates the
    /// operator window afterwards.
    /// </summary>
    bool BringToFrontActivates { get; }

    static IShowWindowController Create() => OperatingSystem.IsWindows()
        ? new WindowsShowWindowController()
        : OperatingSystem.IsMacOS()
            ? new MacShowWindowController()
            : new NullShowWindowController();
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsShowWindowController : IShowWindowController
{
    public bool BringToFrontActivates => false;

    public int? FindShowWindow(long editWindowHandle, int sofficePid)
    {
        if (editWindowHandle == 0)
            return null;

        User32.GetWindowThreadProcessId(new IntPtr(editWindowHandle), out uint pid);
        if (pid == 0)
            return null;

        int best = 0;
        long bestArea = -1;
        User32.EnumWindows((hwnd, _) =>
        {
            if (hwnd == new IntPtr(editWindowHandle) || !User32.IsWindowVisible(hwnd))
                return true;
            User32.GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid != pid || !User32.GetWindowRect(hwnd, out var r))
                return true;
            long area = (long)r.Width * r.Height;
            if (area > bestArea) // the fullscreen show window is the biggest one
            {
                bestArea = area;
                best = (int)hwnd;
            }
            return true;
        }, IntPtr.Zero);

        return best == 0 ? null : best;
    }

    public void PrepareShowWindow(int token)
    {
        var taskbarList = (ITaskbarList2)new CTaskbarList();
        taskbarList.HrInit();
        taskbarList.DeleteTab(new IntPtr(token));
        //keep the taskbar beneath the show even though the operator window has focus
        taskbarList.MarkFullscreenWindow(new IntPtr(token), true);
    }

    public bool IsForeground(int token) => User32.GetForegroundWindow() == new IntPtr(token);

    public void BringToFront(int token, int left, int top) =>
        User32.SetWindowPos(token, User32.HWND_TOP, left, top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);

    public void Hide(int token) { }
}

[SupportedOSPlatform("macos")]
internal sealed class MacShowWindowController : IShowWindowController
{
    public bool BringToFrontActivates => true;

    /// <summary>The soffice process is the token; Impress fullscreens itself.</summary>
    public int? FindShowWindow(long editWindowHandle, int sofficePid) =>
        sofficePid != 0 ? sofficePid : null;

    public void PrepareShowWindow(int token) { } // no taskbar to suppress

    public bool IsForeground(int token) => AppKit.FrontmostApplicationPid() == token;

    //raising another app's window needs activation on macOS; the engine re-activates
    //the operator window afterwards (BringToFrontActivates)
    public void BringToFront(int token, int left, int top) => AppKit.ActivateApplication(token);

    public void Hide(int token) => AppKit.HideApplication(token);
}

internal sealed class NullShowWindowController : IShowWindowController
{
    public bool BringToFrontActivates => false;
    public int? FindShowWindow(long editWindowHandle, int sofficePid) => null;
    public void PrepareShowWindow(int token) { }
    public bool IsForeground(int token) => false;
    public void BringToFront(int token, int left, int top) { }
    public void Hide(int token) { }
}
