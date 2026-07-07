using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Presenter.Core.Abstractions;

namespace Presenter.Engine.Uno;

/// <summary>Per-slide metadata gathered by the helper when the document loads.</summary>
public record HostSlideInfo(string Text, string Notes, int ClickEffects, bool AdvanceOnTime);

/// <summary>
/// Owns one soffice instance with one loaded presentation, remote-controlled by
/// slideshow_host.py. Requests go out as JSON lines and block until the matching
/// response arrives; unsolicited events (slide changes driven by timings or clicks
/// in the show window, the show being closed) surface as .NET events on a background
/// thread.
///
/// Two transports, same protocol: on Windows the script runs out of process on
/// LibreOffice's bundled python.exe (which spawns soffice itself) and speaks over
/// stdin/stdout; on macOS the bundled Python may only be launched by soffice (parent
/// launch constraints, a TCC CVE fix in LibreOffice 25.2.4), so soffice is spawned
/// directly with a generated profile whose OnStartApp event runs the script in
/// process, and it connects back over a loopback TCP socket.
/// </summary>
internal sealed class SlideshowHost : IDisposable
{
    private readonly Process _proc;
    private readonly TextReader _reader;
    private readonly TextWriter _writer;
    private readonly IDisposable? _connection; // the TCP client on macOS
    private readonly IShowWindowController _windows;
    private readonly object _writeLock = new();
    private readonly Dictionary<int, TaskCompletionSource<JsonDocument>> _pending = new();
    private readonly TaskCompletionSource<JsonDocument> _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextId;
    private int _showWindowToken;

    public string Filename { get; }
    public long EditWindowHandle { get; private set; }
    public int SofficePid { get; private set; }
    public IReadOnlyList<HostSlideInfo> SlideInfo { get; private set; } = [];

    /// <summary>Raised with the 0-based Impress slide index; background thread.</summary>
    public event Action<SlideshowHost, int>? SlideChanged;

    /// <summary>Raised when the show window went away (ESC or crash); background thread.</summary>
    public event Action<SlideshowHost>? ShowEnded;

    /// <summary>Engine bookkeeping: last Impress slide index the engine believes is showing.</summary>
    public int LastKnownIndex { get; set; }

    private SlideshowHost(Process proc, string filename, TextReader reader, TextWriter writer,
        IDisposable? connection, IShowWindowController windows)
    {
        _proc = proc;
        Filename = filename;
        _reader = reader;
        _writer = writer;
        _connection = connection;
        _windows = windows;
    }

    public static SlideshowHost Launch(string soffice, string filename, string profileDir,
        TimeSpan timeout, IShowWindowController windows, ScreenBounds? showBounds = null)
    {
        SlideshowHost host = OperatingSystem.IsMacOS()
            ? LaunchInProcess(soffice, filename, profileDir, timeout, windows, showBounds)
            : LaunchPython(soffice, filename, profileDir, windows);

        new Thread(host.ReadLoop) { IsBackground = true, Name = "uno-host-read" }.Start();

        // wait for the loaded event (first run initialises the LibreOffice profile, which is slow);
        // a faulted task (helper died) must dispose too, so probe completion without throwing
        bool gotLoadedEvent;
        try
        {
            gotLoadedEvent = host._loaded.Task.Wait(timeout);
        }
        catch (AggregateException)
        {
            gotLoadedEvent = false;
        }
        if (!gotLoadedEvent)
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
        host.SofficePid = root.TryGetProperty("pid", out var p) ? p.GetInt32() : 0;
        host.SlideInfo = root.GetProperty("slides").EnumerateArray()
            .Select(s => new HostSlideInfo(
                s.GetProperty("text").GetString() ?? "",
                s.GetProperty("notes").GetString() ?? "",
                s.GetProperty("clickEffects").GetInt32(),
                s.GetProperty("advanceOnTime").GetBoolean()))
            .ToArray();
        return host;
    }

    /// <summary>Windows: the script runs on LibreOffice's python.exe and spawns soffice itself.</summary>
    private static SlideshowHost LaunchPython(string soffice, string filename, string profileDir,
        IShowWindowController windows)
    {
        string python = SofficeLocator.FindPython(soffice)
            ?? throw new InvalidOperationException("LibreOffice python.exe not found next to soffice.exe");

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

        var proc = Process.Start(psi)!;
        return new SlideshowHost(proc, filename, proc.StandardOutput, proc.StandardInput, null, windows);
    }

    /// <summary>macOS: soffice is spawned directly and runs the script in process
    /// (OnStartApp binding in the generated profile); it connects back over TCP.</summary>
    private static SlideshowHost LaunchInProcess(string soffice, string filename, string profileDir,
        TimeSpan timeout, IShowWindowController windows, ScreenBounds? showBounds)
    {
        PrepareProfile(soffice, profileDir);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // soffice opens the document itself (read-only via --view): loading from the
        // script's background thread hangs on a freshly initialised profile. The
        // OnLoad binding then starts the in-process host, which picks the open
        // document up from the desktop.
        var psi = new ProcessStartInfo(soffice) { UseShellExecute = false };
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add("--nologo");
        psi.ArgumentList.Add("--nolockcheck");
        psi.ArgumentList.Add("--view");
        psi.ArgumentList.Add("-env:UserInstallation=" + new Uri(profileDir).AbsoluteUri);
        psi.ArgumentList.Add(filename);
        psi.Environment["AVP_HOST_PORT"] = port.ToString();
        psi.Environment["AVP_DOC"] = filename;
        if (showBounds is ScreenBounds b)
            psi.Environment["AVP_SHOW_BOUNDS"] = $"{b.Left},{b.Top},{b.Width},{b.Height}";

        Process? proc = null;
        try
        {
            proc = Process.Start(psi)!;

            // other local processes probe freshly opened loopback ports (connect and
            // drop), so a bare accept is not enough: only adopt the connection whose
            // first line is the script's hello, and keep listening until it arrives
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (proc.HasExited)
                    throw new InvalidOperationException("soffice exited with code " + proc.ExitCode);

                var accept = listener.AcceptTcpClientAsync();
                while (!accept.IsCompleted && DateTime.UtcNow < deadline)
                {
                    if (proc.HasExited)
                        throw new InvalidOperationException("soffice exited with code " + proc.ExitCode);
                    accept.Wait(TimeSpan.FromMilliseconds(250));
                }
                if (!accept.IsCompletedSuccessfully)
                    break;

                TcpClient client = accept.Result;
                var stream = client.GetStream();
                var reader = new StreamReader(stream, new UTF8Encoding(false));
                try
                {
                    stream.ReadTimeout = 5000;
                    string? hello = reader.ReadLine();
                    if (hello != null && hello.Contains("\"hello\"", StringComparison.Ordinal))
                    {
                        stream.ReadTimeout = Timeout.Infinite;
                        return new SlideshowHost(proc, filename, reader,
                            new StreamWriter(stream, new UTF8Encoding(false)), client, windows);
                    }
                }
                catch (IOException) { }
                client.Dispose(); // a prober, or garbage: wait for the real host
            }

            throw new InvalidOperationException("LibreOffice did not start the slideshow host for "
                + Path.GetFileName(filename));
        }
        catch (Exception)
        {
            try { proc?.Kill(true); } catch (Exception) { }
            proc?.Dispose();
            throw;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Populates the per-instance profile so soffice runs slideshow_host.py in process
    /// at startup: the script under user/Scripts/python plus an OnStartApp application
    /// event binding. The binding file is only created once — LibreOffice rewrites it
    /// with its own settings on exit (preserving our item), and overwriting it would
    /// discard those; the script is refreshed every launch to follow app updates.
    /// </summary>
    private static void PrepareProfile(string soffice, string profileDir)
    {
        string scriptDir = Path.Combine(profileDir, "user", "Scripts", "python");
        Directory.CreateDirectory(scriptDir);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "slideshow_host.py"),
            Path.Combine(scriptDir, "slideshow_host.py"), overwrite: true);

        string xcu = Path.Combine(profileDir, "user", "registrymodifications.xcu");
        if (File.Exists(xcu))
            return;

        File.WriteAllText(xcu, """
            <?xml version="1.0" encoding="UTF-8"?>
            <oor:items xmlns:oor="http://openoffice.org/2001/registry" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
             <item oor:path="/org.openoffice.Office.Events/ApplicationEvents/Bindings"><node oor:name="OnLoad" oor:op="replace"><prop oor:name="BindingURL" oor:op="fuse"><value>vnd.sun.star.script:slideshow_host.py$start_host?language=Python&amp;location=user</value></prop></node></item>
            </oor:items>
            """);

        // boot once headless so the real launch skips first-run profile initialisation,
        // which restarts soffice mid-flight and would kill the in-process host thread
        // (the OnStartApp binding fires here too, but the script exits immediately
        // because the AVP_* environment is absent)
        var psi = new ProcessStartInfo(soffice) { UseShellExecute = false };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--invisible");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add("--nolockcheck");
        psi.ArgumentList.Add("--terminate_after_init");
        psi.ArgumentList.Add("-env:UserInstallation=" + new Uri(profileDir).AbsoluteUri);
        psi.Environment.Remove("AVP_HOST_PORT");
        using var warmup = Process.Start(psi)!;
        if (!warmup.WaitForExit(60000))
        {
            try { warmup.Kill(true); } catch (Exception) { }
        }
    }

    private void ReadLoop()
    {
        try
        {
            string? line;
            while ((line = _reader.ReadLine()) != null)
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

        // the transport closed: the helper died — fail anything still waiting
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
            _writer.WriteLine(JsonSerializer.Serialize(payload));
            _writer.Flush();
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
    /// The OS token of the running slideshow (see <see cref="IShowWindowController"/>).
    /// Cached once found; null before the show started.
    /// </summary>
    public int? GetShowWindowHandle()
    {
        if (_showWindowToken != 0)
            return _showWindowToken;

        int? token = _windows.FindShowWindow(EditWindowHandle, SofficePid);
        if (token is int t)
            _showWindowToken = t;
        return token;
    }

    /// <summary>Forgets the cached show window (after the show ended or was restarted).</summary>
    public void InvalidateShowWindow() => _showWindowToken = 0;

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
        _connection?.Dispose();
        _proc.Dispose();
    }
}
