using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Presenter.App_Code
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
            get { return ConfigurationManager.AppSettings["ImageFormats"].Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray(); }
        }

        public static string[] VideoFormats
        {
            get { return ConfigurationManager.AppSettings["VideoFormats"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray(); }
        }

        public static string[] AudioFormats
        {
            get { return ConfigurationManager.AppSettings["AudioFormats"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray(); }
        }

        private static string _path;
        public static string LibraryPath
        {
            get
            {
                if (_path == null)
                {
                    var r = new DirectoryInfo(ConfigurationManager.AppSettings["LibraryPath"]).FullName;
                    _path = r.EndsWith("\\") ? r : r + "\\";
                }
                return _path;
            }
            set
            {
                _path = null;
                SaveSetting("LibraryPath", value);
            }
        }

        public static int ThumbWidth
        {
            get { return Util.Parse<int>(ConfigurationManager.AppSettings["ThumbWidth"]); }
            set { SaveSetting("ThumbWidth", value.ToString()); }
        }

        public static int ThumbHeight
        {
            get { return Util.Parse<int>(ConfigurationManager.AppSettings["ThumbHeight"]); }
            set { SaveSetting("ThumbHeight", value.ToString()); }
        }

        /// <summary>
        /// The delay in seconds before the slide preview popup should show
        /// </summary>
        public static double SlidePreviewPopupDelay
        {
            get { return Util.Parse<double>(ConfigurationManager.AppSettings["SlidePreviewPopupDelay"]); }
            set { SaveSetting("SlidePreviewPopupDelay", value.ToString()); }
        }

        public static string ProjectorScreen
        {
            get { return Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + PowerpointVersion + @"\PowerPoint\Options", false).GetValue("DisplayMonitor") as string ?? System.Windows.Forms.Screen.PrimaryScreen.DeviceName; }
            set { Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + PowerpointVersion + @"\PowerPoint\Options", true).SetValue("DisplayMonitor", value); }
        }

        public static string PowerpointVersion
        {
            get { return new Microsoft.Office.Interop.PowerPoint.Application().Version; }
        }
        
        public static Color BackgroundColour
        {
            get { return (Color)ColorConverter.ConvertFromString(ConfigurationManager.AppSettings["AppColour"]); }
        }

        private static void SaveSetting(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = value;
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
    }
}
