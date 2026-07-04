using Presenter.Data;

namespace Presenter.Data.Tests;

/// <summary>Creates an isolated temp SQLite database per test class instance.</summary>
public abstract class SqliteTestBase : IDisposable
{
    private readonly string _dbPath;

    protected SqliteTestBase()
    {
        string dir = Path.Combine(Path.GetTempPath(), "presenter-tests");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".db");
    }

    protected PresenterDbContext CreateContext() => PresenterDb.Create(_dbPath);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }
}
