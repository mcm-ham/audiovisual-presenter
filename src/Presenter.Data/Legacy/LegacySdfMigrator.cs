using System.Diagnostics;

namespace Presenter.Data.Legacy;

public enum MigrationStatus
{
    /// <summary>No legacy database found — nothing to do.</summary>
    NotNeeded,
    /// <summary>Target database already contains schedules — migration skipped.</summary>
    AlreadyHasData,
    Imported,
    Failed,
}

public record MigrationResult(MigrationStatus Status, LegacyImporter.ImportCounts? Counts = null, string? Error = null);

/// <summary>
/// One-time migration from the legacy Database.sdf: runs the net48 Presenter.SdfExport
/// tool (SQL CE cannot be read from .NET 10 directly) to produce JSON, then imports it.
/// </summary>
public static class LegacySdfMigrator
{
    public static MigrationResult TryMigrate(PresenterDbContext db, string sdfPath, string exporterPath)
    {
        if (!File.Exists(sdfPath))
            return new MigrationResult(MigrationStatus.NotNeeded);

        if (db.Schedules.Any())
            return new MigrationResult(MigrationStatus.AlreadyHasData);

        string jsonPath = Path.Combine(Path.GetTempPath(), $"presenter-legacy-{Guid.NewGuid():N}.json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exporterPath,
                Arguments = $"\"{sdfPath}\" \"{jsonPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start " + exporterPath);
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return new MigrationResult(MigrationStatus.Failed,
                    Error: $"SdfExport exited with code {process.ExitCode}: {stderr.Trim()}");

            var counts = LegacyImporter.ImportJson(db, File.ReadAllText(jsonPath));
            return new MigrationResult(MigrationStatus.Imported, counts);
        }
        catch (Exception ex)
        {
            return new MigrationResult(MigrationStatus.Failed, Error: ex.Message);
        }
        finally
        {
            try { File.Delete(jsonPath); } catch { }
        }
    }
}
