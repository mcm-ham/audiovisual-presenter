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
using Microsoft.Samples.DateControls;
using SongPresenter.App_Code;
using SongPresenter.Resources;

namespace SongPresenter
{
    public partial class OpenDialog : Window
    {
        private DateTime _mth;

        public OpenDialog(bool newFocus)
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);
            
            monthCalendar.SelectedDate = DateTime.Today;
            DatePreview.Text = DateTime.Today.ToLongDateString();
            _mth = DateTime.Today;
            BindScheduleList();

            if (newFocus)
                ScheduleName.Focus();
            else if (ScheduleList.Items.Count > 0)
            {
                ScheduleList.SelectedIndex = 0;
                ScheduleList.Focus();
            }
        }

        protected void BindScheduleList()
        {
            ScheduleList.ItemsSource = Schedule.LoadSchedules(_mth);
        }

        protected void monthCalendar_VisibleMonthChanged(object sender, RoutedPropertyChangedEventArgs<DateTime> e)
        {
            _mth = e.NewValue;
            BindScheduleList();
        }

        protected void monthCalendar_DateSelectionChanged(object sender, DateSelectionChangedEventArgs e)
        {
            DatePreview.Text = monthCalendar.SelectedDate.HasValue ? monthCalendar.SelectedDate.Value.ToLongDateString() : "";
        }

        protected void ScheduleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeleteBtn.Visibility = (ScheduleList.SelectedItem == null) ? Visibility.Hidden : Visibility.Visible;
        }

        protected void ScheduleName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                New_Click(null, null);
        }

        protected void New_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleName.Text == "")
            {
                MessageBox.Show(Labels.OpenMissingDesc, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!monthCalendar.SelectedDate.HasValue)
            {
                MessageBox.Show(Labels.OpenMissingDate, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Schedule schedule = new Schedule() {
                Name = ScheduleName.Text,
                Date = monthCalendar.SelectedDate.Value
            };
            schedule.Save();

            ScheduleName.Text = "";
            BindScheduleList();
            ScheduleList.SelectedValue = schedule;
            Open_Click(null, null);
        }

        protected void Delete_Click(object sender, RoutedEventArgs e)
        {
            Schedule schedule = ScheduleList.SelectedItem as Schedule;
            MessageBoxResult result = MessageBox.Show(String.Format(Labels.OpenDelConfirm, schedule.DisplayName), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Schedule.DeleteSchedule(schedule.ID);
                BindScheduleList();
            }
        }

        protected void ScheduleList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Open_Click(null, null);
        }

        protected void Open_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedItem == null)
            {
                MessageBox.Show(Labels.OpenItemNotSelected, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedSchedule = ScheduleList.SelectedItem as Schedule;
            SelectedSchedule.Items.Load();
            this.Close();
        }

        //properties
        public Schedule SelectedSchedule { get; set; }
    }
}
