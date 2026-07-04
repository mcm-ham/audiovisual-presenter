using Microsoft.EntityFrameworkCore;
using Presenter.Core.Settings;

namespace Presenter.Data;

/// <summary>
/// Database location and context creation helpers.
/// </summary>
public static class PresenterDb
{
    /// <summary>%LocalAppData%\AudiovisualPresenter\presenter.db</summary>
    public static string DefaultPath => Path.Combine(JsonSettingsStore.DefaultDirectory, "presenter.db");

    public static DbContextOptions<PresenterDbContext> CreateOptions(string dbPath)
    {
        return new DbContextOptionsBuilder<PresenterDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
    }

    /// <summary>
    /// Creates a context on the given path (default location when null), ensuring the
    /// directory and schema exist.
    /// </summary>
    public static PresenterDbContext Create(string? dbPath = null)
    {
        dbPath ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var db = new PresenterDbContext(CreateOptions(dbPath));
        db.Database.EnsureCreated();
        return db;
    }
}
