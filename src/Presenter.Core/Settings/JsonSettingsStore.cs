using System.Text.Json;
using System.Text.Json.Serialization;
using Presenter.Core.Abstractions;

namespace Presenter.Core.Settings;

/// <summary>
/// JSON-file backed settings store. Default location:
/// %LocalAppData%\AudiovisualPresenter\settings.json.
/// Unknown/missing properties fall back to defaults, so settings survive upgrades.
/// </summary>
public class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _path;
    private PresenterSettings? _current;

    public JsonSettingsStore() : this(DefaultPath) { }

    public JsonSettingsStore(string path) => _path = path;

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudiovisualPresenter");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public PresenterSettings Current => _current ??= Load();

    private PresenterSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<PresenterSettings>(File.ReadAllText(_path), Options) ?? new PresenterSettings();
        }
        catch (Exception)
        {
            // corrupt settings file — fall back to defaults rather than failing startup
        }
        return new PresenterSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
    }
}
