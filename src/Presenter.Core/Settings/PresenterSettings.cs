namespace Presenter.Core.Settings;

public enum MessengerVerticalPosition { Top, Center, Bottom }
public enum MessengerHorizontalPosition { Left, Center, Right }

/// <summary>
/// All user settings, previously stored in app.config appSettings (see App_Code/Config.cs).
/// Serialized to settings.json under %LocalAppData%\AudiovisualPresenter.
/// </summary>
public class PresenterSettings
{
    // file format lists (extensions, lower case, no dot)
    public List<string> PowerPointFormats { get; set; } = ["ppt", "pps", "pptx", "ppsx", "pptm"];
    public List<string> PowerPointTemplates { get; set; } = ["pot", "potx", "potm"];
    public List<string> ImageFormats { get; set; } = ["jpg", "jpeg", "wmp", "png"];
    // http://support.microsoft.com/kb/316992
    public List<string> VideoFormats { get; set; } =
        ["dvr-ms", "wtv", "mpeg", "mpg", "m1v", "m2v", "mod", "mpa", "mpe", "ifo", "vob", "mp4", "m4v", "mp4v",
         "3gp", "3gpp", "3g2", "3gp2", "m2ts", "m2t", "mts", "ts", "tts", "mov", "avi", "wmv", "asf", "wm", "wmd", "flv"];
    public List<string> AudioFormats { get; set; } =
        ["mid", "rmi", "midi", "m4a", "wav", "snd", "au", "aif", "aifc", "aiff", "wma", "mp2", "mp3", "adts", "adt", "aac"];

    // library
    public string LibraryPath { get; set; } = @"My Documents\Library\";
    public string? SelectedLibrary { get; set; } = "Music";

    // appearance
    public string AppColour { get; set; } = "white";
    public string ScreenBlankColour { get; set; } = "black";
    public double? FontSize { get; set; }

    /// <summary>Slide preview panel at the bottom (true) or right (false); null = decide from screen ratio.</summary>
    public bool? SlidePreviewBottom { get; set; }

    // playback behaviour
    public bool InsertBlankAfterPres { get; set; } = true;
    public bool InsertBlankAfterVideo { get; set; } = true;
    public bool UseSlideTimings { get; set; } = true;
    public int TimerInterval { get; set; } = 8;

    // screens
    public bool UseNonPrimaryScreen { get; set; } = true;

    /// <summary>
    /// Device name of the projector screen chosen by the user. Null = auto-detect.
    /// (The COM engine additionally honours PowerPoint's own DisplayMonitor registry
    /// setting, as the original app did.)
    /// </summary>
    public string? ProjectorScreenDevice { get; set; }

    // on-screen messenger (was "MessengerFont" = "46 Arial White" etc.)
    public double MessengerFontSize { get; set; } = 46;
    public string MessengerFontFamily { get; set; } = "Arial";
    public string MessengerFontColour { get; set; } = "White";
    public MessengerVerticalPosition MessengerVertical { get; set; } = MessengerVerticalPosition.Bottom;
    public MessengerHorizontalPosition MessengerHorizontal { get; set; } = MessengerHorizontalPosition.Left;
    /// <summary>Margin as "left,top,right,bottom" (parsed into a Thickness by the UI).</summary>
    public string MessengerMargin { get; set; } = "0,0,0,8";

    /// <summary>Preferred presentation engine; "com" (drive PowerPoint) or "uno"
    /// (live LibreOffice Impress slideshows).</summary>
    public string PreferredEngine { get; set; } = "com";

    /// <summary>Union of all supported media file extensions.</summary>
    public IReadOnlyCollection<string> SupportedFileTypes =>
        PowerPointFormats.Union(ImageFormats).Union(VideoFormats).Union(AudioFormats).Union(PowerPointTemplates).ToArray();
}
