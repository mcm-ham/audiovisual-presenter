"""Slideshow host for the UNO presentation engine.

Runs on LibreOffice's bundled python.exe (which ships the ``uno`` bridge module, so a
plain LibreOffice install is the only dependency). One host process owns one soffice
instance and one loaded presentation; the C# engine spawns a host per PowerPoint
schedule item and they communicate over stdin/stdout with one JSON object per line.

Requests carry an ``id`` and are answered with a matching ``{"id": n, ...}`` line;
unsolicited events (slide changes from timings/clicks in the show window, the show
being closed) are emitted without an id:

  -> {"id":1,"cmd":"start","display":2,"withTimings":true}
  <- {"id":1,"ok":true,"slide":0}
  <- {"event":"slideChanged","index":3}
  <- {"event":"showEnded"}

Usage: python.exe slideshow_host.py <soffice.exe> <profile-dir> <pipe-name> <document>
"""
import json
import os
import subprocess
import sys
import threading
import time
import traceback

import uno
import unohelper
from com.sun.star.beans import PropertyValue
from com.sun.star.presentation import XSlideShowListener

ON_CLICK = 1        # com.sun.star.presentation.EffectNodeType
MAIN_SEQUENCE = 4

_stdout_lock = threading.Lock()


def emit(obj):
    with _stdout_lock:
        sys.stdout.write(json.dumps(obj) + "\n")
        sys.stdout.flush()


def prop(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


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
        emit({"event": "slideChanged", "index": self._index()})

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
        emit({"event": "showEnded"})


class Host:
    def __init__(self, soffice, profile_dir, pipe_name, document):
        self.soffice = soffice
        self.profile_dir = profile_dir
        self.pipe_name = pipe_name
        self.document = document
        self.proc = None
        self.ctx = None
        self.desktop = None
        self.doc = None
        self.controller = None
        self.listener = None
        self.page_changes = []  # original (Change, Duration) per page for setTimings

    def launch(self):
        os.makedirs(self.profile_dir, exist_ok=True)
        profile_url = uno.systemPathToFileUrl(self.profile_dir)
        self.proc = subprocess.Popen(
            [
                self.soffice,
                "--accept=pipe,name=%s;urp;" % self.pipe_name,
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
                self.ctx = resolver.resolve(
                    "uno:pipe,name=%s;urp;StarOffice.ComponentContext" % self.pipe_name)
                break
            except Exception:
                if self.proc.poll() is not None:
                    raise RuntimeError("soffice exited with code %s" % self.proc.returncode)
                if time.time() > deadline:
                    raise RuntimeError("timed out connecting to soffice")
                time.sleep(0.25)

        self.desktop = self.ctx.ServiceManager.createInstanceWithContext(
            "com.sun.star.frame.Desktop", self.ctx)

        url = uno.systemPathToFileUrl(self.document)
        # read-only: never lock or modify the user's file (the same file may be loaded
        # twice in one schedule); minimized: the edit window must stay out of the way
        self.doc = self.desktop.loadComponentFromURL(
            url, "_blank", 0, (prop("ReadOnly", True), prop("MinimizeWindow", True)))
        if self.doc is None:
            raise RuntimeError("could not load " + self.document)

        # the document window belongs to soffice.bin (not the soffice.exe we spawned);
        # its handle lets the C# engine locate the right process's slideshow window
        hwnd = 0
        try:
            win = self.doc.CurrentController.Frame.ContainerWindow
            hwnd = int(win.getWindowHandle(uno.ByteSequence(b""), 1))  # 1 = SYSTEM_WIN32
        except Exception:
            pass

        emit({"event": "loaded", "pid": self.proc.pid, "hwnd": hwnd, "slides": self.slide_info()})

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
        pres.start()

        deadline = time.time() + 30
        while time.time() < deadline:
            try:
                if pres.isRunning():
                    self.controller = pres.getController()
                    if self.controller is not None:
                        break
            except Exception:
                pass
            time.sleep(0.2)
        if self.controller is None:
            raise RuntimeError("slideshow did not start")

        self.listener = ShowListener(self)
        self.controller.addSlideShowListener(self.listener)
        return {"slide": self.controller.getCurrentSlideIndex()}

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
        if cmd == "slide":
            return {"slide": self.require_controller().getCurrentSlideIndex()}
        if cmd == "setTimings":
            self.set_timings(req["enabled"])
            return {}
        if cmd == "export":
            self.export(req["index"], req["path"], req["width"], req["height"])
            return {"path": req["path"]}
        if cmd == "end":
            if self.controller is not None:
                self.controller = None
                self.doc.Presentation.end()
            return {}
        if cmd == "quit":
            self.shutdown()
            return {}
        raise RuntimeError("unknown command " + cmd)

    def shutdown(self):
        try:
            if self.controller is not None:
                self.controller = None
                self.doc.Presentation.end()
        except Exception:
            pass
        try:
            self.doc.close(False)
        except Exception:
            pass
        try:
            self.desktop.terminate()
        except Exception:
            pass
        if self.proc is not None:
            try:
                self.proc.wait(timeout=10)
            except Exception:
                self.proc.kill()


def main():
    # the C# engine speaks UTF-8; never let the console codepage mangle file paths
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    soffice, profile_dir, pipe_name, document = sys.argv[1:5]
    host = Host(soffice, profile_dir, pipe_name, document)
    try:
        host.launch()
    except Exception as e:
        emit({"event": "error", "message": str(e), "detail": traceback.format_exc()})
        host.shutdown()
        sys.exit(1)

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        req = json.loads(line)
        try:
            result = host.handle(req)
            result["id"] = req.get("id")
            result["ok"] = True
            emit(result)
        except Exception as e:
            emit({"id": req.get("id"), "ok": False, "error": str(e)})
        if req.get("cmd") == "quit":
            return

    host.shutdown()  # stdin closed: the parent process is gone


if __name__ == "__main__":
    main()
