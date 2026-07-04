using Presenter.Data;
using Presenter.Data.Legacy;

namespace Presenter.Data.Tests;

public class LegacyImporterTests : SqliteTestBase
{
    [Fact]
    public void ImportJson_ImportsAllEntitiesAndRelations()
    {
        var scheduleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        string json = $$"""
        {
          "Schedules": [ { "ID": "{{scheduleId}}", "Date": "2024-03-10T00:00:00", "Name": "Old Service" } ],
          "Items": [ { "ID": "{{itemId}}", "ScheduleID": "{{scheduleId}}", "Filename": "C:\\lib\\deck.pptx", "Ordinal": 0 } ],
          "Flags": [ { "ItemID": "{{itemId}}", "Index": 0, "Colour": "Blue" } ]
        }
        """;

        using (var db = CreateContext())
        {
            var counts = LegacyImporter.ImportJson(db, json);
            Assert.Equal(new LegacyImporter.ImportCounts(1, 1, 1), counts);
        }

        using (var db = CreateContext())
        {
            var loaded = new ScheduleRepository(db).LoadSchedule(scheduleId);
            Assert.NotNull(loaded);
            Assert.Equal("Old Service", loaded.Name);
            Assert.Equal(new DateTime(2024, 3, 10), loaded.Date);
            var item = Assert.Single(loaded.Items);
            Assert.Equal(@"C:\lib\deck.pptx", item.Filename);
            Assert.Equal("Blue", Assert.Single(item.Flags).Colour);
        }
    }

    [Fact]
    public void Import_SkipsOrphanedRows()
    {
        var dto = new LegacyDatabaseDto
        {
            Schedules = [new LegacyScheduleDto { ID = Guid.NewGuid(), Date = DateTime.Today, Name = "S" }],
            Items = [new LegacyItemDto { ID = Guid.NewGuid(), ScheduleID = Guid.NewGuid() /* no such schedule */, Filename = "x.mp4" }],
            Flags = [new LegacyFlagDto { ItemID = Guid.NewGuid() /* no such item */, Index = 0, Colour = "Red" }],
        };

        using var db = CreateContext();
        var counts = LegacyImporter.Import(db, dto);

        Assert.Equal(new LegacyImporter.ImportCounts(1, 0, 0), counts);
    }

    [Fact]
    public void TryMigrate_NoSdf_ReturnsNotNeeded()
    {
        using var db = CreateContext();
        var result = LegacySdfMigrator.TryMigrate(db, @"C:\does\not\exist.sdf", @"C:\also\missing.exe");

        Assert.Equal(MigrationStatus.NotNeeded, result.Status);
    }
}
