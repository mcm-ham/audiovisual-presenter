using Presenter.Core.Abstractions;
using Presenter.Core.Models;
using Presenter.Core.Settings;
using Presenter.Engine.Uno;

// Console smoke harness for the UNO engine: drives a real LibreOffice instance
// without the WPF app, so engine changes can be verified on any OS.
//
//   dotnet run --project Presenter.Engine.Uno.Smoke -- [--display N] file.pptx [more files...]
//
// Then commands on stdin (scriptable by piping): info, next, prev, goto <slideIndex>,
// export <slideIndex>, sleep <seconds>, quit.

int display = 1;
var files = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--display" && i + 1 < args.Length)
        display = int.Parse(args[++i]);
    else
        files.Add(Path.GetFullPath(args[i]));
}

if (files.Count == 0)
{
    Console.Error.WriteLine("usage: smoke [--display N] <file.pptx> [more files...]");
    return 1;
}

var settings = new FixedSettings();
var engine = new UnoPresentationEngine(settings, new FixedScreens(display),
    new EngineLabels("Video", "Audio", "Image", "Slide "));

if (!engine.IsAvailable)
{
    Console.Error.WriteLine("FAIL: engine reports LibreOffice unavailable (SofficeLocator found: "
        + (SofficeLocator.Find() ?? "nothing") + ")");
    return 1;
}
Console.WriteLine("soffice: " + SofficeLocator.Find());

int pos = 0; // 1-based index into engine.Slides of the slide we believe is showing
engine.SlideAdded += (_, e) =>
{
    if (e.NewSlide is Slide s)
        Console.WriteLine($"  slide {s.SlideIndex}: [{s.Type}] anims={s.AnimationCount} timed={s.AdvanceOnTime} \"{Truncate(s.Text)}\"");
};
engine.SlideIndexChanged += (_, e) =>
{
    pos = e.NewIndex;
    Console.WriteLine($"# position {e.OldIndex} -> {e.NewIndex}");
};
engine.SlideShowEnd += (_, _) => Console.WriteLine("# show ended");

var schedule = new Schedule { ID = Guid.NewGuid(), Name = "smoke", Date = DateTime.Today };
foreach (string f in files)
    schedule.AddItem(f, settings.Current.SupportedFileTypes);

Console.WriteLine($"loading {files.Count} file(s) on display {display}...");
var sw = System.Diagnostics.Stopwatch.StartNew();
engine.Start(schedule);
Console.WriteLine($"started: {engine.Slides.Count} slides in {sw.Elapsed.TotalSeconds:0.0}s");
pos = 1;

string? line;
while ((line = Console.ReadLine()) != null)
{
    string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
        continue;
    try
    {
        switch (parts[0])
        {
            case "info":
                foreach (var s in engine.Slides)
                    Console.WriteLine($"  slide {s.SlideIndex}: [{s.Type}] anims={s.CurrentAnimationCount}/{s.AnimationCount} \"{Truncate(s.Text)}\"");
                Console.WriteLine($"  position: {pos}");
                break;
            case "next" or "n":
                engine.Next(engine.Slides[pos - 1]);
                break;
            case "prev" or "p":
                engine.Previous(engine.Slides[pos - 1]);
                break;
            case "goto" or "g":
                pos = int.Parse(parts[1]);
                engine.GoTo(engine.Slides[pos - 1]);
                break;
            case "export" or "e":
                string? path = engine.ExportSlideImage(engine.Slides[int.Parse(parts[1]) - 1], int.Parse(parts[1]), "smoke", 640, 480);
                Console.WriteLine(path != null && File.Exists(path)
                    ? $"exported: {path} ({new FileInfo(path).Length} bytes)"
                    : "FAIL: export returned " + (path ?? "null"));
                break;
            case "sleep":
                Thread.Sleep(TimeSpan.FromSeconds(double.Parse(parts[1])));
                break;
            case "quit" or "q":
                goto done;
            default:
                Console.WriteLine("commands: info | next | prev | goto <n> | export <n> | sleep <s> | quit");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("ERROR: " + ex.Message);
    }
}
done:
Console.WriteLine("stopping...");
engine.Stop();
Console.WriteLine("stopped cleanly");
return 0;

static string Truncate(string? s) => s is null ? "" : s.Length > 60 ? s[..60] + "…" : s;

class FixedSettings : ISettingsStore
{
    public PresenterSettings Current { get; } = new();
    public void Save() { }
}

class FixedScreens(int display) : IScreenInfoProvider
{
    public ScreenBounds ProjectorBounds => new(0, 0, 1440, 900);
    public ScreenBounds PrimaryWorkingArea => new(0, 0, 1440, 900);
    public int ProjectorScreenNumber => display;
}
