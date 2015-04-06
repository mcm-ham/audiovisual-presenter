using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Presenter.Resources;
using Core = Microsoft.Office.Core;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace Presenter.App_Code
{
    public class SlideShow
    {
        private PP.Application app;

        private List<Slide> _slides = new List<Slide>();
        public Slide[] Slides { get { return _slides.ToArray(); } }
        public bool IsRunning { get; private set; }

        public event EventHandler SlideShowEnd;
        public event EventHandler<SlideAddedEventArgs> SlideAdded;
        public event EventHandler<SlideShowEventArgs> SlideIndexChanged;

        public void Start(Schedule schedule)
        {
            try
            {
                Item[] items = schedule.Items.OrderBy(i => i.Ordinal).ToArray();
                IsRunning = true;

                app = new PP.Application();
                if (Util.Parse<double>(app.Version) < 14)
                    app.ShowWindowsInTaskbar = Core.MsoTriState.msoFalse;
                app.SlideShowEnd += new PP.EApplication_SlideShowEndEventHandler(app_SlideShowEnd);

                _slides.Clear();
                if (SlideAdded != null)
                    SlideAdded(this, new SlideAddedEventArgs(null, -1));

                AddSlide("", "Blank", null, null, SlideType.Blank, "", 0, new Item(), 1);

                template = null;
                foreach (Item item in items)
                    AddSlides(item);

                AddSlide("", "Blank", null, null, SlideType.Blank, "", 0, new Item(), 1);

                //don't enable event until after all slideshows have started to prevent the slideshow window popping up on top during start
                //now enabled in AddSlides method since don't want event triggered when adding slides during presentation, so event is removed then added
                //app.SlideShowNextSlide += new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);
                
                new Action(() => {
                    //TODO improve logic
                    while (IsRunning)
                    {
                        System.Threading.Thread.Sleep(100);
                        foreach (var p in app.Presentations.Cast<PP.Presentation>())
                        {
                            try
                            {
                                if (p.SlideShowWindow().View.State == PP.PpSlideShowState.ppSlideShowDone)
                                    app_SlideShowNextSlide(p.SlideShowWindow());
                            }
                            catch (Exception) { }
                        }
                    }
                }).BeginInvoke(null, null);
            }
            catch (Exception ex)
            {
                Stop();

                if (!Application.Current.Dispatcher.CheckAccess())
                    Application.Current.Dispatcher.Invoke(new Action(() => { throw new Exception(ex.Message, ex); }));
                else
                    throw ex;
            }
        }

        /// <summary>
        /// Update the UseSlideTimings property for running slideshows.
        /// </summary>
        public void UpdateSlideTimings()
        {
            app.SlideShowNextSlide -= new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);
            foreach (var p in GetPresentations())
            {
                if (!Config.UseSlideTimings)
                    p.Slides.Range().SlideShowTransition.AdvanceOnTime = Core.MsoTriState.msoFalse;
                else
                    p.Slides.Cast<PP.Slide>().ToList().ForEach(ss => ss.SlideShowTransition.AdvanceOnTime = Slides.Where(s => s.PSlide == ss).Select(s => (Core.MsoTriState?)s.AdvanceOnTime).FirstOrDefault() ?? Core.MsoTriState.msoTrue);
            }
            app.SlideShowNextSlide += new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);
        }

        private void app_SlideShowNextSlide(PP.SlideShowWindow Wn)
        {
            if (SlideIndexChanged == null)
                return;
            
            //logic to handle slideshow using timings reaching end, go to first slide of next slideshow
            if (Wn.View.State == PP.PpSlideShowState.ppSlideShowDone)
            {
                Slide slide = Slides.FirstOrDefault(s => s.PSlide == Wn.Presentation.Slides[Wn.Presentation.Slides.Count]);
                if (slide != null)
                {
                    app.SlideShowNextSlide -= new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);
                    //to change slideshow state from ppSlideShowDone so this method does not continuously run
                    Wn.View.Last();
                    //if first slide of next slideshow has timings, go to slide in order to reset timings to allow it to advance (view reset timings method seemed to have no effect)
                    if (Slides[slide.SlideIndex].PSlide != null)
                        GoTo(Slides[slide.SlideIndex]);
                    app.SlideShowNextSlide += new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);
                    Application.Current.Dispatcher.Invoke(new Action(() => { SlideIndexChanged(this, new SlideShowEventArgs(-1, slide.SlideIndex + 1)); }));
                }
            }
            else
            {
                Slide slide = Slides.FirstOrDefault(s => s.PSlide == Wn.View.Slide);
                if (slide != null)
                    Application.Current.Dispatcher.Invoke(new Action(() => { SlideIndexChanged(this, new SlideShowEventArgs(-1, slide.SlideIndex)); }));
            }
        }

        private void app_SlideShowEnd(PP.Presentation Pres)
        {
            if (SlideShowEnd != null)
                SlideShowEnd(this, new EventArgs());
        }

        public PP.Presentation[] GetPresentations()
        {
            return Slides.Where(s => s.Presentation != null).Select(s => s.Presentation).Distinct().ToArray();
        }

        private Slide AddSlide(string text, string comments, PP.Presentation pres, Process process, SlideType type, string filename, double progress, Item scheduleItem, int itemIndex)
        {
            Slide s = new Slide(type, filename) { Text = text, Comment = comments, Presentation = pres, Process = process, SlideIndex = _slides.Count + 1, ScheduleItem = scheduleItem, ItemIndex = itemIndex };
            _slides.Add(s);

            if (SlideAdded != null)
                SlideAdded(this, new SlideAddedEventArgs(s, progress));

            return s;
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            foreach (PP.Presentation pres in GetPresentations())
            {
                try { pres.SlideShowWindow().View.Exit(); }
                catch (InvalidCastException) { }
                catch (COMException) { }
            }

            Slides.ForEach(s => s.Presentation = null);
            Util.SlideShowWindows.Clear();

            //needs to be called before calling running.close() otherwise when slideshowend event fires it will
            //call this code leading to an eternal loop
            IsRunning = false;
        }

        public void Quit()
        {
            app = new PP.Application();
            if (app.Presentations.Count == 0)
                app.Quit();
        }

        public void GoTo(Slide slide)
        {
            if (slide == null)
                return;

            try
            {
                slide.GotoSlide(slide.PSlide.SlideIndex);
                User32.SetWindowPos(slide.Presentation.SlideShowWindow().HWND, User32.HWND_TOP, Config.ProjectorScreen.Bounds.Left, Config.ProjectorScreen.Bounds.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
            }
            catch (COMException ex)
            {
                if (!ex.Message.Contains("There is currently no slide show"))
                    throw ex;
            }
            catch (InvalidCastException)
            {
                //the presentation has exited and the presentation reference (running) is now old
                app_SlideShowEnd(null);
            }
        }

        public void Next(Slide slide)
        {
            int pos = slide.SlideIndex;
            //fix issue in office 2013 where clicking next quickly thru slides CurrentShowPosition reports 0 on last slide
            //int pcurpos = slide.Presentation.SlideShowWindow().View.CurrentShowPosition;
            int pcurpos = slide.ItemIndex + 1;

            //GetClickIndex only available on office 2007 or higher so allow click to proceed (shows end of slideshow message) then switch to next presentation
            bool finishedCurrent = false;
            if (slide.Type == SlideType.PowerPoint && Util.Parse<double>(app.Version) >= 12)
                finishedCurrent = slide.Presentation.SlideShowWindow().View.GetClickIndex() >= slide.Presentation.SlideShowWindow().View.GetClickCount();
            
            //minus one from slide.Presentation.Slides.Count due to extra slide added at end that allowed slide animation on first slide
            if (slide.Type != SlideType.PowerPoint || pcurpos >= (slide.Presentation.Slides.Count - 1) && finishedCurrent)
            {
                if (pos == Slides.Length)
                    return;

                if (SlideIndexChanged != null)
                    SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + 1));

                //call GotoSlide if next slide is the start of a new presentation so that calling Next works with onclick animations
                Slide nextSlide = Slides[pos];
                if (nextSlide.Type == SlideType.PowerPoint)
                    nextSlide.GotoSlide(nextSlide.PSlide.SlideIndex);
                return;
            }

            if (pcurpos > slide.Presentation.Slides.Count)
            {
                if (pos == Slides.Length)
                    return;

                if (SlideIndexChanged != null)
                    SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + 1));

                //call GotoSlide if next slide is the start of a new presentation so that calling Next works with onclick animations
                Slide nextSlide = Slides[pos];
                if (nextSlide.Type == SlideType.PowerPoint)
                    nextSlide.GotoSlide(nextSlide.PSlide.SlideIndex);
                return;
            }

            slide.Next();
            
            int pnewpos = slide.Presentation.SlideShowWindow().View.CurrentShowPosition;

            if (SlideIndexChanged != null)
                SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + (pnewpos - pcurpos)));
        }

        public void Previous(Slide slide)
        {
            int pos = slide.SlideIndex;

            //GetClickIndex only available on office 2007 or higher, so clicking to previous animations on first slide is not supported
            int clickIndex = 0;
            if (slide.Type == SlideType.PowerPoint && Util.Parse<double>(app.Version) >= 12)
                clickIndex = slide.Presentation.SlideShowWindow().View.GetClickIndex();

            if (slide.Type != SlideType.PowerPoint || slide.Presentation.SlideShowWindow().View.CurrentShowPosition == 1 && clickIndex <= 0)
            {
                if (pos == 1)
                    return;

                if (SlideIndexChanged != null)
                    SlideIndexChanged(this, new SlideShowEventArgs(pos, pos - 1));

                Slide prevSlide = Slides[pos - 2];
                if (prevSlide.Type == SlideType.PowerPoint)
                    prevSlide.GotoSlide(prevSlide.PSlide.SlideIndex);
                return;
            }

            int pcurpos = slide.Presentation.SlideShowWindow().View.CurrentShowPosition;
            slide.Previous();
            int pnewpos = slide.Presentation.SlideShowWindow().View.CurrentShowPosition;

            if (SlideIndexChanged != null)
                SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + (pnewpos - pcurpos)));
        }

        private PP.Presentation template;
        public void AddSlides(Item scheduleItem)
        {
            if (!scheduleItem.IsFound || !IsRunning)
                return;
            
            double progress = scheduleItem.Ordinal / (double)scheduleItem.Schedule.Items.Count;
            double progressEnd = (scheduleItem.Ordinal + 1) / (double)scheduleItem.Schedule.Items.Count;
            string filename = Path.GetFullPath(scheduleItem.Filename).ToLower();
            string filetype = System.IO.Path.GetExtension(filename).TrimStart('.').ToLower();
            
            if (Config.VideoFormats.Contains(filetype))
            {
                AddSlide(scheduleItem.Name, Labels.SlideShowVideoLabel, null, null, SlideType.Video, filename, progressEnd, scheduleItem, 1);
            }
            else if (Config.AudioFormats.Contains(filetype))
            {
                AddSlide(scheduleItem.Name, Labels.SlideShowAudioLabel, null, null, SlideType.Audio, filename, progressEnd, scheduleItem, 1);
            }
            else if (Config.ImageFormats.Contains(filetype))
            {
                var s = AddSlide(scheduleItem.Name, Labels.SlideShowImageLabel, null, null, SlideType.Image, filename, progressEnd, scheduleItem, 1);
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    s.Image = SlideShow.RetrieveImage(s.Filename, Config.ProjectorScreen.Bounds.Width, Config.ProjectorScreen.Bounds.Height);
                    s.Preview = SlideShow.RetrieveImage(s.Filename, 333, 250);
                }));
            }
            else if (Config.PowerPointTemplates.Contains(filetype))
            {
                if (template != null)
                {
                    template.Close();
                    template = null;
                }

                if (!scheduleItem.IsTemplateNone)
                    template = OpenPresentation(filename);

                if (SlideAdded != null)
                    SlideAdded(this, new SlideAddedEventArgs(null, progressEnd));
            }
            else if (Config.PowerPointFormats.Contains(filetype))
            {
                var pres = OpenPresentation(filename);

                if (template != null)
                    pres.Slides.Range().Design = template.Designs[1];

                for (int i = 1; i <= pres.Slides.Count; i++)
                {
                    var defaultText = pres.Slides[i].Layout == PP.PpSlideLayout.ppLayoutBlank && pres.Slides[i].Shapes.Count == 0 ? "" : Labels.SlideShowSlideLabel + pres.Slides[i].SlideIndex;
                    var _slide = AddSlide(GetStringSummary(pres.Slides[i].Shapes).ToNullIfEmpty() ?? defaultText, GetStringSummary(pres.Slides[i].NotesPage.Shapes), pres, null, SlideType.PowerPoint, "", progressEnd, scheduleItem, i);
                    _slide.AnimationCount = pres.Slides[i].TimeLine.MainSequence.Cast<PP.Effect>().Sum(e => e.Timing.TriggerType == PP.MsoAnimTriggerType.msoAnimTriggerOnPageClick ? 1 : 0);
                    _slide.AdvanceOnTime = pres.Slides[i].SlideShowTransition.AdvanceOnTime;
                }

                pres.SlideShowSettings.AdvanceMode = PP.PpSlideShowAdvanceMode.ppSlideShowUseSlideTimings;
                if (!Config.UseSlideTimings)
                    pres.Slides.Range().SlideShowTransition.AdvanceOnTime = Core.MsoTriState.msoFalse;

                if (!IsRunning)
                {
                    pres.Close();
                    return;
                }

                app.SlideShowNextSlide -= new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);

                var slide = pres.Slides.Add(1, PP.PpSlideLayout.ppLayoutBlank);
                slide.FollowMasterBackground = Core.MsoTriState.msoFalse;
                slide.Background.Fill.ForeColor.RGB = Util.ToOle(Config.ScreenBlankColour);
                slide.Background.Fill.Solid();
                slide.SlideShowTransition.EntryEffect = PP.PpEntryEffect.ppEffectNone;

                //todo PPT 2010 shows presenter view, 2007 doesn't throws error if the is called though
                //pres.SlideShowSettings.ShowPresenterView = Core.MsoTriState.msoFalse;
                pres.SlideShowSettings.Run();
                Util.SlideShowWindows[pres] = pres.SlideShowWindow;
                
                //ensure presenter has focus otherwise if ppt has focus and user moves scrollwheel over presenter expecting the listview to scroll it will actually change slides instead and can end slideshow unexpectedly
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { System.Windows.Application.Current.MainWindow.Activate(); }));

                var taskbarList = (ITaskbarList)new CTaskbarList();
                taskbarList.HrInit();
                taskbarList.DeleteTab(new IntPtr(pres.SlideShowWindow().HWND));

                app.SlideShowNextSlide += new PP.EApplication_SlideShowNextSlideEventHandler(app_SlideShowNextSlide);
            }

            if (Config.InsertBlankAfterPres && Config.PowerPointFormats.Contains(filetype) || Config.InsertBlankAfterVideo && Config.VideoFormats.Concat(Config.AudioFormats).Contains(filetype))
            {
                AddSlide("", "Blank", null, null, SlideType.Blank, "", progressEnd, new Item(), 1);
            }
        }

        public static BitmapSource RetrieveImage(string filename, int width, int height)
        {
            var photo = BitmapDecoder.Create(new Uri(filename), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None).Frames[0];
            if (photo.Width < width && photo.Height < height)
                return photo;
            double scale = Math.Min(width / photo.Width, height / photo.Height);
            return BitmapFrame.Create(new TransformedBitmap(photo, new ScaleTransform(scale * 96 / photo.DpiX, scale * 96 / photo.DpiY, 0, 0)));
        }

        private PP.Presentation OpenPresentation(string filename)
        {
            PP.Presentation pres;

            //if 2007 format i.e. pptx and application is Office 2003 or lower need to open via shell to perform conversion to old file format
            if (filename.EndsWith("x") && Util.Parse<double>(app.Version) < 12)
            {
                int count = app.Presentations.Count;
                System.Diagnostics.Process.Start(filename);
                while (count == app.Presentations.Count) { }
                pres = app.Presentations[app.Presentations.Count];
            }
            else
                pres = app.Presentations.Open(filename, WithWindow: Core.MsoTriState.msoFalse);

            return pres;
        }

        private string GetStringSummary(PP.Shapes shapes)
        {
            string text = "";
            foreach (PP.Shape shape in shapes)
            {
                if (shape.Name.Contains("Slide Number Placeholder") || shape.Name == "source" || shape.HasTextFrame != Core.MsoTriState.msoTrue)
                    continue;

                try
                {
                    //remove slide numbers
                    if (text == "" && Util.Parse<int?>(shape.TextFrame.TextRange.Text).HasValue)
                        continue;

                    text += " " + shape.TextFrame.TextRange.Text.Replace("\r", " ").Replace("\n", " ").Replace("\v", " ").Trim();
                }
                catch (Exception) { }
            }
            return text.Trim();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slide">The slide to create the image from</param>
        /// <param name="suffix">A suffix to the filename to differentiate the images use</param>
        /// <param name="width">The width of the desired image, set to -1 to use slide width</param>
        /// <param name="height">The height of the desired image, set to -1 to use height width</param>
        /// <returns>The file path to the created image of slide</returns>
        public static string ExportToImage(Slide slide, int idx, string suffix, int width, int height)
        {
            string temp;

            for (int i = 0; true; i++)
            {
                temp = Config.TempPath + idx + suffix + i + ".png";

                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch (Exception) { }

                if (!File.Exists(temp))
                    break;
            }
            
            //if powerpoint closed, will throw error
            slide.PSlide.Export(temp, "PNG", width, height);

            return temp;
        }

        public static string UniqueFilename(string filename)
        {
            string temp = filename;

            for (int i = 0; true; i++)
            {
                temp = filename.Insert(filename.LastIndexOf('.'), i.ToString());

                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch (Exception) { }

                if (!File.Exists(temp))
                    break;
            }

            return temp;
        }
    }

    public class SlideShowEventArgs : EventArgs
    {
        public SlideShowEventArgs(int oldIdx, int newIdx) : base()
        {
            OldIndex = oldIdx;
            NewIndex = newIdx;
        }

        public int OldIndex { get; private set; }
        public int NewIndex { get; private set; }
    }

    public class SlideAddedEventArgs : EventArgs
    {
        public SlideAddedEventArgs(Slide slide, double progress) : base()
        {
            NewSlide = slide;
            Progress = progress;
        }

        public Slide NewSlide { get; private set; }
        public double Progress { get; private set; }
        public bool IsComplete { get; private set; }
    }
}
