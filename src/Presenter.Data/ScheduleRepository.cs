using Microsoft.EntityFrameworkCore;
using Presenter.Core;
using Presenter.Core.Models;

namespace Presenter.Data;

/// <summary>
/// Persistence operations for schedules, ported from the static methods on
/// App_Code/Schedule.cs and App_Code/Item.cs.
/// </summary>
public class ScheduleRepository(PresenterDbContext db)
{
    public PresenterDbContext Db => db;

    public Schedule? LoadSchedule(Guid id)
    {
        return db.Schedules
            .Include(s => s.Items)
            .ThenInclude(i => i.Flags)
            .FirstOrDefault(s => s.ID == id);
    }

    /// <summary>Load all the schedules for the month containing <paramref name="date"/>.</summary>
    public List<Schedule> LoadSchedules(DateTime date)
    {
        DateTime start = new(date.Year, date.Month, 1);
        DateTime end = start.AddMonths(1);
        return db.Schedules
            .Where(s => s.Date >= start && s.Date < end)
            .OrderBy(s => s.Date).ThenBy(s => s.Name)
            .ToList();
    }

    /// <summary>Load all schedules.</summary>
    public List<Schedule> LoadSchedules()
    {
        return db.Schedules.OrderBy(s => s.Date).ThenBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Inserts the schedule when new (empty/unknown ID), otherwise persists pending
    /// changes (port of Schedule.Save()).
    /// </summary>
    public void Save(Schedule schedule)
    {
        if (schedule.ID == Guid.Empty)
            schedule.ID = Guid.NewGuid();

        if (db.Entry(schedule).State == EntityState.Detached && !db.Schedules.Any(s => s.ID == schedule.ID))
            db.Schedules.Add(schedule);

        db.SaveChanges();
    }

    public void SaveChanges() => db.SaveChanges();

    public void DeleteSchedule(Guid id)
    {
        var schedule = db.Schedules.Find(id);
        if (schedule == null)
            return;
        db.Schedules.Remove(schedule); // items and flags cascade
        db.SaveChanges();
    }

    /// <summary>
    /// Usage statistics per media file across all schedules in the date range
    /// (port of Item.GetUsageStats). <paramref name="libraries"/> holds lower-case
    /// library folder names; the special entry "other" inverts the match to count
    /// files outside the named libraries.
    /// </summary>
    public ItemUsage[] GetUsageStats(DateTime fromD, DateTime toD, string[] libraries, string libraryPath)
    {
        bool include = true;
        if (libraries.Contains("other"))
        {
            libraries = Directory.GetDirectories(libraryPath)
                .Select(p => Path.GetFileName(p).ToLowerInvariant())
                .Except(libraries).ToArray();
            include = false;
        }

        var rows = db.Items.AsNoTracking()
            .Where(i => i.Schedule!.Date >= fromD && i.Schedule.Date <= toD)
            .Select(i => new { i.Filename, i.Schedule!.Date, i.Schedule.Name })
            .ToArray();

        // normalize separators so filenames migrated from the Windows app match on macOS/Linux
        string sep = Path.DirectorySeparatorChar.ToString();
        return (from i in rows
                let filename = Util.NormalizeSeparators(i.Filename)
                where libraries.Any(l => filename.ToLowerInvariant().Contains(sep + l + sep)) == include
                      && !filename.EndsWith("None.pot")
                group i by Path.GetFileNameWithoutExtension(filename) into g
                select new ItemUsage
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Presentations = g.OrderBy(i => i.Date)
                        .Select(i => i.Date.ToLongDateString() + ", " + i.Name)
                        .Distinct().ToArray()
                })
               .OrderBy(s => s.Count).ThenByDescending(s => s.Name)
               .ToArray();
    }
}
