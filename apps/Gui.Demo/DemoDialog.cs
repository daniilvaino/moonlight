using Avalonia.Controls;
using Avalonia.Layout;

namespace Moonlight.Gui.Demo;

internal static class DemoDialog
{
    public static async Task ShowInfoAsync(Control source, string title, string body)
    {
        Window dialog = Create(title, body, false);
        if (TopLevel.GetTopLevel(source) is Window owner)
            await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ConfirmAsync(Control source, string title, string body)
    {
        Window dialog = Create(title, body, true);
        return TopLevel.GetTopLevel(source) is Window owner && await dialog.ShowDialog<bool>(owner);
    }

    private static Window Create(string title, string body, bool confirmation)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var content = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 14 };
        content.Children.Add(new TextBlock { Text = body, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 7 };
        if (confirmation)
        {
            var cancel = new Button { Content = "Cancel" };
            cancel.Click += (_, _) => dialog.Close(false);
            actions.Children.Add(cancel);
        }
        var accept = new Button { Content = confirmation ? "Continue" : "Close" };
        accept.Click += (_, _) => dialog.Close(true);
        actions.Children.Add(accept);
        content.Children.Add(actions);
        dialog.Content = content;
        return dialog;
    }
}
