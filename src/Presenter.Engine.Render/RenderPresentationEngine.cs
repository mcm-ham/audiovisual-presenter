using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Presenter.Core.Abstractions;
using Presenter.Core.Models;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Presenter.Engine.Render;

/// <summary>
/// No-Office fallback engine: converts PowerPoint files to PDF with LibreOffice
/// (soffice --headless) and rasterizes each page with the Windows PDF renderer.
/// Rendered slides are exposed as <see cref="SlideType.Image"/> slides, so the
/// existing FullscreenWindow media path displays them — no live animations or
/// transitions, and template (.pot) items are skipped.
/// </summary>
public class RenderPresentationEngine : IPresentationEngine
{
    private readonly ISettingsStore _settings;
    private readonly IScreenInfoProvider _screens;
    private readonly EngineLabels _labels;

    private readonly List<Slide> _slides = new();

    public RenderPresentationEngine(ISettingsStore settings, IScreenInfoProvider screens, EngineLabels labels)
    {
        _settings = settings;
        _screens = screens;
        _labels = labels;
    }

    public bool IsAvailable => SofficeLocator.Find() != null;

    public bool IsRunning { get; private set; }

    public IReadOnlyList<Slide> Slides => _slides;

#pragma warning disable CS0067 //the render engine has no external window that can end the show
    public event EventHandler? SlideShowEnd;
#pragma warning restore CS0067
    public event EventHandler<SlideAddedEventArgs>? SlideAdded;
    public event EventHandler<SlideIndexChangedEventArgs>? SlideIndexChanged;

    public void Start(Schedule schedule)
    {
        try
        {
            Item[] items = schedule.Items.OrderBy(i => i.Ordinal).ToArray();
            IsRunning = true;

            _slides.Clear();
            SlideAdded?.Invoke(this, new SlideAddedEventArgs(null, -1));

            AddSlide(SlideType.Blank, "", "", "Blank", 0, new Item(), 1);

            foreach (Item item in items)
                AddSlides(item);

            AddSlide(SlideType.Blank, "", "", "Blank", 0, new Item(), 1);
        }
        catch (Exception)
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        IsRunning = false;
    }

    public void Quit() { }

    public void AddSlides(Item scheduleItem)
    {
        if (!scheduleItem.IsFound || !IsRunning)
            return;

        var S = _settings.Current;
        int itemCount = scheduleItem.Schedule?.Items.Count ?? 1;
        double progressEnd = (scheduleItem.Ordinal + 1) / (double)itemCount;
        string filename = Path.GetFullPath(scheduleItem.Filename).ToLower();
        string filetype = Path.GetExtension(filename).TrimStart('.').ToLower();

        if (S.VideoFormats.Contains(filetype))
        {
            AddSlide(SlideType.Video, filename, scheduleItem.Name, _labels.VideoLabel, progressEnd, scheduleItem, 1);
        }
        else if (S.AudioFormats.Contains(filetype))
        {
            AddSlide(SlideType.Audio, filename, scheduleItem.Name, _labels.AudioLabel, progressEnd, scheduleItem, 1);
        }
        else if (S.ImageFormats.Contains(filetype))
        {
            var b = _screens.ProjectorBounds;
            var s = new Slide(SlideType.Image, filename)
            {
                Text = scheduleItem.Name,
                Comment = _labels.ImageLabel,
                Image = RetrieveImage(filename, b.Width, b.Height),
                Preview = RetrieveImage(filename, 333, 250),
            };
            AddSlide(s, progressEnd, scheduleItem, 1);
        }
        else if (S.PowerPointTemplates.Contains(filetype))
        {
            //applying .pot template designs requires PowerPoint; the render engine skips them
            SlideAdded?.Invoke(this, new SlideAddedEventArgs(null, progressEnd));
        }
        else if (S.PowerPointFormats.Contains(filetype))
        {
            var pages = RenderSlides(filename);
            for (int i = 0; i < pages.Count; i++)
            {
                var s = new Slide(SlideType.Image, filename)
                {
                    Text = _labels.SlideLabel + (i + 1),
                    Comment = "",
                    Image = pages[i].Full,
                    Preview = pages[i].Preview,
                };
                AddSlide(s, progressEnd, scheduleItem, i + 1);
            }
        }

        if (S.InsertBlankAfterPres && S.PowerPointFormats.Contains(filetype) || S.InsertBlankAfterVideo && S.VideoFormats.Concat(S.AudioFormats).Contains(filetype))
        {
            AddSlide(SlideType.Blank, "", "", "Blank", progressEnd, new Item(), 1);
        }
    }

    public void Next(Slide slide)
    {
        int pos = slide.SlideIndex;
        if (pos == _slides.Count)
            return;
        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos + 1));
    }

    public void Previous(Slide slide)
    {
        int pos = slide.SlideIndex;
        if (pos == 1)
            return;
        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos - 1));
    }

    //navigation is driven entirely by the operator list selection; the engine has no own window
    public void GoTo(Slide slide) { }

    public void UpdateSlideTimings() { }

    //previews are set while building (the pages are already rendered), nothing to export
    public string? ExportSlideImage(Slide slide, int idx, string suffix, int width, int height) => null;

    public int? GetSlideWindowHandle(Slide slide) => null;

    public void Reset(Slide slide) { }

    public void BringToFront(Slide slide) { }

    public bool TryEditSlide(Slide slide) => false;

    private Slide AddSlide(SlideType type, string filename, string text, string comment, double progress, Item scheduleItem, int itemIndex)
    {
        return AddSlide(new Slide(type, filename) { Text = text, Comment = comment }, progress, scheduleItem, itemIndex);
    }

    private Slide AddSlide(Slide s, double progress, Item scheduleItem, int itemIndex)
    {
        s.SlideIndex = _slides.Count + 1;
        s.ScheduleItem = scheduleItem;
        s.ItemIndex = itemIndex;
        _slides.Add(s);

        SlideAdded?.Invoke(this, new SlideAddedEventArgs(s, progress));

        return s;
    }

    /// <summary>Loads an image scaled to fit, frozen so it can cross threads.</summary>
    private static BitmapSource RetrieveImage(string filename, int width, int height)
    {
        BitmapSource photo = BitmapDecoder.Create(new Uri(filename), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (photo.Width >= width || photo.Height >= height)
        {
            double scale = Math.Min(width / photo.Width, height / photo.Height);
            photo = BitmapFrame.Create(new TransformedBitmap(photo, new ScaleTransform(scale * 96 / photo.DpiX, scale * 96 / photo.DpiY, 0, 0)));
        }
        if (photo.CanFreeze)
            photo.Freeze();
        return photo;
    }

    private List<(BitmapSource Full, BitmapSource Preview)> RenderSlides(string filename)
    {
        string pdf = ConvertToPdf(filename);
        try
        {
            return RasterizePdf(pdf).GetAwaiter().GetResult();
        }
        finally
        {
            try { File.Delete(pdf); } catch (Exception) { }
        }
    }

    private static string ConvertToPdf(string filename)
    {
        string soffice = SofficeLocator.Find()
            ?? throw new InvalidOperationException("LibreOffice (soffice.exe) was not found. Install LibreOffice or select the PowerPoint engine in Options.");

        string outDir = Path.Combine(TempPath, "render");
        Directory.CreateDirectory(outDir);
        //private profile so conversion doesn't clash with an open LibreOffice instance
        string profile = new Uri(Path.Combine(TempPath, "lo-profile")).AbsoluteUri;

        var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add("-env:UserInstallation=" + profile);
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(filename);

        using var proc = Process.Start(psi)!;
        if (!proc.WaitForExit(TimeSpan.FromMinutes(3)))
        {
            proc.Kill(true);
            throw new InvalidOperationException("LibreOffice timed out converting " + Path.GetFileName(filename));
        }

        string pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(filename) + ".pdf");
        if (!File.Exists(pdf))
            throw new InvalidOperationException("LibreOffice could not convert " + Path.GetFileName(filename));
        return pdf;
    }

    private async Task<List<(BitmapSource, BitmapSource)>> RasterizePdf(string pdfPath)
    {
        var result = new List<(BitmapSource, BitmapSource)>();
        var file = await StorageFile.GetFileFromPathAsync(pdfPath);
        var doc = await PdfDocument.LoadFromFileAsync(file);
        var b = _screens.ProjectorBounds;

        for (uint i = 0; i < doc.PageCount; i++)
        {
            using var page = doc.GetPage(i);
            result.Add((await RenderPage(page, b.Width, b.Height), await RenderPage(page, 333, 250)));
        }
        return result;
    }

    private static async Task<BitmapSource> RenderPage(PdfPage page, int maxWidth, int maxHeight)
    {
        double scale = Math.Min(maxWidth / page.Size.Width, maxHeight / page.Size.Height);
        var options = new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, page.Size.Width * scale),
            DestinationHeight = (uint)Math.Max(1, page.Size.Height * scale),
        };

        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, options);
        stream.Seek(0);

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = stream.AsStreamForRead();
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static string TempPath
    {
        get
        {
            string temp = Path.Combine(Path.GetTempPath(), "presenter") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(temp);
            return temp;
        }
    }
}
