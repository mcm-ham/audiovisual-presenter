using System.Runtime.InteropServices;

namespace Presenter.App_Code
{
    /// <summary>
    /// Windows-only window management used while a slideshow runs (z-order juggling
    /// around PowerPoint/Impress show windows, remote-mode cursor capture). All call
    /// sites are guarded with OperatingSystem.IsWindows(); on macOS the engine's own
    /// window controller and Avalonia's Topmost handle ordering.
    /// </summary>
    public static class User32
    {
        [StructLayout(LayoutKind.Sequential)]
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

        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOSIZE = 0x0001;
        public const int HWND_TOP = 0;
        public const int HWND_TOPMOST = -1;
        public const int HWND_NOTOPMOST = -2;

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Brings a window of this process to the foreground even when another process
        /// (a PowerPoint or Impress show window) currently holds it. Windows denies a
        /// plain SetForegroundWindow from a background process, so temporarily attach
        /// to the foreground window's input queue, which grants the right.
        /// </summary>
        public static void ForceForeground(IntPtr hWnd)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == hWnd)
                return;

            uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
            uint ourThread = GetCurrentThreadId();
            if (foreground != IntPtr.Zero && foregroundThread != ourThread)
            {
                AttachThreadInput(ourThread, foregroundThread, true);
                try { SetForegroundWindow(hWnd); }
                finally { AttachThreadInput(ourThread, foregroundThread, false); }
            }
            else
            {
                SetForegroundWindow(hWnd);
            }
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Rect rect, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        /// <summary>
        /// 1-based number of the monitor whose top-left corner is (<paramref name="x"/>,
        /// <paramref name="y"/>), in the OS EnumDisplayMonitors order. This is the same
        /// order System.Windows.Forms.Screen.AllScreens uses (WinForms is built on the
        /// same enumeration) and the numbering LibreOffice's presentation Display property
        /// expects, so it matches the working WPF app exactly. Matching by position keeps
        /// it robust when two monitors share a display name. Returns 0 when none matches.
        /// </summary>
        public static int MonitorNumberAt(int x, int y)
        {
            int found = 0, i = 0;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref Rect r, IntPtr _) =>
            {
                i++;
                if (found == 0 && r.Left == x && r.Top == y)
                    found = i;
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
