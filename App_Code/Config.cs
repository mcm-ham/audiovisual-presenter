using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;

namespace Presenter.App_Code
{
    public class Config : DependencyObject
    {
        public static readonly Config instance = new Config();

        private static Collection<string> _allformats;
        public static Collection<string> SupportedFileTypes
        {
            get
            {
                if (_allformats == null)
                    _allformats = new Collection<string>(PowerPointFormats.Union(PowerPointTemplates).Union(ImageFormats).Union(VideoFormats).Union(AudioFormats).ToArray());
                return _allformats;
            }
        }

        private static Collection<string> _powerpoint;
        public static Collection<string> PowerPointFormats
        {
            get
            {
                if (_powerpoint == null)
                    _powerpoint = new Collection<string>(ConfigurationManager.AppSettings["PowerPointFormats"].Split(','));
                return _powerpoint;
            }
        }

        private static Collection<string> _templates;
        public static Collection<string> PowerPointTemplates
        {
            get
            {
                if (_templates == null)
                    _templates = new Collection<string>(ConfigurationManager.AppSettings["PowerPointTemplates"].Split(','));
                return _templates;
            }
        }

        private static Collection<string> _image;
        public static Collection<string> ImageFormats
        {
            get
            {
                if (_image == null)
                    _image = new Collection<string>(ConfigurationManager.AppSettings["ImageFormats"].Split(','));
                return _image;
            }
        }

        //http://support.microsoft.com/kb/316992
        private static Collection<string> _video;
        public static Collection<string> VideoFormats
        {
            get
            {
                if (_video == null)
                    _video = new Collection<string>(ConfigurationManager.AppSettings["VideoFormats"].Split(','));
                return _video;
            }
        }

        private static Collection<string> _audio;
        public static Collection<string> AudioFormats
        {
            get
            {
                if (_audio == null)
                    _audio = new Collection<string>(ConfigurationManager.AppSettings["AudioFormats"].Split(','));
                return _audio;
            }
        }

        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSizeProperty", typeof(double), typeof(Config));
        public static double FontSize
        {
            get { return (double)instance.GetValue(FontSizeProperty); }
            set { instance.SetValue(FontSizeProperty, value); }
        }

        private static string _path;
        /// <summary>
        /// Path to the directory of the library, ends in '\'
        /// </summary>
        public static string LibraryPath
        {
            get
            {
                if (_path == null)
                {
                    try
                    {
                        string res = ConfigurationManager.AppSettings["LibraryPath"];
                        if (!Path.IsPathRooted(res))
                        {
                            string[] parts = res.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            Environment.SpecialFolder? folder = Util.Parse<Environment.SpecialFolder?>((parts.FirstOrDefault() ?? "").Replace(" ", ""));
                            if (folder.HasValue && (folder.Value == Environment.SpecialFolder.MyDocuments || folder.Value == Environment.SpecialFolder.MyMusic || folder.Value == Environment.SpecialFolder.MyPictures))
                                res = Environment.GetFolderPath(folder.Value) + "\\" + String.Join("\\", parts.Skip(1).ToArray());
                            else
                                res = Path.GetFullPath(res);
                        }
                        _path = res.TrimEnd('\\') + "\\";
                    }
                    catch (Exception)
                    {
                        _path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Library\\";
                    }

                    if (!Directory.Exists(_path))
                        Directory.CreateDirectory(_path);
                }
                return _path;
            }
            set
            {
                _path = null;
                SaveSetting("LibraryPath", value);
            }
        }

        private static System.Windows.Forms.Screen _screen;
        public static System.Windows.Forms.Screen ProjectorScreen
        {
            get
            {
                if (_screen == null)
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + PowerpointVersion + @"\PowerPoint\Options", false);
                    var keyname = key == null ? "" : key.GetValue("DisplayMonitor") as string ?? "";
                    if (!String.IsNullOrEmpty(keyname))
                        _screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName.StartsWith(keyname));
                    
                    if (_screen == null)
                    {
                        if (System.Windows.Forms.Screen.AllScreens.Length == 2)
                            _screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary);
                        else
                            _screen = System.Windows.Forms.Screen.PrimaryScreen;
                    }
                }
                return _screen;
            }
            set
            {
                _screen = value;
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + PowerpointVersion + @"\PowerPoint\Options", true);
                if (key == null)
                    key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Office\" + PowerpointVersion + @"\PowerPoint\Options");
                key.SetValue("DisplayMonitor", value.DeviceName);
            }
        }

        public static string PowerpointVersion
        {
            get { return new Microsoft.Office.Interop.PowerPoint.Application().Version; }
        }
        
        public static Color BackgroundColour
        {
            get
            {
                try { return (Color)ColorConverter.ConvertFromString(ConfigurationManager.AppSettings["AppColour"]); }
                catch (Exception) { return Colors.White; }
            }
        }

        private static void SaveSetting(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[key] != null)
                config.AppSettings.Settings[key].Value = value;
            else
                config.AppSettings.Settings.Add(key, value);
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public static string TempPath
        {
            get
            {
                string temp = Path.GetTempPath() + "presenter\\";
                if (!Directory.Exists(temp))
                    Directory.CreateDirectory(temp);
                return temp;
            }
        }

        public static bool KeepPresentations
        {
            get { return Util.Parse<bool>(ConfigurationManager.AppSettings["KeepPresentations"]); }
        }

        public static string PresentationPath
        {
            get
            {
                string path = ConfigurationManager.AppSettings["PresentationPath"] ?? "Library\\Presentations\\";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return Path.GetFullPath(path);
            }
        }

        public static Color MessengerFontColour
        {
            get
            {
                try { return (Color)ColorConverter.ConvertFromString(MessengerFontColourName); }
                catch (Exception) { return Colors.White; }
            }
        }

        public static string MessengerFontColourName
        {
            get
            {
                try { return ConfigurationManager.AppSettings["MessengerFont"].Split(' ').Last(); }
                catch (Exception) { return "White"; }
            }
        }

        public static double MessengerFontSize
        {
            get { return Util.Parse<double?>((ConfigurationManager.AppSettings["MessengerFont"] ?? "").Split(' ')[0]) ?? 46; }
        }

        public static void SaveMessengerFont(double size, FontFamily font, string colorName)
        {
            SaveSetting("MessengerFont", size + " " + font.Source + " " + colorName);
        }

        public static FontFamily MessengerFontFamily
        {
            get
            {
                try
                {
                    string name = ConfigurationManager.AppSettings["MessengerFont"];
                    name = name.Substring(name.IndexOf(' ') + 1, name.LastIndexOf(' ') - name.IndexOf(' ') - 1);
                    return new FontFamily(name);
                }
                catch (Exception) { return new FontFamily("Arial"); }
            }
        }

        public static VerticalAlignment MessengerVerticalPosition
        {
            get { return Util.Parse<VerticalAlignment?>((ConfigurationManager.AppSettings["MessengerPosition"] ?? "").Split(' ').First().ToFirstUpper()) ?? VerticalAlignment.Bottom; }
        }

        public static HorizontalAlignment MessengerHorizontalPosition
        {
            get { return Util.Parse<HorizontalAlignment?>((ConfigurationManager.AppSettings["MessengerPosition"] ?? "").Split(' ').Last().ToFirstUpper()) ?? HorizontalAlignment.Left; }
        }

        public static Thickness MessengerMargin
        {
            get { return Util.Parse<Thickness?>(ConfigurationManager.AppSettings["MessengerMargin"]) ?? new Thickness(); }
        }

        public static void SaveMessengerLocation(VerticalAlignment posy, HorizontalAlignment posx)
        {
            SaveSetting("MessengerPosition", posy + " " + posx);
        }

        public static int TimerInterval
        {
            get { return Util.Parse<int?>(ConfigurationManager.AppSettings["TimerInterval"]) ?? 8; }
            set { SaveSetting("TimerInterval", value.ToString()); }
        }

        public static string SelectedLibrary
        {
            get { return ConfigurationManager.AppSettings["SelectedLibrary"]; }
            set { SaveSetting("SelectedLibrary", value); }
        }

        public static bool InsertBlankSlides
        {
            get { return Util.Parse<bool?>(ConfigurationManager.AppSettings["InsertBlankSlides"]) ?? true; }
            set { SaveSetting("InsertBlankSlides", value.ToString()); }
        }
    }
}
