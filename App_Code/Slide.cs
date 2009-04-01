using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Runtime.InteropServices;

namespace SongPresenter.App_Code
{
    public class Slide
    {
        public string Text { get; set; }
        public string Comment { get; set; }
        public int SlideIndex { get; set; }
        public object PSlide { get; set; }
        public int? JumpIndex { get; set; }
        public SlideType Type { get; set; }
        public string Filename { get; set; }

        private string _preview;
        public string Preview
        {
            get
            {
                if (_preview == null)
                {
                    if (Config.ThumbHeight == 0 || Config.ThumbWidth == 0 || PSlide == null)
                        return null;
                    
                    _preview = SlideShow.ExportToImage(PSlide, SlideIndex, "-preview", Config.ThumbWidth, Config.ThumbHeight);
                }
                return _preview;
            }
        }
    }
}
