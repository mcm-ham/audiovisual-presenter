using Presenter.Core.Settings;

namespace Presenter.Core.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir;

    public JsonSettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "presenter-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void MissingFile_YieldsDefaults()
    {
        var store = new JsonSettingsStore(Path.Combine(_dir, "settings.json"));

        Assert.True(store.Current.UseSlideTimings);
        Assert.Equal(8, store.Current.TimerInterval);
        Assert.Contains("pptx", store.Current.PowerPointFormats);
        Assert.Contains("pptx", store.Current.SupportedFileTypes);
    }

    [Fact]
    public void SaveAndReload_RoundTrips()
    {
        string path = Path.Combine(_dir, "settings.json");
        var store = new JsonSettingsStore(path);
        store.Current.TimerInterval = 12;
        store.Current.MessengerVertical = MessengerVerticalPosition.Top;
        store.Current.SelectedLibrary = "Videos";
        store.Current.VideoFormats.Add("mkv");
        store.Save();

        var reloaded = new JsonSettingsStore(path);
        Assert.Equal(12, reloaded.Current.TimerInterval);
        Assert.Equal(MessengerVerticalPosition.Top, reloaded.Current.MessengerVertical);
        Assert.Equal("Videos", reloaded.Current.SelectedLibrary);
        Assert.Contains("mkv", reloaded.Current.VideoFormats);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        string path = Path.Combine(_dir, "settings.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "{ not valid json !!!");

        var store = new JsonSettingsStore(path);

        Assert.Equal(8, store.Current.TimerInterval);
    }

    [Fact]
    public void UnknownProperties_AreIgnored()
    {
        string path = Path.Combine(_dir, "settings.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, """{ "TimerInterval": 20, "SomeFutureSetting": true }""");

        var store = new JsonSettingsStore(path);

        Assert.Equal(20, store.Current.TimerInterval);
    }
}
