using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Presenter.App_Code;

namespace Presenter
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
            if (this.Owner.TaskbarItemInfo != null)
                this.Owner.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
        }

        protected void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancelled = true;
            Close();
        }

        public void UpdateProgress(double progress)
        {
            if (this.Owner.TaskbarItemInfo == null)
                this.Owner.TaskbarItemInfo = new TaskbarItemInfo() { ProgressState = TaskbarItemProgressState.Normal };
            this.Owner.TaskbarItemInfo.ProgressValue = progress;
            Dispatcher.BeginInvoke(new Action(() => { Progress.SetValue(ProgressBar.ValueProperty, progress); }), DispatcherPriority.Background, null);
        }

        public bool Cancelled { get; private set; }
    }
}
