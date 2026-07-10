using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Presenter.App_Code;
using Presenter.Core.Models;
using Presenter.Resources;

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
            Opened += (s, e) => ScheduleList.Focus();
        }

        protected void BindScheduleList()
        {
            ScheduleList.ItemsSource = AppServices.Repository.LoadSchedules(_mth);
        }

        private void monthCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            DatePreview.Text = monthCalendar.SelectedDate.HasValue ? monthCalendar.SelectedDate.Value.ToLongDateString() : "";
        }

        private void monthCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            _mth = e.AddedDate ?? DateTime.Now;
            BindScheduleList();
        }

        protected void ScheduleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeleteBtn.IsVisible = RenameBtn.IsVisible = (ScheduleList.SelectedItem != null);
        }

        protected void ScheduleName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                New_Click(null, null);
        }

        protected async void New_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ScheduleName.Text))
            {
                await App_Code.MessageBox.Show(this, Labels.OpenMissingDesc);
                return;
            }

            if (!monthCalendar.SelectedDate.HasValue)
            {
                await App_Code.MessageBox.Show(this, Labels.OpenMissingDate);
                return;
            }

            Schedule schedule = new Schedule()
            {
                Name = ScheduleName.Text,
                Date = monthCalendar.SelectedDate.Value
            };
            AppServices.Repository.Save(schedule);

            ScheduleName.Text = "";
            BindScheduleList();

            //EF Core identity map returns the same tracked instance, so selecting by
            //object reference still works (as with the original ADO.NET Entities note)
            ScheduleList.SelectedItem = schedule;

            Open_Click(null, null);
        }

        protected async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Schedule schedule = ScheduleList.SelectedItem as Schedule;
            MessageBoxResult result = await App_Code.MessageBox.Show(this, String.Format(Labels.OpenDelConfirm, schedule.DisplayName), "", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                AppServices.Repository.DeleteSchedule(schedule.ID);
                BindScheduleList();
                if (ScheduleDeleted != null)
                    ScheduleDeleted(this, new DeletedScheduleArgs(schedule.ID));
            }
        }

        public event EventHandler<DeletedScheduleArgs> ScheduleDeleted;

        protected void ScheduleList_DoubleTapped(object sender, TappedEventArgs e)
        {
            Open_Click(null, null);
        }

        protected async void Open_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedItem == null)
            {
                await App_Code.MessageBox.Show(this, Labels.OpenItemNotSelected);
                return;
            }

            //reload with items and flags eagerly included (replaces lazy Items.Load())
            SelectedSchedule = AppServices.Repository.LoadSchedule((ScheduleList.SelectedItem as Schedule).ID);
            this.Close();
        }

        protected void Rename_Click(object sender, RoutedEventArgs e)
        {
            var container = ScheduleList.ContainerFromItem(ScheduleList.SelectedItem);
            if (container == null)
                return;

            var scheduleItemTextBox = container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            var scheduleItemLabel = container.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            if (scheduleItemTextBox == null || scheduleItemLabel == null)
                return;

            scheduleItemTextBox.IsVisible = true;
            scheduleItemTextBox.Focus();
            scheduleItemLabel.IsVisible = false;
        }

        private void ScheduleItemTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            var scheduleItemTextBox = (TextBox)sender;
            var scheduleItemLabel = ((StackPanel)scheduleItemTextBox.GetVisualParent()).Children.OfType<TextBlock>().First();

            if (e.Key == Key.Escape)
            {
                scheduleItemLabel.IsVisible = true;
                scheduleItemTextBox.IsVisible = false;
            }

            if (e.Key == Key.Enter)
            {
                var schedule = (Schedule)ScheduleList.SelectedItem;
                schedule.Name = scheduleItemTextBox.Text;
                AppServices.Repository.Save(schedule);
                scheduleItemLabel.Text = schedule.DisplayName;

                scheduleItemLabel.IsVisible = true;
                scheduleItemTextBox.IsVisible = false;
            }
        }

        private void ScheduleItemTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var scheduleItemTextBox = (TextBox)sender;
            var scheduleItemLabel = ((StackPanel)scheduleItemTextBox.GetVisualParent()).Children.OfType<TextBlock>().First();
            scheduleItemLabel.IsVisible = true;
            scheduleItemTextBox.IsVisible = false;
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
