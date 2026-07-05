using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Presenter.Engine.Uno.Interop;

namespace Presenter.Engine.Uno;

/// <summary>Per-slide metadata gathered by the helper when the document loads.</summary>
public record HostSlideInfo(string Text, string Notes, int ClickEffects, bool AdvanceOnTime);

/// <summary>
/// Owns one slideshow_host.py process (which in turn owns one soffice instance with
/// one loaded presentation). Requests go out as JSON lines over stdin and block until
/// the matching response arrives; unsolicited events (slide changes driven by timings
/// or clicks in the show window, the show being closed) surface as .NET events on a
/// background thread.
/// </summary>
internal sealed class SlideshowHost : IDisposable
{
    private readonly Process _proc;
    private readonly object _writeLock = new();
    private readonly Dictionary<int, TaskCompletionSource<JsonDocument>> _pending = new();
    private readonly TaskCompletionSource<JsonDocument> _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextId;
    private int _showWindowHandle;

    public string Filename { get; }
    public long EditWindowHandle { get; private set; }
    public IReadOnlyList<HostSlideInfo> SlideInfo { get; private set; } = [];

    /// <summary>Raised with the 0-based Impress slide index; background thread.</summary>
    public event Action<SlideshowHost, int>? SlideChanged;

    /// <summary>Raised when the show window went away (ESC or crash); background thread.</summary>
    public event Action<SlideshowHost>? ShowEnded;

    /// <summary>Engine bookkeeping: last Impress slide index the engine believes is showing.</summary>
    public int LastKnownIndex { get; set; }

    private SlideshowHost(Process proc, string filename)
    {
        _proc = proc;
        Filename = filename;
    }

    public static SlideshowHost Launch(string soffice, string filename, string profileDir, TimeSpan timeout)
    {
        string python = Path.Combine(Path.GetDirectoryName(soffice)!, "python.exe");
        if (!File.Exists(python))
            throw new InvalidOperationException("LibreOffice python.exe not found next to soffice.exe");

        string script = Path.Combine(AppContext.BaseDirectory, "slideshow_host.py");
        var psi = new ProcessStartInfo(python)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
        };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add(soffice);
        psi.ArgumentList.Add(profileDir);
        psi.ArgumentList.Add("avp-uno-" + Guid.NewGuid().ToString("N"));
        psi.ArgumentList.Add(filename);

        var host = new SlideshowHost(Process.Start(psi)!, filename);
        new Thread(host.ReadLoop) { IsBackground = true, Name = "uno-host-read" }.Start();

        // wait for the loaded event (first run initialises the LibreOffice profile, which is slow)
        if (!host._loaded.Task.Wait(timeout))
        {
            host.Dispose();
            throw new InvalidOperationException("LibreOffice did not load " + Path.GetFileName(filename));
        }
        using JsonDocument loaded = host._loaded.Task.Result;
        var root = loaded.RootElement;
        if (root.TryGetProperty("event", out var ev) && ev.GetString() == "error")
        {
            host.Dispose();
            throw new InvalidOperationException("LibreOffice could not load " + Path.GetFileName(filename)
                + ": " + root.GetProperty("message").GetString());
        }

        host.EditWindowHandle = root.TryGetProperty("hwnd", out var h) ? h.GetInt64() : 0;
        host.SlideInfo = root.GetProperty("slides").EnumerateArray()
            .Select(s => new HostSlideInfo(
                s.GetProperty("text").GetString() ?? "",
                s.GetProperty("notes").GetString() ?? "",
                s.GetProperty("clickEffects").GetInt32(),
                s.GetProperty("advanceOnTime").GetBoolean()))
            .ToArray();
        return host;
    }

    private void ReadLoop()
    {
        try
        {
            string? line;
            while ((line = _proc.StandardOutput.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;
                var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                {
                    TaskCompletionSource<JsonDocument>? tcs;
                    lock (_pending)
                        _pending.Remove(id.GetInt32(), out tcs);
                    tcs?.TrySetResult(doc);
                    continue;
                }

                switch (root.TryGetProperty("event", out var ev) ? ev.GetString() : null)
                {
                    case "loaded":
                    case "error":
                        _loaded.TrySetResult(doc);
                        break;
                    case "slideChanged":
                        SlideChanged?.Invoke(this, root.GetProperty("index").GetInt32());
                        doc.Dispose();
                        break;
                    case "showEnded":
                        ShowEnded?.Invoke(this);
                        doc.Dispose();
                        break;
                    default:
                        doc.Dispose();
                        break;
                }
            }
        }
        catch (Exception) { }

        // stdout closed: the helper died — fail anything still waiting
        _loaded.TrySetException(new InvalidOperationException("LibreOffice helper exited unexpectedly"));
        List<TaskCompletionSource<JsonDocument>> pending;
        lock (_pending)
        {
            pending = _pending.Values.ToList();
            _pending.Clear();
        }
        foreach (var tcs in pending)
            tcs.TrySetException(new InvalidOperationException("LibreOffice helper exited unexpectedly"));
    }

    /// <summary>Sends a command and blocks for its response (throws on error/timeout).</summary>
    public JsonDocument Request(TimeSpan timeout, string cmd, params (string Key, object Value)[] args)
    {
        int id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending)
            _pending[id] = tcs;

        var payload = new Dictionary<string, object?> { ["id"] = id, ["cmd"] = cmd };
        foreach (var (key, value) in args)
            payload[key] = value;

        lock (_writeLock)
        {
            _proc.StandardInput.WriteLine(JsonSerializer.Serialize(payload));
            _proc.StandardInput.Flush();
        }

        if (!tcs.Task.Wait(timeout))
        {
            lock (_pending)
                _pending.Remove(id);
            throw new TimeoutException($"LibreOffice did not answer '{cmd}' for {Path.GetFileName(Filename)}");
        }

        JsonDocument doc = tcs.Task.Result;
        if (!doc.RootElement.GetProperty("ok").GetBoolean())
        {
            string error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
            doc.Dispose();
            throw new InvalidOperationException($"LibreOffice '{cmd}' failed: {error}");
        }
        return doc;
    }

    /// <summary>
    /// The Impress slideshow window: a visible top-level window of the soffice.bin
    /// process (identified via the document window handle) other than the document
    /// window itself. Cached once found; null before the show started.
    /// </summary>
    public int? GetShowWindowHandle()
    {
        if (_showWindowHandle != 0)
            return _showWindowHandle;
        if (EditWindowHandle == 0)
            return null;

        User32.GetWindowThreadProcessId(new IntPtr(EditWindowHandle), out uint pid);
        if (pid == 0)
            return null;

        int best = 0;
        long bestArea = -1;
        User32.EnumWindows((hwnd, _) =>
        {
            if (hwnd == new IntPtr(EditWindowHandle) || !User32.IsWindowVisible(hwnd))
                return true;
            User32.GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid != pid || !User32.GetWindowRect(hwnd, out var r))
                return true;
            long area = (long)r.Width * r.Height;
            if (area > bestArea) // the fullscreen show window is the biggest one
            {
                bestArea = area;
                best = (int)hwnd;
            }
            return true;
        }, IntPtr.Zero);

        if (best != 0)
            _showWindowHandle = best;
        return best == 0 ? null : best;
    }

    /// <summary>Forgets the cached show window (after the show ended or was restarted).</summary>
    public void InvalidateShowWindow() => _showWindowHandle = 0;

    public void Dispose()
    {
        try
        {
            if (!_proc.HasExited)
            {
                try { Request(TimeSpan.FromSeconds(5), "quit").Dispose(); }
                catch (Exception) { }
                if (!_proc.WaitForExit(10000))
                    _proc.Kill(true); // takes the soffice child down too
            }
        }
        catch (Exception) { }
        _proc.Dispose();
    }
}
