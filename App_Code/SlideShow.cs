using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
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
                app.ShowWindowsInTaskbar = Core.MsoTriState.msoFalse;
                app.SlideShowEnd += new PP.EApplication_SlideShowEndEventHandler(app_SlideShowEnd);

                _slides.Clear();
                if (SlideAdded != null)
                    SlideAdded(this, new SlideAddedEventArgs(null, -1));

                AddSlide("", "Blank", null, SlideType.Blank, "", 0, new Item(), 1);

                foreach (Item item in items)
                    AddSlides(item);
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

        private void app_SlideShowEnd(PP.Presentation Pres)
        {
            if (SlideShowEnd != null)
                SlideShowEnd(this, new EventArgs());
        }

        private void AddSlide(string text, string comments, PP.Presentation pres, SlideType type, string filename, double progress, Item scheduleItem, int itemIndex)
        {
            Slide s = new Slide(type, filename) { Text = text, Comment = comments, Presentation = pres, SlideIndex = _slides.Count + 1, ScheduleItem = scheduleItem, ItemIndex = itemIndex };
            _slides.Add(s);

            if (SlideAdded != null)
                SlideAdded(this, new SlideAddedEventArgs(s, progress));
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            PP.Presentation[] list = Slides.Where(s => s.Presentation != null).Select(s => s.Presentation).Distinct().ToArray();

            foreach (PP.Presentation pres in list)
            {
                try { pres.SlideShowWindow.View.Exit(); }
                catch (InvalidCastException) { }
                catch (COMException) { }
            }

            Slides.ForEach(s => s.Presentation = null);

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
                slide.Presentation.SlideShowWindow.View.GotoSlide(slide.ItemIndex);
                User32.SendWindowToFront(slide.Presentation.SlideShowWindow.HWND);
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
            int pos = Slides.FindIndex(s => s.SlideIndex == slide.SlideIndex) + 1;
            if (pos == Slides.Length)
                return;

            if (slide.Type != SlideType.PowerPoint || slide.Presentation.SlideShowWindow.View.CurrentShowPosition == slide.Presentation.Slides.Count)
            {
                if (Slides[pos].Type == SlideType.PowerPoint)
                    Slides[pos].Presentation.SlideShowWindow.View.GotoSlide(Slides[pos].ItemIndex);

                if (SlideIndexChanged != null)
                    SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + 1));
                return;
            }

            int pcurpos = slide.Presentation.SlideShowWindow.View.CurrentShowPosition;
            if (pcurpos > slide.Presentation.Slides.Count)
                return;
            slide.Presentation.SlideShowWindow.View.Next();
            int pnewpos = slide.Presentation.SlideShowWindow.View.CurrentShowPosition;
            
            if (SlideIndexChanged != null)
                SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + (pnewpos - pcurpos)));
        }

        public void Previous(Slide slide)
        {
            int pos = Slides.FindIndex(s => s.SlideIndex == slide.SlideIndex) + 1;
            if (pos == 1)
                return;

            if (slide.Type != SlideType.PowerPoint || slide.Presentation.SlideShowWindow.View.CurrentShowPosition == 1)
            {
                if (Slides[pos - 2].Type == SlideType.PowerPoint)
                    Slides[pos - 2].Presentation.SlideShowWindow.View.GotoSlide(Slides[pos - 2].ItemIndex);

                if (SlideIndexChanged != null)
                    SlideIndexChanged(this, new SlideShowEventArgs(pos, pos - 1));
                return;
            }

            int pcurpos = slide.Presentation.SlideShowWindow.View.CurrentShowPosition;
            slide.Presentation.SlideShowWindow.View.Previous();
            int pnewpos = slide.Presentation.SlideShowWindow.View.CurrentShowPosition;

            if (SlideIndexChanged != null)
                SlideIndexChanged(this, new SlideShowEventArgs(pos, pos + (pnewpos - pcurpos)));
        }

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
                AddSlide(scheduleItem.Name, Labels.SlideShowVideoLabel, null, SlideType.Video, filename, progressEnd, scheduleItem, 1);
            }
            else if (Config.AudioFormats.Contains(filetype))
            {
                AddSlide(scheduleItem.Name, Labels.SlideShowAudioLabel, null, SlideType.Audio, filename, progressEnd, scheduleItem, 1);
            }
            else if (Config.ImageFormats.Contains(filetype))
            {
                AddSlide(scheduleItem.Name, Labels.SlideShowImageLabel, null, SlideType.Image, filename, progressEnd, scheduleItem, 1);
            }
            /*else if (Config.PowerPointTemplates.Contains(filetype))
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
            }*/
            else if (Config.PowerPointFormats.Contains(filetype))
            {
                PP.Presentation pres = OpenPresentation(filename);

                for (int i = 1; i <= pres.Slides.Count; i++)
                    AddSlide(GetStringSummary(pres.Slides[i].Shapes), GetStringSummary(pres.Slides[i].NotesPage.Shapes), pres, SlideType.PowerPoint, "", progressEnd, scheduleItem, i);

                pres.SlideShowSettings.AdvanceMode = PP.PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                if (!IsRunning)
                {
                    pres.Close();
                    return;
                }
                pres.SlideShowSettings.Run();

                var taskbarList = (ITaskbarList)new CTaskbarList();
                taskbarList.HrInit();
                taskbarList.DeleteTab(new IntPtr(pres.SlideShowWindow.HWND));
            }

            if (Config.InsertBlankAfterPres && Config.PowerPointFormats.Contains(filetype) || Config.InsertBlankAfterVideo && Config.VideoFormats.Concat(Config.AudioFormats).Contains(filetype))
            {
                AddSlide("", "Blank", null, SlideType.Blank, "", progressEnd, new Item(), 1);
            }
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
        public static string ExportToImage(object slide, int idx, string suffix, int width, int height)
        {
            if (slide as PP.Slide == null)
                return "";

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
            (slide as PP.Slide).Export(temp, "PNG", width, height);

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
