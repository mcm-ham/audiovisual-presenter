using System;
using System.Runtime.InteropServices;
using System.Text;

public class User32
{
    [DllImport("user32.dll")]
    public static extern IntPtr SetForegroundWindow(IntPtr hWnd);

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
}
