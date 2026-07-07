using Avalonia.Controls;
using Avalonia.Layout;
using Presenter.Resources;

namespace Presenter.App_Code
{
    public enum MessageBoxButton { OK, YesNo }
    public enum MessageBoxResult { None, OK, Yes, No }

    /// <summary>
    /// Minimal WPF-MessageBox replacement (Avalonia has no built-in message box).
    /// Dialogs are async in Avalonia, so callers await instead of blocking.
    /// </summary>
    public static class MessageBox
    {
        public static async Task<MessageBoxResult> Show(Window owner, string text, string title = "",
            MessageBoxButton buttons = MessageBoxButton.OK)
        {
            var result = MessageBoxResult.None;

            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                CanResize = false,
                ShowInTaskbar = false,
                MaxWidth = 500,
                FontSize = Config.FontSize,
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
            };

            void AddButton(string caption, MessageBoxResult value, bool isDefault)
            {
                var btn = new Button { Content = caption, MinWidth = 75, IsDefault = isDefault, HorizontalContentAlignment = HorizontalAlignment.Center };
                btn.Click += (s, e) => { result = value; dialog.Close(); };
                buttonPanel.Children.Add(btn);
            }

            if (buttons == MessageBoxButton.OK)
                AddButton(Labels.GenericOk, MessageBoxResult.OK, true);
            else
            {
                //the WPF MessageBox got these from the OS; no resource keys exist for them
                AddButton("Yes", MessageBoxResult.Yes, true);
                AddButton("No", MessageBoxResult.No, false);
            }

            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    buttonPanel,
                },
            };

            if (owner != null)
                await dialog.ShowDialog(owner);
            else
            {
                // fallback: fire-and-forget window when no owner is available
                dialog.Show();
                return MessageBoxResult.None;
            }

            return result;
        }
    }
}
