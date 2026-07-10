"""Slideshow host for the UNO presentation engine.

One host owns one soffice instance and one loaded presentation; the C# engine runs a
host per PowerPoint schedule item and exchanges one JSON object per line with it.

Requests carry an ``id`` and are answered with a matching ``{"id": n, ...}`` line;
unsolicited events (slide changes from timings/clicks in the show window, the show
being closed) are emitted without an id:

  -> {"id":1,"cmd":"start","display":2,"withTimings":true}
  <- {"id":1,"ok":true,"slide":0}
  <- {"event":"slideChanged","index":3}
  <- {"event":"showEnded"}

The host runs in one of two modes, same protocol either way:

* Windows — out of process on LibreOffice's bundled python.exe (which ships the
  ``uno`` bridge module, so a plain LibreOffice install is the only dependency).
  This script spawns soffice, connects over a URP pipe and speaks JSON lines over
  stdin/stdout.

      python.exe slideshow_host.py <soffice.exe> <profile-dir> <pipe-name> <document>

* macOS — in process inside soffice via the Python script provider. The bundled
  Python is signed with parent launch constraints (a TCC-bypass CVE fix in
  LibreOffice >= 25.2.4), so only soffice itself may run it: the C# engine instead
  copies this script into the instance profile (user/Scripts/python/), binds
  ``start_host`` to the OnStartApp application event in registrymodifications.xcu
  and spawns soffice directly. The script then connects out to the C# engine's
  loopback listener (AVP_HOST_PORT) and speaks JSON lines over that socket, using
  the in-process component context (no URP pipe, no second process).
"""
import json
import os
import subprocess
import sys
import threading
import time
import traceback
import ctypes

import uno
import unohelper
from com.sun.star.awt import XCallback
from com.sun.star.beans import PropertyValue
from com.sun.star.presentation import XSlideShowListener

ON_CLICK = 1        # com.sun.star.presentation.EffectNodeType
MAIN_SEQUENCE = 4


def prop(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


class _MainThreadCall(unohelper.Base, XCallback):
    def __init__(self, fn):
        self.fn = fn
        self.result = None
        self.error = None
        self.done = threading.Event()

    def notify(self, data):
        try:
            self.result = self.fn()
        except Exception as e:
            self.error = e
        finally:
            self.done.set()


def run_on_main(ctx, fn, timeout=60):
    """Runs fn on soffice's main thread. In-process UNO calls execute directly on
    the calling thread, but window management (starting/ending a slideshow, closing
    a document) must happen on the UI thread — most visibly on macOS, where AppKit
    windows only work from the main thread."""
    call = _MainThreadCall(fn)
    cb = ctx.ServiceManager.createInstanceWithContext("com.sun.star.awt.AsyncCallback", ctx)
    cb.addCallback(call, None)
    if not call.done.wait(timeout):
        raise RuntimeError("main-thread call timed out")
    if call.error is not None:
        raise call.error
    return call.result


class ShowListener(unohelper.Base, XSlideShowListener):
    """Forwards slideshow window activity to the C# engine as events."""

    def __init__(self, host):
        self.host = host

    def _index(self):
        try:
            return self.host.controller.getCurrentSlideIndex()
        except Exception:
            return -1

    # XSlideShowListener
    def paused(self):
        pass

    def resumed(self):
        pass

    def slideTransitionStarted(self):
        self.host.emit({"event": "slideChanged", "index": self._index()})

    def slideTransitionEnded(self):
        pass

    def slideAnimationsEnded(self):
        pass

    def slideEnded(self, reverse):
        pass

    def hyperLinkClicked(self, link):
        pass

    # XAnimationListener (base of XSlideShowListener)
    def beginEvent(self, node):
        pass

    def endEvent(self, node):
        pass

    def repeat(self, node):
        pass

    # XEventListener: the show window was closed (ESC or end of show)
    def disposing(self, source):
        self.host.controller = None
        self.host.emit({"event": "showEnded"})


class Host:
    def __init__(self, document, write_line, run_ui=None, show_bounds=None):
        self.document = document
        self.ctx = None
        self.desktop = None
        self.doc = None
        self.controller = None
        self.listener = None
        self.proc = None    # the spawned soffice, out-of-process mode only
        self.page_changes = []  # original (Change, Duration) per page for setTimings
        self._write_line = write_line
        self._emit_lock = threading.Lock()
        # window management calls; in-process mode routes them to the main thread
        self.run_ui = run_ui or (lambda fn: fn())
        # macOS: (x, y, w, h) of the projector screen. The show runs in a window
        # covering that area instead of fullscreen — starting a native macOS
        # fullscreen Space from a background soffice hangs (isRunning never turns
        # true on LibreOffice 26.2), and windowed shows also z-order/switch without
        # Space-transition animations, matching how the engine manages windows.
        self.show_bounds = show_bounds

    def emit(self, obj):
        with self._emit_lock:
            self._write_line(json.dumps(obj))

    def attach(self, ctx, soffice_pid):
        """Loads the document in the given component context and reports it."""
        self.ctx = ctx
        self.desktop = ctx.ServiceManager.createInstanceWithContext(
            "com.sun.star.frame.Desktop", ctx)

        self.disable_presenter_console()

        url = uno.systemPathToFileUrl(self.document)
        # read-only: never lock or modify the user's file (the same file may be loaded
        # twice in one schedule); minimized: the edit window must stay out of the way
        # (not in windowed-show mode, where the show renders inside that window)
        props = [prop("ReadOnly", True)]
        if self.show_bounds is None:
            props.append(prop("MinimizeWindow", True))
        _debug_log("[attach] loading %s" % self.document)
        self.doc = self.desktop.loadComponentFromURL(url, "_blank", 0, tuple(props))
        _debug_log("[attach] loaded")
        if self.doc is None:
            raise RuntimeError("could not load " + self.document)

        self._report(soffice_pid)

    def attach_existing(self, ctx, soffice_pid, timeout=60):
        """In-process mode: soffice was started with the document on its command line
        (loading from a background thread hangs on a freshly initialised profile), so
        find the presentation it opened instead of loading one."""
        self.ctx = ctx
        self.desktop = ctx.ServiceManager.createInstanceWithContext(
            "com.sun.star.frame.Desktop", ctx)

        self.disable_presenter_console()

        deadline = time.time() + timeout
        while self.doc is None and time.time() < deadline:
            try:
                comps = self.desktop.Components.createEnumeration()
                while comps.hasMoreElements():
                    c = comps.nextElement()
                    if c.supportsService("com.sun.star.presentation.PresentationDocument"):
                        self.doc = c
                        break
            except Exception:
                pass
            if self.doc is None:
                time.sleep(0.25)
        if self.doc is None:
            raise RuntimeError("presentation document did not open")
        _debug_log("[attach] found open document")

        self._report(soffice_pid)

    def _report(self, soffice_pid):
        # the document window belongs to soffice.bin (not the process spawned by the
        # engine); its handle lets the C# engine locate the right process's slideshow
        # window on Windows. Fails harmlessly (0) on other platforms.
        hwnd = 0
        try:
            win = self.doc.CurrentController.Frame.ContainerWindow
            hwnd = int(win.getWindowHandle(uno.ByteSequence(b""), 1))  # 1 = SYSTEM_WIN32
        except Exception:
            pass

        self.emit({"event": "loaded", "pid": soffice_pid, "hwnd": hwnd,
                   "slides": self.slide_info()})

    def disable_presenter_console(self):
        """The Presenter Console would take over the operator's screen (the show itself
        goes to the configured display); the app has its own preview/notes UI, so turn
        it off in this soffice instance's dedicated profile."""
        try:
            cp = self.ctx.ServiceManager.createInstanceWithContext(
                "com.sun.star.configuration.ConfigurationProvider", self.ctx)
            node = prop("nodepath", "/org.openoffice.Office.Impress/Misc")
            cfg = cp.createInstanceWithArguments(
                "com.sun.star.configuration.ConfigurationUpdateAccess", (node,))
            cfg.getByName("Start").setPropertyValue("EnablePresenterScreen", False)
            cfg.commitChanges()
        except Exception:
            pass  # older LibreOffice may lack the key; the show still runs

    def slide_info(self):
        pages = self.doc.DrawPages
        info = []
        for i in range(pages.Count):
            page = pages.getByIndex(i)
            self.page_changes.append((page.Change, page.Duration))
            info.append({
                "text": self.text_summary(page),
                "notes": self.text_summary(page.NotesPage),
                "clickEffects": self.click_effect_count(page),
                "advanceOnTime": page.Change == 1,
            })
        return info

    @staticmethod
    def text_summary(page):
        parts = []
        for i in range(page.Count):
            shape = page.getByIndex(i)
            try:
                text = shape.getString().replace("\r", " ").replace("\n", " ").strip()
            except Exception:
                continue
            # skip leading bare slide numbers, mirroring the COM engine's summary
            if not parts:
                try:
                    int(text)
                    continue
                except ValueError:
                    pass
            if text:
                parts.append(text)
        return " ".join(parts)

    @staticmethod
    def click_effect_count(page):
        """Number of click-triggered effect groups in the page's main sequence
        (the ON_CLICK marker sits on the first effect node of each click group,
        nested a few levels below the MAIN_SEQUENCE container)."""

        def node_type(node):
            for nv in node.UserData or ():
                if nv.Name == "node-type":
                    return nv.Value
            return None

        def count_clicks(node):
            count = 1 if node_type(node) == ON_CLICK else 0
            try:
                en = node.createEnumeration()
            except Exception:
                return count
            while en.hasMoreElements():
                count += count_clicks(en.nextElement())
            return count

        def find_main_sequence(node):
            if node_type(node) == MAIN_SEQUENCE:
                return node
            try:
                en = node.createEnumeration()
            except Exception:
                return None
            while en.hasMoreElements():
                found = find_main_sequence(en.nextElement())
                if found is not None:
                    return found
            return None

        try:
            main = find_main_sequence(page.AnimationNode)
            return count_clicks(main) if main is not None else 0
        except Exception:
            return 0

    def start(self, display, with_timings):
        if not with_timings:
            self.set_timings(False)

        pres = self.doc.Presentation
        pres.setPropertyValue("Display", display)
        pres.setPropertyValue("IsAlwaysOnTop", False)
        pres.setPropertyValue("IsMouseVisible", False)
        # endless so a timings-driven show never reaches Impress's "click to exit"
        # screen (which disposes the show); the engine detects the wrap to slide 0
        # and advances to the next schedule item instead
        pres.setPropertyValue("IsEndless", True)
        pres.setPropertyValue("IsShowAll", True)
        if self.show_bounds is not None:
            pres.setPropertyValue("IsFullScreen", False)
            self.run_ui(self.position_show_window)
        _debug_log("[start] calling pres.start on display %s" % display)
        self.run_ui(pres.start)
        _debug_log("[start] pres.start returned")

        deadline = time.time() + 30
        next_focus = 0
        while time.time() < deadline:
            try:
                # Native fullscreen will not complete until the document's native
                # NSWindow becomes key. Repeat that AppKit activation while the show
                # is pending; this is the state change the user's click supplied.
                if (sys.platform == "darwin" and self.show_bounds is None
                        and time.time() >= next_focus):
                    self.run_ui(self.activate_native_start_window)
                    next_focus = time.time() + 0.25
                if pres.isRunning():
                    self.controller = pres.getController()
                    if self.controller is not None:
                        break
            except Exception as e:
                _debug_log("[start] poll error: %r" % e)
            time.sleep(0.2)
        if self.controller is None:
            _debug_log("[start] gave up; isRunning=%r" % pres.isRunning())
            raise RuntimeError("slideshow did not start")

        # Activate the slideshow UI itself. Activating the LibreOffice application
        # only makes its editor window key on macOS, which can leave that editor in
        # front of the running presentation. The controller also owns the effective
        # topmost flag after pres.start has created the slideshow window.
        self.activate_show()

        self.listener = ShowListener(self)
        self.controller.addSlideShowListener(self.listener)
        return {"slide": self.controller.getCurrentSlideIndex()}

    def activate_show(self):
        c = self.require_controller()
        c.AlwaysOnTop = False
        self.run_ui(c.activate)
        c.AlwaysOnTop = False

    def deactivate_show(self):
        c = self.require_controller()
        self.run_ui(c.deactivate)

    def position_show_window(self):
        """Covers the projector screen with the document window (whose client area
        hosts the windowed show). Runs on the main thread."""
        x, y, w, h = self.show_bounds
        win = self.doc.CurrentController.Frame.ContainerWindow
        win.setPosSize(x, y, w, h, 15)  # 15 = PosSize.POSSIZE
        win.toFront()

    def activate_native_start_window(self):
        document_window = self.doc.CurrentController.Frame.ContainerWindow
        document_window.toFront()
        document_window.setFocus()

        # XSystemDependentWindowPeer returns the NSView* on macOS. Running inside
        # soffice means we can make its owning NSWindow key directly on the AppKit
        # main thread, reproducing the window activation performed by a physical
        # click without posting global input or requiring Accessibility permission.
        view = int(document_window.getWindowHandle(uno.ByteSequence(b""), 5))
        if view == 0:
            return

        objc = ctypes.CDLL("/usr/lib/libobjc.A.dylib")
        objc.objc_getClass.argtypes = [ctypes.c_char_p]
        objc.objc_getClass.restype = ctypes.c_void_p
        objc.sel_registerName.argtypes = [ctypes.c_char_p]
        objc.sel_registerName.restype = ctypes.c_void_p
        send_ptr = ctypes.CFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p)(
            ("objc_msgSend", objc))
        send_void = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_void_p)(
            ("objc_msgSend", objc))
        send_void_obj = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_void_p,
                                         ctypes.c_void_p)(("objc_msgSend", objc))
        send_void_bool = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_void_p,
                                          ctypes.c_bool)(("objc_msgSend", objc))
        sel = lambda name: objc.sel_registerName(name.encode("ascii"))

        nswindow = send_ptr(view, sel("window"))
        nsapp = send_ptr(objc.objc_getClass(b"NSApplication"),
                         sel("sharedApplication"))
        if nsapp:
            send_void_bool(nsapp, sel("activateIgnoringOtherApps:"), True)
        if nswindow:
            send_void_obj(nswindow, sel("makeKeyAndOrderFront:"), None)
            send_void(nswindow, sel("orderFrontRegardless"))
            _debug_log("[start] made native document NSWindow key")

    def set_timings(self, enabled):
        pages = self.doc.DrawPages
        for i in range(pages.Count):
            page = pages.getByIndex(i)
            if enabled:
                change, duration = self.page_changes[i]
                page.Change = change
                page.Duration = duration
            else:
                page.Change = 0

    def require_controller(self):
        if self.controller is None:
            raise RuntimeError("slideshow is not running")
        return self.controller

    def export(self, index, path, width, height):
        page = self.doc.DrawPages.getByIndex(index)
        exporter = self.ctx.ServiceManager.createInstanceWithContext(
            "com.sun.star.drawing.GraphicExportFilter", self.ctx)
        exporter.setSourceDocument(page)
        filter_data = uno.Any(
            "[]com.sun.star.beans.PropertyValue",
            (prop("PixelWidth", width), prop("PixelHeight", height)))
        exporter.filter((
            prop("URL", uno.systemPathToFileUrl(path)),
            prop("MediaType", "image/png"),
            uno.createUnoStruct("com.sun.star.beans.PropertyValue", "FilterData", 0, filter_data, 0),
        ))

    def handle(self, req):
        cmd = req["cmd"]
        if cmd == "start":
            return self.start(req.get("display", 0), req.get("withTimings", True))
        if cmd == "next":
            c = self.require_controller()
            c.gotoNextEffect()
            return {"slide": c.getCurrentSlideIndex()}
        if cmd == "prev":
            c = self.require_controller()
            c.gotoPreviousEffect()
            return {"slide": c.getCurrentSlideIndex()}
        if cmd == "goto":
            c = self.require_controller()
            c.gotoSlideIndex(req["index"])
            return {"slide": c.getCurrentSlideIndex()}
        if cmd == "activate":
            self.activate_show()
            return {"slide": self.controller.getCurrentSlideIndex()}
        if cmd == "deactivate":
            self.deactivate_show()
            return {}
        if cmd == "slide":
            return {"slide": self.require_controller().getCurrentSlideIndex()}
        if cmd == "setTimings":
            self.set_timings(req["enabled"])
            return {}
        if cmd == "export":
            self.export(req["index"], req["path"], req["width"], req["height"])
            return {"path": req["path"]}
        raise RuntimeError("unknown command " + cmd)

    def serve(self, lines):
        """Answers requests until quit or the request stream ends (engine gone)."""
        for line in lines:
            line = line.strip()
            if not line:
                continue
            req = json.loads(line)
            if req.get("cmd") == "quit":
                # answer before shutdown: terminating soffice tears the pipe down
                self.emit({"id": req.get("id"), "ok": True})
                self.shutdown()
                return
            try:
                result = self.handle(req)
                result["id"] = req.get("id")
                result["ok"] = True
                self.emit(result)
            except Exception as e:
                self.emit({"id": req.get("id"), "ok": False, "error": str(e)})
        self.shutdown()

    def shutdown(self):
        try:
            if self.controller is not None:
                self.controller = None
                self.run_ui(self.doc.Presentation.end)
        except Exception:
            pass
        try:
            self.run_ui(lambda: self.doc.close(False))
        except Exception:
            pass
        try:
            self.run_ui(self.desktop.terminate)
        except Exception:
            pass
        if self.proc is not None:
            try:
                self.proc.wait(timeout=10)
            except Exception:
                self.proc.kill()


# ---- out-of-process mode (Windows): spawn soffice, connect over a URP pipe --------

def connect_pipe(host, soffice, profile_dir, pipe_name):
    """Spawns soffice with a dedicated profile and resolves its component context."""
    os.makedirs(profile_dir, exist_ok=True)
    profile_url = uno.systemPathToFileUrl(profile_dir)
    host.proc = subprocess.Popen(
        [
            soffice,
            "--accept=pipe,name=%s;urp;" % pipe_name,
            "--norestore",
            "--nologo",
            "--nodefault",
            "--nolockcheck",
            "-env:UserInstallation=" + profile_url,
        ],
        stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    local = uno.getComponentContext()
    resolver = local.ServiceManager.createInstanceWithContext(
        "com.sun.star.bridge.UnoUrlResolver", local)
    deadline = time.time() + 90  # first run initialises the profile, which is slow
    while True:
        try:
            return resolver.resolve(
                "uno:pipe,name=%s;urp;StarOffice.ComponentContext" % pipe_name)
        except Exception:
            if host.proc.poll() is not None:
                raise RuntimeError("soffice exited with code %s" % host.proc.returncode)
            if time.time() > deadline:
                raise RuntimeError("timed out connecting to soffice")
            time.sleep(0.25)


def main():
    # the C# engine speaks UTF-8; never let the console codepage mangle file paths
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    soffice, profile_dir, pipe_name, document = sys.argv[1:5]

    stdout_lock = threading.Lock()

    def write_line(text):
        with stdout_lock:
            sys.stdout.write(text + "\n")
            sys.stdout.flush()

    host = Host(document, write_line)
    try:
        ctx = connect_pipe(host, soffice, profile_dir, pipe_name)
        host.attach(ctx, host.proc.pid)
    except Exception as e:
        host.emit({"event": "error", "message": str(e), "detail": traceback.format_exc()})
        host.shutdown()
        sys.exit(1)

    host.serve(sys.stdin)


# ---- in-process mode (macOS): script provider entry point --------------------------

def _run_inproc():
    import socket

    try:
        port = int(os.environ["AVP_HOST_PORT"])
    except Exception:
        return  # not launched by the engine (manual soffice start on this profile)
    document = os.environ.get("AVP_DOC", "")

    _debug_log("[%s pid=%d tid=%d] connecting to 127.0.0.1:%d"
               % (time.strftime("%H:%M:%S"), os.getpid(), threading.get_ident(), port))

    sock = socket.create_connection(("127.0.0.1", port))
    reader = sock.makefile("r", encoding="utf-8", newline="\n")

    def write_line(text):
        sock.sendall((text + "\n").encode("utf-8"))

    # other local processes probe fresh loopback ports and would win a bare accept
    # race; the engine only adopts a connection that says hello
    write_line('{"event": "hello"}')

    try:
        ctx = XSCRIPTCONTEXT.getComponentContext()  # noqa: F821
    except NameError:
        ctx = uno.getComponentContext()

    # OnStartApp fires while soffice is still initialising; loading a document that
    # early can hang. A no-op main-thread round trip means the main loop is up.
    _debug_log("[inproc] waiting for main loop")
    run_on_main(ctx, lambda: None, timeout=120)
    _debug_log("[inproc] main loop up")

    bounds = None
    if os.environ.get("AVP_SHOW_BOUNDS"):
        bounds = tuple(int(v) for v in os.environ["AVP_SHOW_BOUNDS"].split(","))

    host = Host(document, write_line, run_ui=lambda fn: run_on_main(ctx, fn),
                show_bounds=bounds)
    try:
        host.attach_existing(ctx, os.getpid())
    except Exception as e:
        host.emit({"event": "error", "message": str(e), "detail": traceback.format_exc()})
        host.shutdown()
        return

    try:
        host.serve(reader)
    except Exception:
        _debug_log(traceback.format_exc())
        host.shutdown()  # engine gone (socket reset): don't leave soffice behind


def _debug_log(text):
    """Diagnostics for the in-process mode, where stderr belongs to soffice and is
    normally invisible; enabled by pointing AVP_LOG at a file."""
    path = os.environ.get("AVP_LOG")
    if not path:
        return
    try:
        with open(path, "a") as f:
            f.write(text + "\n")
    except Exception:
        pass


def start_host(*args):
    """OnLoad handler (bound in the generated profile): must return quickly, so the
    protocol loop runs on its own thread inside soffice. The event can fire more
    than once and the script provider reloads this module per invocation, so the
    once-guard lives in the process environment rather than a module global."""
    if os.environ.get("AVP_HOST_STARTED"):
        return
    os.environ["AVP_HOST_STARTED"] = "1"

    def run():
        try:
            _run_inproc()
        except Exception:
            _debug_log(traceback.format_exc())
    threading.Thread(target=run, name="avp-host", daemon=True).start()


g_exportedScripts = (start_host,)


if __name__ == "__main__":
    main()
