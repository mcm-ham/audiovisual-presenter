using System.Text.Json;
using Presenter.Core.Models;

namespace Presenter.Data.Legacy;

/// <summary>
/// Imports a legacy database JSON export (produced by Presenter.SdfExport) into the
/// SQLite database.
/// </summary>
public static class LegacyImporter
{
    public record ImportCounts(int Schedules, int Items, int Flags);

    public static ImportCounts ImportJson(PresenterDbContext db, string json)
    {
        var dto = JsonSerializer.Deserialize<LegacyDatabaseDto>(json)
                  ?? throw new InvalidDataException("Legacy export JSON is empty.");
        return Import(db, dto);
    }

    public static ImportCounts Import(PresenterDbContext db, LegacyDatabaseDto dto)
    {
        // orphaned rows in the old DB (no FK enforcement gaps expected, but be safe)
        var scheduleIds = dto.Schedules.Select(s => s.ID).ToHashSet();
        var items = dto.Items.Where(i => scheduleIds.Contains(i.ScheduleID)).ToList();
        var itemIds = items.Select(i => i.ID).ToHashSet();
        var flags = dto.Flags.Where(f => itemIds.Contains(f.ItemID)).ToList();

        db.Schedules.AddRange(dto.Schedules.Select(s => new Schedule
        {
            ID = s.ID,
            Date = s.Date,
            Name = s.Name,
        }));

        db.Items.AddRange(items.Select(i => new Item
        {
            ID = i.ID,
            ScheduleID = i.ScheduleID,
            Filename = i.Filename,
            Ordinal = i.Ordinal,
        }));

        db.Flags.AddRange(flags.Select(f => new Flag
        {
            ItemID = f.ItemID,
            Index = f.Index,
            Colour = f.Colour,
        }));

        db.SaveChanges();
        return new ImportCounts(dto.Schedules.Count, items.Count, flags.Count);
    }
}
