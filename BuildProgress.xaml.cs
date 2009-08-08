using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
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

        protected void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancelled = true;
            CancelButton.IsEnabled = false;
        }

        public void UpdateProgress(double progress)
        {
            Dispatcher.BeginInvoke(new Action(() => { Progress.SetValue(ProgressBar.ValueProperty, progress); }), DispatcherPriority.Background, null);
        }
        
        public void Finish()
        {
            Dispatcher.BeginInvoke(new Action(() => { Close(); }), DispatcherPriority.Background, null);
        }

        public bool Cancelled { get; private set; }
    }
}
