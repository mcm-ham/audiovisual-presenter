using System.Runtime.InteropServices;

namespace Presenter.Engine.Uno.Interop;

/// <summary>
/// ITaskbarList2 COM interface, used once the Impress show window appears to remove
/// its taskbar tab and to mark it fullscreen so the taskbar stays beneath it while
/// the operator window holds focus.
/// </summary>
[ComImport, Guid("602D4995-B13A-429B-A66E-1935E44F4317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList2
{
    void HrInit();
    void AddTab([In] IntPtr hWnd);
    void DeleteTab([In] IntPtr hWnd);
    void ActivateTab([In] IntPtr hWnd);
    void SetActiveAlt([In] IntPtr hWnd);
    void MarkFullscreenWindow([In] IntPtr hWnd, [In, MarshalAs(UnmanagedType.Bool)] bool fullscreen);
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
internal class CTaskbarList
{
}
