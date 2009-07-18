using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SongPresenter.App_Code
{
    public partial class Item
    {
        public string Name
        {
            get { return System.IO.Path.GetFileName(Filename); }
        }

        //static methods
        public static ItemUsage[] GetUsageStats(DateTime fromD, DateTime toD)
        {
            return (from i in DB.Instance.Items.Select(i => new { i.Filename, i.Schedule.Date }).Where(i => i.Date >= fromD && i.Date <= toD).ToArray()
                    group i by i.Filename into g
                    select new ItemUsage() {
                        Name = System.IO.Path.GetFileNameWithoutExtension(g.Key) + "  ", //add whitespace to end to provide gap between label and y-axis
                        Count = g.Count(),
                        Dates = g.Select(i => i.Date).OrderBy(d => d).Distinct().ToArray()
                    }).OrderBy(s => s.Count).ThenByDescending(s => s.Name).ToArray();
        }
    }

    public class ItemUsage
    {
        public int Count { get; set; }
        public string Name { get; set; }
        public DateTime[] Dates { get; set; }
    }
}
