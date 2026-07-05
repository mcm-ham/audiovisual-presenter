using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Presenter.Core;
using Presenter.Core.Services;
using Presenter.Core.Settings;

namespace Presenter.App_Code
{
    /// <summary>
    /// Static configuration facade preserving the API of the original App_Code/Config.cs
    /// (so XAML bindings and code-behind port unchanged), now backed by the JSON
    /// settings store instead of app.config.
    /// </summary>
    public class Config : DependencyObject
    {
        public static readonly Config instance = new Config();

        private static PresenterSettings S => AppServices.SettingsStore.Current;
        private static void Save() => AppServices.SettingsStore.Save();

        public static Collection<string> SupportedFileTypes => new(S.SupportedFileTypes.ToArray());
        public static Collection<string> PowerPointFormats => new(S.PowerPointFormats);
        public static Collection<string> PowerPointTemplates => new(S.PowerPointTemplates);
        public static Collection<string> ImageFormats => new(S.ImageFormats);
        public static Collection<string> VideoFormats => new(S.VideoFormats);
        public static Collection<string> AudioFormats => new(S.AudioFormats);

        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSizeProperty", typeof(double), typeof(Config));
        public static double FontSize
        {
            get { return (double)instance.GetValue(FontSizeProperty); }
            set
            {
                instance.SetValue(FontSizeProperty, value);
                S.FontSize = value;
                Save();
            }
        }

        private static string _path;
        /// <summary>Path to the directory of the library, ends in '\'</summary>
        public static string LibraryPath
        {
            get { return _path ??= LibraryPathResolver.Resolve(S); }
            set
            {
                _path = null;
                S.LibraryPath = value;
                Save();
            }
        }

        /// <summary>The raw configured library path (as shown in the options dialog).</summary>
        public static string LibraryPathSetting => S.LibraryPath;

        public static System.Windows.Forms.Screen PrimaryScreen => System.Windows.Forms.Screen.PrimaryScreen;

        private static System.Windows.Forms.Screen _screen;
        public static System.Windows.Forms.Screen ProjectorScreen
        {
            get
            {
                if (_screen == null)
                {
                    if (!string.IsNullOrEmpty(S.ProjectorScreenDevice))
                        _screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName.StartsWith(S.ProjectorScreenDevice));

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
                S.ProjectorScreenDevice = value?.DeviceName;
                Save();
            }
        }

        public static bool UseNonPrimaryScreen
        {
            get { return S.UseNonPrimaryScreen; }
            set { S.UseNonPrimaryScreen = value; Save(); }
        }

        public static Color BackgroundColour
        {
            get
            {
                try { return (Color)ColorConverter.ConvertFromString(S.AppColour); }
                catch { return Colors.White; }
            }
        }

        public static Color ScreenBlankColour
        {
            get
            {
                try { return (Color)ColorConverter.ConvertFromString(S.ScreenBlankColour); }
                catch { return Colors.Black; }
            }
        }

        public static string TempPath
        {
            get
            {
                string temp = Path.Combine(Path.GetTempPath(), "presenter") + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(temp);
                return temp;
            }
        }

        public static Color MessengerFontColour
        {
            get
            {
                try { return (Color)ColorConverter.ConvertFromString(S.MessengerFontColour); }
                catch { return Colors.White; }
            }
        }

        public static string MessengerFontColourName => S.MessengerFontColour;

        public static double MessengerFontSize => S.MessengerFontSize;

        public static void SaveMessengerFont(double size, FontFamily font, string colorName)
        {
            S.MessengerFontSize = size;
            S.MessengerFontFamily = font.Source;
            S.MessengerFontColour = colorName;
            Save();
        }

        public static FontFamily MessengerFontFamily
        {
            get
            {
                try { return new FontFamily(S.MessengerFontFamily); }
                catch { return new FontFamily("Arial"); }
            }
        }

        public static VerticalAlignment MessengerVerticalPosition =>
            S.MessengerVertical switch
            {
                Core.Settings.MessengerVerticalPosition.Top => VerticalAlignment.Top,
                Core.Settings.MessengerVerticalPosition.Center => VerticalAlignment.Center,
                _ => VerticalAlignment.Bottom,
            };

        public static HorizontalAlignment MessengerHorizontalPosition =>
            S.MessengerHorizontal switch
            {
                Core.Settings.MessengerHorizontalPosition.Right => HorizontalAlignment.Right,
                Core.Settings.MessengerHorizontalPosition.Center => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Left,
            };

        public static Thickness MessengerMargin => Util.Parse<Thickness?>(S.MessengerMargin) ?? new Thickness();

        public static void SaveMessengerLocation(VerticalAlignment posy, HorizontalAlignment posx)
        {
            S.MessengerVertical = posy switch
            {
                VerticalAlignment.Top => Core.Settings.MessengerVerticalPosition.Top,
                VerticalAlignment.Center => Core.Settings.MessengerVerticalPosition.Center,
                _ => Core.Settings.MessengerVerticalPosition.Bottom,
            };
            S.MessengerHorizontal = posx switch
            {
                HorizontalAlignment.Right => Core.Settings.MessengerHorizontalPosition.Right,
                HorizontalAlignment.Center => Core.Settings.MessengerHorizontalPosition.Center,
                _ => Core.Settings.MessengerHorizontalPosition.Left,
            };
            Save();
        }

        public static int TimerInterval
        {
            get { return S.TimerInterval; }
            set { S.TimerInterval = value; Save(); }
        }

        public static string SelectedLibrary
        {
            get { return S.SelectedLibrary; }
            set { S.SelectedLibrary = value; Save(); }
        }

        public static bool InsertBlankAfterPres
        {
            get { return S.InsertBlankAfterPres; }
            set { S.InsertBlankAfterPres = value; Save(); }
        }

        public static bool InsertBlankAfterVideo
        {
            get { return S.InsertBlankAfterVideo; }
            set { S.InsertBlankAfterVideo = value; Save(); }
        }

        /// <summary>Preferred presentation engine: "com" (drive PowerPoint) or "uno" (LibreOffice).</summary>
        public static string PreferredEngine
        {
            get { return S.PreferredEngine; }
            set { S.PreferredEngine = value; Save(); }
        }

        public static bool UseSlideTimings
        {
            get { return S.UseSlideTimings; }
            set { S.UseSlideTimings = value; Save(); }
        }

        /// <summary>
        /// Specifies the location of the slide preview panel: true = bottom, false = right.
        /// Default depends on the primary screen aspect ratio (as in the original).
        /// </summary>
        public static bool SlidePreviewBottom
        {
            get
            {
                return S.SlidePreviewBottom ??
                    (System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / (double)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height < 1.5);
            }
            set
            {
                S.SlidePreviewBottom = value;
                Save();
                instance.SlidePreviewBottomChanged?.Invoke(instance, EventArgs.Empty);
            }
        }

        public event EventHandler SlidePreviewBottomChanged;
    }
}
