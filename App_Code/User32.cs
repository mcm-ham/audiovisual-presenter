using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Presenter.App_Code;

public class User32
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
    public const int HWND_BACK = 1;
    public const int HWND_TOPMOST = -1;
    public const int HWND_NOTOPMOST = -2;

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    public static extern bool SetWindowPos(int hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int GetWindowTextLength(IntPtr hWnd);

    public static string GetText(IntPtr hWnd)
    {
        // Allocate correct string length first
        int length = GetWindowTextLength(hWnd);
        StringBuilder sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    public const uint WM_CHAR = 0x102;
    public const uint WM_KEYUP = 0x0101; //http://msdn.microsoft.com/en-us/library/ms646281%28VS.85%29.aspx
    public const uint WM_KEYDOWN = 0x0100; //http://msdn.microsoft.com/en-us/library/ms646281%28VS.85%29.aspx
    public const uint VK_RBUTTON = 0x02; //http://msdn.microsoft.com/en-us/library/dd375731%28v=VS.85%29.aspx
    public const uint VK_RETURN = 0x0D;
    public const uint VK_F5 = 0x74;
    public const uint WM_SETFOCUS = 0x0007;


    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, UIntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr PostMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, UIntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, ref SearchData data);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private class SearchData
    {
        public int processId;
        public string lpClassName;
        public string lpWindowName;
        public IntPtr hWnd;
    }
    private delegate bool EnumWindowsProc(IntPtr hWnd, ref SearchData data);

    public static IntPtr FindWindow(int processId, string lpClassName, string lpWindowName)
    {
        SearchData sd = new SearchData { processId = processId, lpClassName = lpClassName, lpWindowName = lpWindowName };
        EnumWindows(new EnumWindowsProc(delegate(IntPtr hWnd, ref SearchData data)
        {

            if (data.processId != 0)
            {
                uint id;
                GetWindowThreadProcessId(hWnd, out id);
                if (data.processId != id)
                    return true;
            }

            if (data.lpClassName != null)
            {
                StringBuilder sb = new StringBuilder(1024);
                GetClassName(hWnd, sb, sb.Capacity);
                if (data.lpClassName.ToLower() != sb.ToString().ToLower())
                    return true;
            }

            if (data.lpWindowName != null)
            {
                StringBuilder sb = new StringBuilder(1024);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (data.lpWindowName.ToLower() != sb.ToString().ToLower())
                    return true;
            }

            data.hWnd = hWnd;
            return false;

        }), ref sd);
        return sd.hWnd;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Creates an Image object containing a screen shot of a specific window
    /// </summary>
    /// <param name="handle">The handle to the window. (In windows forms, this is obtained by the Handle property)</param>
    /// <returns></returns>
    public static Image CaptureWindow(IntPtr handle)
    {
        // get te hDC of the target window
        IntPtr hdcSrc = User32.GetWindowDC(handle);
        // get the size
        Rect windowRect = new Rect();
        GetWindowRect(handle, ref windowRect);
        int width = windowRect.Right - windowRect.Left;
        int height = windowRect.Bottom - windowRect.Top;
        // create a device context we can copy to
        IntPtr hdcDest = GDI32.CreateCompatibleDC(hdcSrc);
        // create a bitmap we can copy it to,
        // using GetDeviceCaps to get the width/height
        IntPtr hBitmap = GDI32.CreateCompatibleBitmap(hdcSrc, width, height);
        // select the bitmap object
        IntPtr hOld = GDI32.SelectObject(hdcDest, hBitmap);
        // bitblt over
        GDI32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, GDI32.SRCCOPY);
        // restore selection
        GDI32.SelectObject(hdcDest, hOld);
        // clean up
        GDI32.DeleteDC(hdcDest);
        User32.ReleaseDC(handle, hdcSrc);
        // get a .NET image object for it
        Image img = Image.FromHbitmap(hBitmap);
        // free up the Bitmap object
        GDI32.DeleteObject(hBitmap);
        return img;
    }

    /// <summary>
    /// Helper class containing Gdi32 API functions
    /// </summary>
    private class GDI32
    {

        public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
            int nWidth, int nHeight, IntPtr hObjectSource,
            int nXSrc, int nYSrc, int dwRop);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth,
            int nHeight);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    }

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        [MarshalAs(UnmanagedType.U4)]
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags()]
    public enum DisplayDeviceStateFlags : int
    {
        /// <summary>The device is part of the desktop.</summary>
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,
        /// <summary>The device is part of the desktop.</summary>
        PrimaryDevice = 0x4,
        /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
        MirroringDriver = 0x8,
        /// <summary>The device is VGA compatible.</summary>
        VGACompatible = 0x16,
        /// <summary>The device is removable; it cannot be the primary display.</summary>
        Removable = 0x20,
        /// <summary>The device has more display modes than its output devices support.</summary>
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
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

