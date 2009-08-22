using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Taskbar;
using SongPresenter.App_Code;

namespace SongPresenter
{
    public partial class BuildProgress : Window
    {
        public BuildProgress()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);
        }

        protected void BuildProgress_Closing(object sender, CancelEventArgs e)
        {
            if (TaskbarManager.IsPlatformSupported)
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
        }

        protected void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancelled = true;
            Close();
        }

        public void UpdateProgress(double progress)
        {
            if (TaskbarManager.IsPlatformSupported)
                TaskbarManager.Instance.SetProgressValue((int)(progress * 100), 100);
            Dispatcher.BeginInvoke(new Action(() => { Progress.SetValue(ProgressBar.ValueProperty, progress); }), DispatcherPriority.Background, null);
        }

        public bool Cancelled { get; private set; }
    }
}
