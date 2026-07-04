using Presenter.Core.Models;
using Presenter.Core.Settings;

namespace Presenter.Core.Tests;

public class ScheduleTests
{
    private static readonly IReadOnlyCollection<string> Supported = new PresenterSettings().SupportedFileTypes;

    private static Schedule NewSchedule(params string[] files)
    {
        var s = new Schedule { ID = Guid.NewGuid(), Date = new DateTime(2026, 7, 5), Name = "Test" };
        foreach (var f in files)
            Assert.True(s.AddItem(f, Supported));
        return s;
    }

    [Fact]
    public void AddItem_AssignsSequentialOrdinals()
    {
        var s = NewSchedule(@"C:\lib\a.pptx", @"C:\lib\b.mp4", @"C:\lib\c.jpg");

        Assert.Equal(new short[] { 0, 1, 2 }, s.Items.OrderBy(i => i.Ordinal).Select(i => i.Ordinal));
    }

    [Fact]
    public void AddItem_RejectsUnsupportedExtension()
    {
        var s = new Schedule();

        Assert.False(s.AddItem(@"C:\lib\file.xyz", Supported));
        Assert.Empty(s.Items);
    }

    [Fact]
    public void AddItem_IsCaseInsensitiveOnExtension()
    {
        var s = new Schedule();

        Assert.True(s.AddItem(@"C:\lib\DECK.PPTX", Supported));
    }

    [Fact]
    public void RemoveItem_RenumbersRemaining()
    {
        var s = NewSchedule(@"C:\a.pptx", @"C:\b.pptx", @"C:\c.pptx");
        var middle = s.Items.Single(i => i.Filename == @"C:\b.pptx");

        s.RemoveItem(middle);

        var remaining = s.Items.OrderBy(i => i.Ordinal).ToArray();
        Assert.Equal([@"C:\a.pptx", @"C:\c.pptx"], remaining.Select(i => i.Filename));
        Assert.Equal(new short[] { 0, 1 }, remaining.Select(i => i.Ordinal));
    }

    [Fact]
    public void MoveItem_MovesSourceBeforeDest()
    {
        var s = NewSchedule(@"C:\a.pptx", @"C:\b.pptx", @"C:\c.pptx");
        var a = s.Items.Single(i => i.Filename == @"C:\a.pptx");
        var c = s.Items.Single(i => i.Filename == @"C:\c.pptx");

        // move c to where a is => order: c, a, b
        s.MoveItem(c, a);

        Assert.Equal([@"C:\c.pptx", @"C:\a.pptx", @"C:\b.pptx"],
            s.Items.OrderBy(i => i.Ordinal).Select(i => i.Filename));
    }

    [Fact]
    public void MoveItem_NullDest_MovesToEnd()
    {
        var s = NewSchedule(@"C:\a.pptx", @"C:\b.pptx", @"C:\c.pptx");
        var a = s.Items.Single(i => i.Filename == @"C:\a.pptx");

        s.MoveItem(a, null);

        Assert.Equal([@"C:\b.pptx", @"C:\c.pptx", @"C:\a.pptx"],
            s.Items.OrderBy(i => i.Ordinal).Select(i => i.Filename));
    }

    [Fact]
    public void DisplayName_ContainsDateAndName()
    {
        var s = new Schedule { Date = new DateTime(2026, 7, 5), Name = "Morning" };

        Assert.EndsWith("- Morning", s.DisplayName);
        Assert.Contains("2026", s.DisplayName);
    }
}
