using System;
using System.Data;
using System.IO;
using System.Linq;

namespace Presenter.App_Code
{
    public partial class Schedule
    {
        public string DisplayName
        {
            get { return Date.ToString("ddd, d MMM yyyy") + " - " + Name; }
        }

        //methods
        public void Save()
        {
            if (EntityState == EntityState.Detached)
            {
                ID = Guid.NewGuid();
                DB.Instance.AddToSchedules(this);
            }

            DB.Instance.SaveChanges();
        }

        /// <summary>
        /// Adds an item to schedule and saves back to the database immediately.
        /// </summary>
        /// <returns>Returns 0 if successful or 1 if the file is not supported</returns>
        public int AddItem(string filename)
        {
            string ext = System.IO.Path.GetExtension(filename).TrimStart('.').ToLower();
            if (!Config.SupportedFileTypes.Contains(ext))
                return 1;

            Items.Add(new Item() {
                ID = Guid.NewGuid(),
                Filename = filename,
                Ordinal = (short)((Items.Max(i => (short?)i.Ordinal) ?? -1) + 1)
            });
            Save();
            return 0;
        }

        /// <summary>
        /// Removes item from schedule and saves back to the database immediately. Workaround for remove not
        /// working on Items collection.
        /// </summary>
        public void RemoveItem(Item item)
        {
            ReOrder(item, null); //update ordinals or other items to reflect removal of this item
            DB.Instance.DeleteObject(item);
            DB.Instance.SaveChanges();
        }

        public void ReOrder(Item source, Item dest)
        {
            if (source == null)
                return;

            short destIdx = (dest == null) ? (short)(Items.Count - 1) : Items.FirstOrDefault(i => i.ID == dest.ID).Ordinal;
            var list = Items.ToDictionary(i => i.Ordinal);
            
            if (destIdx < source.Ordinal)
            {
                for (short j = destIdx; j < source.Ordinal; j++)
                    list[j].Ordinal++;
            }
            else if (destIdx > source.Ordinal)
            {
                for (short j = (short)(source.Ordinal + 1); j <= destIdx; j++)
                    list[j].Ordinal--;
            }

            source.Ordinal = destIdx;
            Save();
        }

        //static methods
        public static Schedule LoadSchedule(Guid id)
        {
            return DB.Instance.Schedules.Include("Items").FirstOrDefault(s => s.ID == id);
        }

        /// <summary>
        /// Load all the schedules for the given month
        /// </summary>
        public static IQueryable<Schedule> LoadSchedules(DateTime date)
        {
            DateTime start = new DateTime(date.Year, date.Month, 1);
            DateTime end = start.AddMonths(1);
            return DB.Instance.Schedules.Where(s => s.Date >= start && s.Date < end).OrderBy(s => s.Date).ThenBy(s => s.Name);
        }

        /// <summary>
        /// Load all schedules
        /// </summary>
        public static IQueryable<Schedule> LoadSchedules()
        {
            return DB.Instance.Schedules.OrderBy(s => s.Date).ThenBy(s => s.Name);
        }

        public static void DeleteSchedule(Guid id)
        {
            var schedule = LoadSchedule(id);
            DB.Instance.DeleteObject(schedule);
            DB.Instance.SaveChanges();
        }
    }
}
