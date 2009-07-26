using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Objects;
using System.Linq;
using System.Text;
using System.IO;

namespace SongPresenter.App_Code
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
            Save(true);
        }

        private void Save(bool removeExistingPres)
        {
            if (removeExistingPres)
                SlideShow.RemoveOldPres();

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
        public void AddItem(string filename)
        {
            AddItem(filename, true);
        }

        public void AddItem(string filename, bool removeExistingPres)
        {
            if (!Config.SupportedFileTypes.Any(t => (filename ?? "").ToLower().EndsWith("." + t)))
                return;

            Items.Add(new Item()
            {
                ID = Guid.NewGuid(),
                Filename = filename,
                Ordinal = Items.Count > 0 ? Items.Max(i => i.Ordinal) + 1 : 0
            });
            Save(removeExistingPres);
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

            int destIdx = (dest == null) ? Items.Count - 1 : Items.FirstOrDefault(i => i.ID == dest.ID).Ordinal;
            var list = Items.ToDictionary(i => i.Ordinal);
            
            if (destIdx < source.Ordinal)
            {
                for (int j = destIdx; j < source.Ordinal; j++)
                    list[j].Ordinal++;
            }
            else if (destIdx > source.Ordinal)
            {
                for (int j = source.Ordinal + 1; j <= destIdx; j++)
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

        public static void DeleteSchedule(Guid id)
        {
            var schedule = LoadSchedule(id);

            //if presentations are kept, then remove saved presentation when schedule is deleted
            string path = Config.PresentationPath + schedule.DisplayName + ".ppt";
            if (File.Exists(path))
                File.Delete(path);

            DB.Instance.DeleteObject(schedule);
            DB.Instance.SaveChanges();
        }
    }
}
