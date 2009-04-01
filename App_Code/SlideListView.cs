using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using SongPresenter.App_Code;
using System.Windows.Threading;

namespace SongPresenter
{
    public class SlideListView : ListView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new SlideListViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is SlideListViewItem;
        }
    }

    public class SlideListViewItem : ListViewItem
    {
        public static readonly DependencyProperty IsPopupOpenProperty = DependencyProperty.Register("IsPopupOpen", typeof(bool), typeof(SlideListViewItem), new FrameworkPropertyMetadata(IsPopupOpenChanged));

        private Popup popup;
        private Image slidePreview;
        private bool _delayopen;
        DispatcherTimer timer = new DispatcherTimer();

        public SlideListViewItem()
        {
            this.slidePreview = new Image();
            Grid grid = new Grid();
            grid.Children.Add(this.slidePreview);
            this.popup = new Popup() { Child = grid, PlacementTarget = this, Placement = PlacementMode.Mouse, Width = Config.ThumbWidth, Height = Config.ThumbHeight };
            this.timer.Tick += new EventHandler(timer_Tick);
            this.timer.Tag = this;
        }

        public bool IsPopupOpen
        {
            get { return (bool)GetValue(IsPopupOpenProperty); }
            set { SetValue(IsPopupOpenProperty, value); }
        }

        private static void IsPopupOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SlideListViewItem item = d as SlideListViewItem;
            item._delayopen = (bool)e.NewValue;

            if (item != null)
            {
                if (item._delayopen)
                {
                    item.timer.Interval = new TimeSpan(0, 0, 0, 0, (int)(Config.SlidePreviewPopupDelay * 1000)); //placed here to pick up new config value if changed
                    item.timer.Start();
                }
                else
                    item.popup.IsOpen = false;

                if (item._delayopen && item.slidePreview.Source == null)
                {
                    string path = (item.DataContext as Slide).Preview;
                    if (System.IO.File.Exists(path))
                        item.slidePreview.Source = new BitmapImage(new Uri(path));
                }
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            SlideListViewItem item = (sender as DispatcherTimer).Tag as SlideListViewItem;
            if (item._delayopen)
                item.popup.IsOpen = true;
        }
    }
}
