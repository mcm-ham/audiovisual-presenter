using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Collections;
using System.Windows.Media;
using System.Data;

namespace Presenter.App_Code
{
    public partial class Item
    {
        //properties
        public string Name
        {
            get { return Path.GetFileName(Filename); }
        }

        private bool? _found;
        public bool IsFound
        {
            get
            {
                if (_found.HasValue)
                    return _found.Value;

                if (File.Exists(Filename) || IsTemplateNone)
                {
                    _found = true;
                    return true;
                }
                
                //check for file under library in case library path has been moved
                string newpath = Config.LibraryPath + Filename.Substring(Filename.LastIndexOf('\\', Filename.LastIndexOf('\\') - 1) + 1);
                if (File.Exists(newpath))
                {
                    Filename = newpath;
                    DB.Instance.SaveChanges();
                    _found = true;
                    return true;
                }

                _found = false;
                return false;
            }
        }

        public bool IsTemplateNone
        {
            get { return Path.GetFileName(Filename).ToLower() == "none.pot"; }
        }

        //methods
        public void Save()
        {
            if (EntityState == EntityState.Detached)
            {
                ID = Guid.NewGuid();
                DB.Instance.AddToItems(this);
            }

            DB.Instance.SaveChanges();
        }

        //static methods
        public static ItemUsage[] GetUsageStats(DateTime fromD, DateTime toD, string[] libraries)
        {
            bool include = true;
            if (libraries.Contains("other"))
            {
                libraries = Directory.GetDirectories(Config.LibraryPath).Select(p => System.IO.Path.GetFileName(p).ToLower()).Except(libraries).ToArray();
                include = false;
            }

            return (from i in DB.Instance.Items.Select(i => new { i.Filename, i.Schedule.Date }).Where(i => i.Date >= fromD && i.Date <= toD).ToArray()
                    where libraries.Any(l => i.Filename.ToLower().Contains("\\" + l + "\\")) == include && !i.Filename.EndsWith("None.pot")
                    group i by System.IO.Path.GetFileNameWithoutExtension(i.Filename) into g
                    select new ItemUsage() {
                        Name = g.Key + "  ", //add whitespace to end to provide gap between label and y-axis
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
