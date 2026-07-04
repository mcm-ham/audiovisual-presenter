using Presenter.Core.Models;
using Presenter.Data;

namespace Presenter.Data.Tests;

public class ScheduleRepositoryTests : SqliteTestBase
{
    private static Schedule NewSchedule(string name, DateTime date)
    {
        var s = new Schedule { ID = Guid.NewGuid(), Name = name, Date = date };
        s.Items.Add(new Item { ID = Guid.NewGuid(), ScheduleID = s.ID, Filename = @"C:\lib\Presentations\deck.pptx", Ordinal = 0 });
        s.Items.Add(new Item { ID = Guid.NewGuid(), ScheduleID = s.ID, Filename = @"C:\lib\Videos\clip.mp4", Ordinal = 1 });
        s.Items.First().Flags.Add(new Flag { ItemID = s.Items.First().ID, Index = 0, Colour = "Red" });
        return s;
    }

    [Fact]
    public void SaveAndLoad_RoundTripsScheduleWithItemsAndFlags()
    {
        var schedule = NewSchedule("Morning", new DateTime(2026, 7, 5));

        using (var db = CreateContext())
            new ScheduleRepository(db).Save(schedule);

        using (var db = CreateContext())
        {
            var loaded = new ScheduleRepository(db).LoadSchedule(schedule.ID);

            Assert.NotNull(loaded);
            Assert.Equal("Morning", loaded.Name);
            Assert.Equal(2, loaded.Items.Count);
            var deck = loaded.Items.Single(i => i.Ordinal == 0);
            Assert.Equal(@"C:\lib\Presentations\deck.pptx", deck.Filename);
            Assert.Equal("Red", deck.Flags.Single().Colour);
        }
    }

    [Fact]
    public void Save_AssignsIdWhenEmpty()
    {
        var schedule = new Schedule { Name = "New", Date = DateTime.Today };

        using var db = CreateContext();
        new ScheduleRepository(db).Save(schedule);

        Assert.NotEqual(Guid.Empty, schedule.ID);
        Assert.Single(db.Schedules.ToList());
    }

    [Fact]
    public void DeleteSchedule_CascadesToItemsAndFlags()
    {
        var schedule = NewSchedule("Doomed", new DateTime(2026, 7, 5));
        using (var db = CreateContext())
            new ScheduleRepository(db).Save(schedule);

        using (var db = CreateContext())
            new ScheduleRepository(db).DeleteSchedule(schedule.ID);

        using (var db = CreateContext())
        {
            Assert.Empty(db.Schedules.ToList());
            Assert.Empty(db.Items.ToList());
            Assert.Empty(db.Flags.ToList());
        }
    }

    [Fact]
    public void LoadSchedules_ByMonth_FiltersAndOrders()
    {
        using (var db = CreateContext())
        {
            var repo = new ScheduleRepository(db);
            repo.Save(new Schedule { Name = "B-July", Date = new DateTime(2026, 7, 20) });
            repo.Save(new Schedule { Name = "A-July", Date = new DateTime(2026, 7, 20) });
            repo.Save(new Schedule { Name = "Early-July", Date = new DateTime(2026, 7, 1) });
            repo.Save(new Schedule { Name = "June", Date = new DateTime(2026, 6, 30) });
            repo.Save(new Schedule { Name = "August", Date = new DateTime(2026, 8, 1) });
        }

        using (var db = CreateContext())
        {
            var july = new ScheduleRepository(db).LoadSchedules(new DateTime(2026, 7, 15));

            Assert.Equal(["Early-July", "A-July", "B-July"], july.Select(s => s.Name));
        }
    }

    [Fact]
    public void GetUsageStats_CountsAcrossSchedules()
    {
        string library = Path.Combine(Path.GetTempPath(), "presenter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(library, "presentations"));

        using (var db = CreateContext())
        {
            var repo = new ScheduleRepository(db);
            var s1 = new Schedule { ID = Guid.NewGuid(), Name = "S1", Date = new DateTime(2026, 7, 1) };
            s1.Items.Add(new Item { ID = Guid.NewGuid(), ScheduleID = s1.ID, Filename = @"C:\lib\presentations\deck.pptx", Ordinal = 0 });
            var s2 = new Schedule { ID = Guid.NewGuid(), Name = "S2", Date = new DateTime(2026, 7, 8) };
            s2.Items.Add(new Item { ID = Guid.NewGuid(), ScheduleID = s2.ID, Filename = @"C:\lib\presentations\deck.pptx", Ordinal = 0 });
            s2.Items.Add(new Item { ID = Guid.NewGuid(), ScheduleID = s2.ID, Filename = @"C:\lib\videos\clip.mp4", Ordinal = 1 });
            repo.Save(s1);
            repo.Save(s2);

            var stats = repo.GetUsageStats(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), ["presentations"], library);

            var deck = Assert.Single(stats);
            Assert.Equal("deck", deck.Name);
            Assert.Equal(2, deck.Count);
            Assert.Equal(2, deck.Presentations.Length);
        }
    }
}
