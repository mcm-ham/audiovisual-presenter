using Presenter.App_Code;
using Presenter.Resources;
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

namespace Presenter
{
    public partial class OpenDialog : Window
    {
        private DateTime _mth;

        public OpenDialog()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            monthCalendar.SelectedDate = DateTime.Today;
            DatePreview.Text = DateTime.Today.ToLongDateString();
            _mth = DateTime.Today;
            BindScheduleList();

            ScheduleList.SelectedIndex = 0;
            ScheduleList.Focus();
        }

        protected void BindScheduleList()
        {
            ScheduleList.ItemsSource = Schedule.LoadSchedules(_mth);
        }

        private void monthCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            DatePreview.Text = monthCalendar.SelectedDate.HasValue ? monthCalendar.SelectedDate.Value.ToLongDateString() : "";
            monthCalendar.DisplayDateChanged += new EventHandler<CalendarDateChangedEventArgs>(monthCalendar_DisplayDateChanged);
        }

        private void monthCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            _mth = e.AddedDate ?? DateTime.Now;
            BindScheduleList();
        }

        protected void ScheduleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeleteBtn.Visibility = RenameBtn.Visibility = (ScheduleList.SelectedItem == null) ? Visibility.Hidden : Visibility.Visible;
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

            Schedule schedule = new Schedule()
            {
                Name = ScheduleName.Text,
                Date = monthCalendar.SelectedDate.Value
            };
            schedule.Save();

            ScheduleName.Text = "";
            BindScheduleList();

            //note since ADO.NET Entities is being used the object instance below is the same as what
            //BindScheduleList retrieved therefore the line below works
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
                if (ScheduleDeleted != null)
                    ScheduleDeleted(this, new DeletedScheduleArgs(schedule.ID));
            }
        }

        public event EventHandler<DeletedScheduleArgs> ScheduleDeleted;

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

        protected void Rename_Click(object sender, RoutedEventArgs e)
        {
            var listBoxItem = (ListBoxItem)ScheduleList.ItemContainerGenerator.ContainerFromItem(ScheduleList.SelectedItem);
            var presenter = FindVisualChild<ContentPresenter>(listBoxItem);
            var template = (DataTemplate)listBoxItem.ContentTemplate;

            var scheduleItemTextBox = (TextBox)template.FindName("ScheduleItemTextBox", presenter);
            var scheduleItemLabel = (TextBlock)template.FindName("ScheduleItemLabel", presenter);
            scheduleItemTextBox.Visibility = System.Windows.Visibility.Visible;
            scheduleItemTextBox.Focus();
            scheduleItemLabel.Visibility = System.Windows.Visibility.Collapsed;
        }

        private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        private void ScheduleItemTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            var scheduleItemTextBox = (TextBox)sender;
            var scheduleItemLabel = ((StackPanel)VisualTreeHelper.GetParent(scheduleItemTextBox)).Children.OfType<TextBlock>().First();

            if (e.Key == Key.Escape)
            {
                scheduleItemLabel.Visibility = System.Windows.Visibility.Visible;
                scheduleItemTextBox.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (e.Key == Key.Enter)
            {
                var schedule = (Schedule)ScheduleList.SelectedItem;
                schedule.Name = scheduleItemTextBox.Text;
                schedule.Save();
                scheduleItemLabel.Text = schedule.DisplayName;

                scheduleItemLabel.Visibility = System.Windows.Visibility.Visible;
                scheduleItemTextBox.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void ScheduleItemTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var scheduleItemTextBox = (TextBox)sender;
            var scheduleItemLabel = ((StackPanel)VisualTreeHelper.GetParent(scheduleItemTextBox)).Children.OfType<TextBlock>().First();
            scheduleItemLabel.Visibility = System.Windows.Visibility.Visible;
            scheduleItemTextBox.Visibility = System.Windows.Visibility.Collapsed;
        }

        //properties
        public Schedule SelectedSchedule { get; set; }

        //classes
        public class DeletedScheduleArgs : EventArgs
        {
            public DeletedScheduleArgs(Guid scheduleId)
                : base()
            {
                DeletedScheduleID = scheduleId;
            }

            public Guid DeletedScheduleID { get; set; }
        }
    }
}
