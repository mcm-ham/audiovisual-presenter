using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;

namespace SongPresenter.App_Code
{
    public class Config : DependencyObject
    {
        public static readonly Config instance = new Config();

        public static string[] SupportedFileTypes
        {
            get { return new string[] { "ppt", "pptx", "pps", "ppsx" }.Union(ImageFormats).Union(VideoFormats).Union(AudioFormats).ToArray(); }
        }

        public static string[] ImageFormats
        {
            get { return (ConfigurationManager.AppSettings["ImageFormats"] ?? "jpg").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim().ToLower()).ToArray(); }
        }

        public static string[] VideoFormats
        {
            get { return (ConfigurationManager.AppSettings["VideoFormats"] ?? "wmv,mov,avi,mpg").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim().ToLower()).ToArray(); }
        }

        public static string[] AudioFormats
        {
            get { return (ConfigurationManager.AppSettings["AudioFormats"] ?? "wma,wav,mp3").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim().ToLower()).ToArray(); }
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
                        var r = new DirectoryInfo(ConfigurationManager.AppSettings["LibraryPath"]).FullName;
                        _path = r.EndsWith("\\") ? r : r + "\\";
                    }
                    catch (Exception)
                    {
                        _path = "Library\\";
                    }
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
                Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + PowerpointVersion + @"\PowerPoint\Options", true).SetValue("DisplayMonitor", value.DeviceName);
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
    }
}
