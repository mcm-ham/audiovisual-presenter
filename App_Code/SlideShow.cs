using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Core = Microsoft.Office.Core;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace SongPresenter.App_Code
{
    public class SlideShow
    {
        private PP.Application app;
        private PP.Presentation running;

        private List<Slide> _slides = new List<Slide>();
        public Slide[] Slides { get { return _slides.ToArray(); } }
        public bool IsRunning { get; private set; }

        private static string _cpath;
        public static string CPath
        {
            get
            {
                if (_cpath == null)
                    _cpath = Config.TempPath + "current.ppt";
                return _cpath;
            }
            set { _cpath = value; }
        }

        public void Start(Schedule schedule)
        {
            if (Config.KeepPresentations)
                CPath = Config.PresentationPath + schedule.DisplayName + ".ppt";

            Item[] items = schedule.Items.OrderBy(i => i.Ordinal).ToArray();
            IsRunning = true;

            //if (app == null)
            {
                app = new PP.Application();
                app.SlideShowEnd += new PP.EApplication_SlideShowEndEventHandler(app_SlideShowEnd);
            }

            app.Activate();
            app.WindowState = PP.PpWindowState.ppWindowMinimized;
            Core.MsoTriState showWindow = Core.MsoTriState.msoTrue;

            //if (!File.Exists(CPath))
            {
                previousDesigns.Clear();
                _slides.Clear();
                if (SlideAdded != null)
                    SlideAdded(this, new SlideAddedEventArgs(null));

                running = app.Presentations.Open(Environment.CurrentDirectory + "\\base.pot", Core.MsoTriState.msoFalse, Core.MsoTriState.msoFalse, showWindow);
                AddSlide("", "Blank", running.Slides._Index(1));

                foreach (Item item in items)
                    AddSlides(item);

                //ppSaveAsPresentation = 1, ppSaveAsOpenXMLPresentation = 24
                PP.PpSaveAsFileType filetype = (PP.PpSaveAsFileType)(Util.Parse<double>(app.Version) >= 12 ? 24 : 1);

                running.SaveAs(CPath, (PP.PpSaveAsFileType)filetype, Core.MsoTriState.msoFalse);

                running.SlideShowSettings.Run();
            }
            //else
            //{
            //    running = app.Presentations.Open(CPath, Core.MsoTriState.msoFalse, Core.MsoTriState.msoFalse, showWindow);

            //    for (int i = 1; i <= Math.Min(Slides.Length, running.Slides.Count); i++)
            //        Slides[i - 1].PSlide = running.Slides._Index(i);

            //    running.SlideShowSettings.Run();
            //}

            //close master slide view
            PP.DocumentWindow wnd = ((PP.DocumentWindow)app.Windows._Index(1));
            wnd.ViewType = PP.PpViewType.ppViewSlide;
            wnd.ViewType = PP.PpViewType.ppViewNormal;
        }

        void app_SlideShowEnd(PP.Presentation Pres)
        {
            if (SlideShowEnd != null)
                SlideShowEnd(this, new EventArgs());
        }

        public event EventHandler SlideShowEnd;

        void AddSlide(string text, string comments, object slide)
        {
            AddSlide(text, comments, slide, SlideType.PowerPoint, "");
        }

        void AddSlide(string text, string comments, object slide, SlideType type, string filename)
        {
            Slide s = new Slide() { Text = text, Comment = comments, PSlide = slide, SlideIndex = _slides.Count + 1, Type = type, Filename = filename };
            _slides.Add(s);

            if (SlideAdded != null)
                SlideAdded(this, new SlideAddedEventArgs(s));
        }

        public event EventHandler<SlideAddedEventArgs> SlideAdded;

        public void Stop()
        {
            if (!IsRunning)
                return;

            foreach (Slide slide in Slides)
                slide.PSlide = null;

            //needs to be called before calling running.close() otherwise when slideshowend event fires it will
            //call this code leading to an eternal loop
            IsRunning = false;

            try { running.Close(); }
            catch (InvalidCastException) { }
            running = null;
        }

        public void Quit()
        {
            app = new PP.Application();
            if (app.Presentations.Count == 0)
                app.Quit();
        }

        public void GoTo(Slide slide)
        {
            if (slide == null || running == null)
                return;

            try { running.SlideShowWindow.View.GotoSlide(slide.SlideIndex, Core.MsoTriState.msoTrue); }
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

        public void Next()
        {
            int curpos = running.SlideShowWindow.View.CurrentShowPosition;
            if (curpos > Slides.Length)
                return;
            running.SlideShowWindow.View.Next();
            int newpos = running.SlideShowWindow.View.CurrentShowPosition; ;

            if (SlideIndexChanged != null)
                SlideIndexChanged(this, new SlideShowEventArgs(curpos, newpos));
        }

        public void Previous()
        {
            int curpos = running.SlideShowWindow.View.CurrentShowPosition;
            running.SlideShowWindow.View.Previous();
            int newpos = running.SlideShowWindow.View.CurrentShowPosition; ;

            if (SlideIndexChanged != null)
                SlideIndexChanged(this, new SlideShowEventArgs(curpos, newpos));
        }

        public event EventHandler<SlideShowEventArgs> SlideIndexChanged;

        public void AddSlides(Item scheduleItem)
        {
            if (!File.Exists(scheduleItem.Filename))
                return;

            if (Config.VideoFormats.Any(f => scheduleItem.Filename.ToLower().EndsWith("." + f)))
            {
                ((PP.Slide)running.Slides._Index(1)).Copy();
                PP.Slide slide = (PP.Slide)running.Slides.Paste(running.Slides.Count + 1)._Index(1);
                slide.Design = (PP.Design)running.Designs._Index(1);
                AddSlide(scheduleItem.Name, "", slide, SlideType.Video, scheduleItem.Filename);
            }
            else if (Config.AudioFormats.Any(f => scheduleItem.Filename.ToLower().EndsWith("." + f)))
            {
                ((PP.Slide)running.Slides._Index(1)).Copy();
                PP.Slide slide = (PP.Slide)running.Slides.Paste(running.Slides.Count + 1)._Index(1);
                slide.Design = (PP.Design)running.Designs._Index(1);
                AddSlide(scheduleItem.Name, "", slide, SlideType.Audio, scheduleItem.Filename);
            }
            else if (Config.ImageFormats.Any(f => scheduleItem.Filename.ToLower().EndsWith("." + f)))
            {
                PP.Slide slide = running.Slides.Add(running.Slides.Count + 1, PP.PpSlideLayout.ppLayoutBlank);
                PP.Shape shape;

                if (Util.Parse<double>(app.Version) >= 12)
                    shape = slide.Shapes.AddPicture(scheduleItem.Filename, Core.MsoTriState.msoTrue, Core.MsoTriState.msoFalse, -1f, -1f, -1f, -1f);
                else
                {
                    string path = UniqueFilename(Config.TempPath + (running.Slides.Count + 1) + "-background.jpg");
                    byte[] data = Util.ToByteArray(Util.Resize(Util.ToImage(File.ReadAllBytes(scheduleItem.Filename)), (int)slide.Design.SlideMaster.Width, (int)slide.Design.SlideMaster.Height, System.Drawing.Color.Black));
                    using (FileStream stream = File.Open(path, FileMode.Create))
                        data.ForEach(b => stream.WriteByte(b));
                    shape = slide.Shapes.AddPicture(path, Core.MsoTriState.msoTrue, Core.MsoTriState.msoFalse, -1f, -1f, -1f, -1f);
                }

                if (shape.Width < slide.Master.Width)
                    shape.Left = (slide.Master.Width - shape.Width) / 2;
                if (shape.Height < slide.Master.Height)
                    shape.Top = (slide.Master.Height - shape.Height) / 2;
                slide.Design = (PP.Design)running.Designs._Index(1);
                slide.FollowMasterBackground = Core.MsoTriState.msoTrue;
                slide.Comments.Add(0f, 0f, "", "", scheduleItem.Name);
                AddSlide(scheduleItem.Name, "", slide);
            }
            else
            {
                PP.Presentation pres;
                if (scheduleItem.Filename.ToLower().EndsWith(".pptx") && Util.Parse<double>(app.Version) < 12)
                {
                    int count = app.Presentations.Count;
                    System.Diagnostics.Process p = new System.Diagnostics.Process();
                    p.StartInfo.FileName = scheduleItem.Filename;
                    p.Start();
                    while (count == app.Presentations.Count) { }
                    pres = (PP.Presentation)app.Presentations._Index(app.Presentations.Count);
                }
                else
                    pres = app.Presentations.Open(scheduleItem.Filename, Core.MsoTriState.msoFalse, Core.MsoTriState.msoFalse, Core.MsoTriState.msoFalse);

                PasteSlidesWithFormatting(running, pres);
                pres.Close();
            }

            if (IsRunning && !running.FullName.ToLower().EndsWith(".pot"))
                running.Save();
        }

        public static void RemoveOldPres()
        {
            try
            {
                if (File.Exists(CPath))
                    File.Delete(CPath);
            }
            catch (Exception)
            {
                //if delete fails, use a new filename instead
                string ext = ".ppt" + (CPath.EndsWith("pptx") ? "x" : "");
                string name = "current";
                int incr = Util.Parse<int>(CPath.Substring(name.Length, CPath.Length - name.Length - ext.Length)) + 1;
                _cpath = name + incr + ext;
                RemoveOldPres(); //verify file does not exist
            }
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

        Dictionary<string, int> previousDesigns = new Dictionary<string, int>();
        private void PasteSlidesWithFormatting(PP.Presentation dest, PP.Presentation source)
        {
            Dictionary<PP.Design, int> designs = new Dictionary<PP.Design, int>();
            Dictionary<PP.ColorScheme, int> schemes = new Dictionary<PP.ColorScheme, int>();

            foreach (PP.Slide slide in source.Slides)
            {
                slide.Copy();
                PP.SlideRange range = dest.Slides.Paste(dest.Slides.Count + 1);

                //fix bugs
                for (int i = 1; i <= range.Shapes.Count; i++)
                {
                    if (range.Shapes[i].HasTextFrame != Core.MsoTriState.msoTrue)
                        continue;

                    //fix font sizes not being kept
                    //check that source font size is greater than 0 i.e. "The Grace (7pm only)" on Office XP
                    if (slide.Shapes[i].TextFrame.TextRange.Font.Size > 0)
                        range.Shapes[i].TextFrame.TextRange.Font.Size = slide.Shapes[i].TextFrame.TextRange.Font.Size;

                    //fix alignment on some files like "Alleluia Jesus Is Lord" on Office 2007
                    range.Shapes[i].TextFrame.TextRange.ParagraphFormat.Alignment = slide.Shapes[i].TextFrame.TextRange.ParagraphFormat.Alignment;
                }

                //slide master
                //reuse the same design otherwise, if we copy the design for every slide we end up with a very big file
                if (!designs.ContainsKey(slide.Design))
                {
                    range.Design = slide.Design;

                    //If presentation is listed twice, the Design is not copied across since it already exists (even though
                    //it doesn't when the same design is used in the same presentation) and therefore we need the index when
                    //it was first added not the current index. Can't persist the designs dictionary between presentations
                    //because even though the Master slide may be the same it's a different instation of the object.
                    string key = source.FullName + slide.Design.Index;
                    if (!previousDesigns.ContainsKey(key))
                    {
                        designs.Add(slide.Design, dest.Designs.Count);
                        previousDesigns.Add(key, dest.Designs.Count);
                    }
                    else
                        designs.Add(slide.Design, previousDesigns[key]);
                }
                else
                    range.Design = (PP.Design)dest.Designs._Index(designs[slide.Design]);

                //colour scheme
                if (!schemes.ContainsKey(slide.ColorScheme))
                {
                    range.ColorScheme = slide.ColorScheme;
                    schemes.Add(slide.ColorScheme, dest.ColorSchemes.Count);
                }
                else
                    range.ColorScheme = (PP.ColorScheme)dest.ColorSchemes._Index(schemes[slide.ColorScheme]);

                //place this code after assigning master slide because if mouse happens to be hovering over the
                //spot where this slide will appear a thumbnail image will be immediately generated and will look wrong
                AddSlide(GetStringSummary(range.Shapes), GetStringSummary(range.NotesPage.Shapes), dest.Slides._Index(dest.Slides.Count));
                
                if (slide.FollowMasterBackground == Core.MsoTriState.msoTrue)
                    continue;

                //background properties
                range.FollowMasterBackground = slide.FollowMasterBackground;
                range.Background.Fill.Visible = slide.Background.Fill.Visible;
                range.Background.Fill.ForeColor = slide.Background.Fill.ForeColor;
                range.Background.Fill.BackColor = slide.Background.Fill.BackColor;

                if (slide.Background.Fill.Type == Core.MsoFillType.msoFillBackground)
                {
                    //needs testing
                }
                else if (slide.Background.Fill.Type == Core.MsoFillType.msoFillGradient)
                {
                    if (slide.Background.Fill.GradientColorType == Core.MsoGradientColorType.msoGradientColorMixed)
                    {
                        //needs testing
                    }
                    else if (slide.Background.Fill.GradientColorType == Core.MsoGradientColorType.msoGradientOneColor)
                        range.Background.Fill.OneColorGradient(slide.Background.Fill.GradientStyle, slide.Background.Fill.GradientVariant, slide.Background.Fill.GradientDegree);
                    else if (slide.Background.Fill.GradientColorType == Core.MsoGradientColorType.msoGradientPresetColors)
                        range.Background.Fill.PresetGradient(slide.Background.Fill.GradientStyle, slide.Background.Fill.GradientVariant, slide.Background.Fill.PresetGradientType);
                    else if (slide.Background.Fill.GradientColorType == Core.MsoGradientColorType.msoGradientTwoColors)
                        range.Background.Fill.TwoColorGradient(slide.Background.Fill.GradientStyle, slide.Background.Fill.GradientVariant);
                }
                else if (slide.Background.Fill.Type == Core.MsoFillType.msoFillMixed)
                {
                    //needs testing
                }
                else if (slide.Background.Fill.Type == Core.MsoFillType.msoFillPatterned)
                {
                    range.Background.Fill.Patterned(slide.Background.Fill.Pattern);
                }
                else if (slide.Background.Fill.Type == Core.MsoFillType.msoFillPicture)
                {
                    CopySlideBackgroundAsImage(range, slide);
                }
                else if (slide.Background.Fill.Type == Core.MsoFillType.msoFillSolid)
                {
                    range.Background.Fill.Transparency = 0f;
                    range.Background.Fill.Solid();
                }
                else if (slide.Background.Fill.Type == Core.MsoFillType.msoFillTextured)
                {
                    if (slide.Background.Fill.TextureType == Core.MsoTextureType.msoTexturePreset)
                        range.Background.Fill.PresetTextured(slide.Background.Fill.PresetTexture);
                    else if (slide.Background.Fill.TextureType == Microsoft.Office.Core.MsoTextureType.msoTextureTypeMixed)
                        CopySlideBackgroundAsImage(range, slide);
                    else if (slide.Background.Fill.TextureType == Core.MsoTextureType.msoTextureUserDefined)
                        CopySlideBackgroundAsImage(range, slide);
                }
            }
        }

        private void CopySlideBackgroundAsImage(PP.SlideRange dest, PP.Slide source)
        {
            //background images seem to be copied over in Office 2007 so no need to use this method
            if (Util.Parse<double>(app.Version) >= 12)
                return;

            //hide shapes so only background is showing before taking snapshot
            var state = new Dictionary<PP.Shape, Core.MsoTriState>();
            foreach (PP.Shape shape in source.Shapes)
            {
                state.Add(shape, shape.Visible);
                shape.Visible = Core.MsoTriState.msoFalse;
            }

            Core.MsoTriState masterState = source.DisplayMasterShapes;
            source.DisplayMasterShapes = Core.MsoTriState.msoFalse;

            //export slide as image to be used as background
            string path = ExportToImage(source, _slides.Count + 1, "-background", -1, -1);
            dest.Background.Fill.UserPicture(path);

            //show shapes again
            foreach (PP.Shape shape in source.Shapes)
                shape.Visible = state[shape];
            source.DisplayMasterShapes = masterState;
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
            if (slide == null)
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
        public SlideShowEventArgs(int oldIdx, int newIdx)
            : base()
        {
            OldIndex = oldIdx;
            NewIndex = newIdx;
        }

        public int OldIndex { get; private set; }
        public int NewIndex { get; private set; }
    }

    public class SlideAddedEventArgs : EventArgs
    {
        public SlideAddedEventArgs(Slide slide)
            : base()
        {
            NewSlide = slide;
        }

        public Slide NewSlide { get; private set; }
    }
}
