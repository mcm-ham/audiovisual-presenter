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
