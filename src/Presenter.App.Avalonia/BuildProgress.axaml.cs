using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Presenter.App_Code;

namespace Presenter
{
    public partial class BuildProgress : Window
    {
        public BuildProgress()
        {
            InitializeComponent();
            LayoutUpdated += (s, e) => System.IO.File.AppendAllText("/tmp/presenter-layout-diag.log",
                $"{System.DateTime.Now:HH:mm:ss.fff} BP w={Bounds.Width} h={Bounds.Height}\n");
            Background = new SolidColorBrush(Config.BackgroundColour);
            //the WPF version also mirrored progress to the Windows taskbar
            //(TaskbarItemInfo); there is no cross-platform equivalent in Avalonia
        }

        protected void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancelled = true;
            Close();
        }

        public void UpdateProgress(double progress)
        {
            Dispatcher.UIThread.Post(() => { Progress.Value = progress; }, DispatcherPriority.Background);
        }

        public bool Cancelled { get; private set; }
    }
}
