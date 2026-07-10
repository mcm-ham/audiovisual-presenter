using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Presenter.Engine.Uno.Interop;

/// <summary>
/// Minimal AppKit interop for macOS via the Objective-C runtime: activating the
/// soffice application that runs a slideshow (NSRunningApplication) and reading
/// which application is frontmost (NSWorkspace). Needs no TCC permissions.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class AppKit
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr receiver, IntPtr selector, int arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern bool MsgSendBool(IntPtr receiver, IntPtr selector, nuint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern bool MsgSendBool(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern int MsgSendInt(IntPtr receiver, IntPtr selector);

    // AppKit must be loaded before its classes resolve (console hosts don't link it)
    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    private static readonly IntPtr _appKit =
        dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", 1 /* RTLD_LAZY */);

    private const nuint NSApplicationActivateAllWindows = 1 << 0;
    private const nuint NSApplicationActivateIgnoringOtherApps = 1 << 1;

    /// <summary>Activates (and thereby raises) the application with the given pid.</summary>
    public static bool ActivateApplication(int pid)
    {
        IntPtr app = MsgSend(objc_getClass("NSRunningApplication"),
            sel_registerName("runningApplicationWithProcessIdentifier:"), pid);
        if (app == IntPtr.Zero)
            return false;
        MsgSendBool(app, sel_registerName("unhide"));
        return MsgSendBool(app, sel_registerName("activateWithOptions:"),
            NSApplicationActivateAllWindows | NSApplicationActivateIgnoringOtherApps);
    }

    /// <summary>Hides the application with the given pid and all of its windows.</summary>
    public static bool HideApplication(int pid)
    {
        IntPtr app = MsgSend(objc_getClass("NSRunningApplication"),
            sel_registerName("runningApplicationWithProcessIdentifier:"), pid);
        return app != IntPtr.Zero && MsgSendBool(app, sel_registerName("hide"));
    }

    /// <summary>Pid of the frontmost application, or 0 when unknown.</summary>
    public static int FrontmostApplicationPid()
    {
        IntPtr workspace = MsgSend(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
        IntPtr app = MsgSend(workspace, sel_registerName("frontmostApplication"));
        if (app == IntPtr.Zero)
            return 0;
        return MsgSendInt(app, sel_registerName("processIdentifier"));
    }
}
