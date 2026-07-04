using System.Runtime.InteropServices;

namespace Presenter.Engine.Com.Interop;

/// <summary>
/// ITaskbarList COM interface, used to remove the PowerPoint slideshow window's tab
/// from the taskbar after SlideShowSettings.Run().
/// </summary>
[ComImport, Guid("56FDF342-FD6D-11D0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList
{
    void HrInit();
    void AddTab([In] IntPtr hWnd);
    void DeleteTab([In] IntPtr hWnd);
    void ActivateTab([In] IntPtr hWnd);
    void SetActiveAlt([In] IntPtr hWnd);
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
internal class CTaskbarList
{
}
