using System;
using System.Runtime.InteropServices;
using Presenter.App_Code;

public class User32
{
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool ClipCursor(ref Rect lpRect);

    [DllImport("user32.dll")]
    public static extern bool GetClipCursor(ref Rect lpRect);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool SetWindowPos(int hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOSIZE = 0x0001;
    private const int HWND_TOP = 0;
    private const int HWND_TOPMOST = -1;
    private const int HWND_NOTOPMOST = -2;

    public static void SendWindowToFront(int hwnd)
    {
        SetWindowPos(hwnd, HWND_TOP, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE);
    }

    public static void SetWindowTopmost(int hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE);
    }

    public static void SetWindowNotTopmost(int hwnd)
    {
        SetWindowPos(hwnd, HWND_NOTOPMOST, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE);
    }
}

[ComImport, Guid("56FDF342-FD6D-11D0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITaskbarList
{
    /// <summary>
    /// Initializes the taskbar list object. This method must be called before any other ITaskbarList methods can be called.
    /// </summary>
    void HrInit();

    /// <summary>
    /// Adds an item to the taskbar.
    /// </summary>
    /// <param name="hWnd">A handle to the window to be added to the taskbar.</param>
    void AddTab([In] IntPtr hWnd);

    /// <summary>
    /// Deletes an item from the taskbar.
    /// </summary>
    /// <param name="hWnd">A handle to the window to be deleted from the taskbar.</param>
    void DeleteTab([In] IntPtr hWnd);

    /// <summary>
    /// Activates an item on the taskbar. The window is not actually activated; the window's item on the taskbar is merely displayed as active.
    /// </summary>
    /// <param name="hWnd">A handle to the window on the taskbar to be displayed as active.</param>
    void ActivateTab([In] IntPtr hWnd);

    /// <summary>
    /// Marks a taskbar item as active but does not visually activate it.
    /// </summary>
    /// <param name="hWnd">A handle to the window to be marked as active.</param>
    void SetActiveAlt([In] IntPtr hWnd);
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
public class CTaskbarList
{
}
