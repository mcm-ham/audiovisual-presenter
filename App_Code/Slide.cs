using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace SongPresenter.App_Code
{
    public class Slide
    {
        public string Text { get; set; }
        public string Comment { get; set; }
        public object PSlide { get; set; }
        public int? JumpIndex { get; set; }
        public SlideType Type { get; set; }
        public string Filename { get; set; }
        public Item ScheduleItem { get; set; }

        /// <summary>
        /// one based index of the slide position in Schedule
        /// </summary>
        public int SlideIndex { get; set; }

        /// <summary>
        /// one based index of slide in ScheduleItem
        /// </summary>
        public int ItemIndex { get; set; }

        private string _preview;
        public string Preview
        {
            get
            {
                if (_preview == null)
                    _preview = SlideShow.ExportToImage(PSlide, SlideIndex, "-preview", 333, 250);
                return _preview;
            }
        }
    }
}
