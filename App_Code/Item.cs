using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Collections;

namespace SongPresenter.App_Code
{
    public partial class Item
    {
        //properties
        public string Name
        {
            get { return Path.GetFileName(Filename); }
        }

        private HighlightedCollection _highlighted;
        public HighlightedCollection Highlighted
        {
            get
            {
                if (_highlighted == null)
                {
                    _highlighted = new HighlightedCollection();
                    if (HighlightedIndexes != null)
                    {
                        //highlighted indexes are stored in binary format to save space
                        //slide index:  5	4	3	2	1
                        //highlighted:  1   0   0	0	1   = 0x11
                        BitArray array = new BitArray(HighlightedIndexes);
                        for (int i = 1; i < array.Length; i++)
                            if (array[i - 1])
                                _highlighted.Add(i);
                    }

                    _highlighted.PropertyChanged += new PropertyChangedEventHandler(_highlighted_PropertyChanged);
                }

                return _highlighted;
            }
        }

        protected void _highlighted_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Highlighted.Count == 0)
            {
                HighlightedIndexes = new byte[0];
                DB.Instance.SaveChanges();
                return;
            }

            BitArray array = new BitArray(Highlighted.Max());
            for (int i = 1; i <= array.Length; i++)
                array[i - 1] = Highlighted.Contains(i);

            byte[] bytes = new byte[array.Length];
            array.CopyTo(bytes, 0);
            HighlightedIndexes = bytes;

            DB.Instance.SaveChanges();
        }

        private bool? _found;
        public bool IsFound
        {
            get
            {
                if (_found.HasValue)
                    return _found.Value;
                
                if (File.Exists(Filename))
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
                    where libraries.Any(l => i.Filename.ToLower().Contains("\\" + l + "\\")) == include
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

    public class HighlightedCollection : System.Collections.ObjectModel.Collection<int>, INotifyPropertyChanged
    {
        public HighlightedCollection() : base() { }
        public HighlightedCollection(IList<int> List) : base(List) { }

        protected override void InsertItem(int index, int item)
        {
            base.InsertItem(index, item);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(null));
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(null));
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(null));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
