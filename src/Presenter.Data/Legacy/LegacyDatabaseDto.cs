namespace Presenter.Data.Legacy;

/// <summary>
/// JSON shape produced by the Presenter.SdfExport tool (net48) when exporting the
/// legacy SQL Server Compact database. Keep in sync with src/Presenter.SdfExport.
/// </summary>
public class LegacyDatabaseDto
{
    public List<LegacyScheduleDto> Schedules { get; set; } = [];
    public List<LegacyItemDto> Items { get; set; } = [];
    public List<LegacyFlagDto> Flags { get; set; } = [];
}

public class LegacyScheduleDto
{
    public Guid ID { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
}

public class LegacyItemDto
{
    public Guid ID { get; set; }
    public Guid ScheduleID { get; set; }
    public string Filename { get; set; } = "";
    public short Ordinal { get; set; }
}

public class LegacyFlagDto
{
    public Guid ItemID { get; set; }
    public short Index { get; set; }
    public string Colour { get; set; } = "";
}
