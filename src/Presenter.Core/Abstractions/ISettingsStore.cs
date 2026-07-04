using Presenter.Core.Settings;

namespace Presenter.Core.Abstractions;

/// <summary>
/// Loads and persists application settings (replaces the app.config read/write logic
/// in App_Code/Config.cs).
/// </summary>
public interface ISettingsStore
{
    PresenterSettings Current { get; }

    void Save();
}
