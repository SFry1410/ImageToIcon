using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ImageToIcon.Ui;

/// Two-button confirmation dialog. Returns true only when the confirm button was pressed.
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message, string confirmText, string cancelText)
    {
        var confirmed = false;
        var confirmBtn = new Button { Content = confirmText, Width = 120, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancelBtn = new Button { Content = cancelText, Width = 100, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };

        var dlg = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { confirmBtn, cancelBtn }
                    }
                }
            }
        };

        confirmBtn.Click += (_, _) =>
        {
            confirmed = true;
            dlg.Close();
        };
        cancelBtn.Click += (_, _) => dlg.Close();

        await dlg.ShowDialog(owner);
        return confirmed;
    }
}
