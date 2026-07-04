using System.Runtime.InteropServices;

namespace Presenter.Engine.Com.Interop;

/// <summary>
/// Minimal Win32 interop the COM engine needs (subset of the app's User32 helper —
/// the engine cannot reference Presenter.App).
/// </summary>
internal static class User32
{
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOSIZE = 0x0001;
    public const int HWND_TOP = 0;

    public const uint WM_SETFOCUS = 0x0007;

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    public static extern bool SetWindowPos(int hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, UIntPtr lParam);
}
