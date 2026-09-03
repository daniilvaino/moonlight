using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Moonlight.Gui.Demo;
using Moonlight.Gui.Demo.Models;
using Moonlight.Gui.Demo.Views;
using Xunit;

namespace Moonlight.Gui.Tests;

public sealed class VisualCaptureTests
{
    private static readonly string OutputDirectory = Path.Combine(Path.GetTempPath(), "moonlight-gui-eval");

    [AvaloniaFact]
    public static void CaptureEveryPrimaryViewAtReferenceSize()
    {
        Directory.CreateDirectory(OutputDirectory);

        var window = new MainWindow
        {
            Width = 835,
            Height = 469
        };
        try
        {
            window.Show();
            Capture(window, "home");

            foreach (string view in new[] { "History", "Send", "Receive", "Coins", "Contacts", "Notes", "Calc" })
            {
                Button tab = window.FindControl<Button>($"{view}Tab")
                    ?? throw new InvalidOperationException($"Missing {view} tab.");
                tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Capture(window, view.ToLowerInvariant());
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void CaptureProductionHistoryAndSend()
    {
        Directory.CreateDirectory(OutputDirectory);
        var window = new MainWindow();
        try
        {
            window.Show();
            window.FindControl<Button>("HistoryTab")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Capture(window, "history-production", new PixelSize(977, 499));
            window.FindControl<Button>("SendTab")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Capture(window, "send-production", new PixelSize(977, 499));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void ProductionAmountIsCenteredAndSendFieldsShareOneAxis()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.FindControl<Button>("HistoryTab")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            DataGrid history = window.FindControl<HistoryView>("HistoryPage")!.FindControl<DataGrid>("Table")!;
            Assert.Equal(12, history.FontSize);
            Assert.True(history.Columns[3].ActualWidth >= 112);

            window.FindControl<Button>("SendTab")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Control send = window.FindControl<Control>("SendPage")!;
            TextBox payTo = window.FindControl<TextBox>("PayToBox")!;
            TextBox description = window.FindControl<TextBox>("DescriptionBox")!;
            TextBox amountBox = window.FindControl<TextBox>("AmountBox")!;
            ComboBox fee = window.FindControl<ComboBox>("FeePriorityBox")!;
            StackPanel actions = window.FindControl<StackPanel>("SendActions")!;
            double fieldX = payTo.TranslatePoint(default, send)!.Value.X;
            Assert.Equal(fieldX, description.TranslatePoint(default, send)!.Value.X);
            Assert.Equal(fieldX, amountBox.TranslatePoint(default, send)!.Value.X);
            Assert.Equal(fieldX, fee.TranslatePoint(default, send)!.Value.X);
            Assert.True(actions.TranslatePoint(default, send)!.Value.Y > fee.TranslatePoint(default, send)!.Value.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void HistoryHasCanonicalColumnsAndSearchState()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            HistoryView history = window.FindControl<HistoryView>("HistoryPage")
                ?? throw new InvalidOperationException("Missing History view.");
            DataGrid table = history.FindControl<DataGrid>("Table")
                ?? throw new InvalidOperationException("Missing History table.");
            TextBox search = history.FindControl<TextBox>("SearchBox")
                ?? throw new InvalidOperationException("Missing History search box.");

            Assert.Equal(4, table.Columns.Count);
            Assert.True(table.Columns[0].IsVisible);
            Assert.False(table.Columns[1].IsVisible);
            Assert.True(table.Columns[2].IsVisible);
            Assert.True(table.Columns[3].IsVisible);
            Assert.Equal("Donation", Assert.IsType<TransactionRow>(table.ItemsSource!.Cast<object>().First()).Description);

            search.Text = "1f62ff84";
            Dispatcher.UIThread.RunJobs();
            Assert.True(table.Columns[1].IsVisible);
            Assert.Single(table.ItemsSource!.Cast<object>());

            search.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Assert.False(table.Columns[1].IsVisible);

            search.Text = "Donation";
            Dispatcher.UIThread.RunJobs();
            Assert.True(table.Columns[1].IsVisible);
            TransactionRow match = Assert.IsType<TransactionRow>(Assert.Single(table.ItemsSource!.Cast<object>()));
            Assert.Equal("Donation", match.Description);

            MenuItem reset = history.FindControl<MenuItem>("ResetDefaultsMenu")
                ?? throw new InvalidOperationException("Missing reset option.");
            reset.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(table.Columns[1].IsVisible);

            search.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Assert.False(table.Columns[1].IsVisible);
            Assert.Equal("Donation", Assert.IsType<TransactionRow>(table.ItemsSource!.Cast<object>().First()).Description);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void HistoryContainsEveryConfirmationStage()
    {
        TransactionRow Create(int confirmations) => new()
        {
            Confirmations = confirmations,
            ConfirmationsRequired = 10,
            Date = DateTime.UnixEpoch,
            TransactionId = confirmations.ToString(CultureInfo.InvariantCulture),
            Amount = 0
        };
        TransactionRow[] stages = [Create(1), Create(3), Create(5), Create(7), Create(9), Create(10)];
        for (int index = 0; index < stages.Length; index++)
            Assert.Equal(index + 1, stages[index].ConfirmationStage);
        Assert.Equal("1/10 confirmations", stages[0].StatusToolTip);
        Assert.Equal("10 confirmations", stages[^1].StatusToolTip);
    }

    [AvaloniaFact]
    public static void HistoryOptionsPreserveUsableColumnsAndToggleFullIds()
    {
        var history = new HistoryView();
        var window = new Window { Content = history };
        try
        {
            window.Show();
            DataGrid table = history.FindControl<DataGrid>("Table")!;
            MenuItem full = history.FindControl<MenuItem>("FullTransactionIdMenu")!;
            full.IsChecked = true;
            full.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var txid = Assert.IsType<DataGridTextColumn>(table.Columns[1]);
            Assert.Equal(nameof(TransactionRow.TransactionId), Assert.IsType<Binding>(txid.Binding).Path);
            Assert.True(txid.Width.IsStar);

            MenuItem date = history.FindControl<MenuItem>("DateColumnMenu")!;
            MenuItem description = history.FindControl<MenuItem>("DescriptionColumnMenu")!;
            MenuItem amount = history.FindControl<MenuItem>("AmountColumnMenu")!;
            date.IsChecked = false;
            date.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            description.IsChecked = false;
            description.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            amount.IsChecked = false;
            amount.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(amount.IsChecked);
            Assert.True(table.Columns[3].IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void HistoryContextActionsFollowTransactionState()
    {
        var history = new HistoryView();
        var window = new Window { Content = history };
        try
        {
            window.Show();
            DataGrid table = history.FindControl<DataGrid>("Table")!;
            ContextMenu menu = table.ContextMenu!;
            MenuItem[] actions = menu.Items.OfType<MenuItem>().ToArray();
            MenuItem resend = actions[0];
            MenuItem remove = actions[1];

            table.SelectedItem = table.ItemsSource!.Cast<TransactionRow>().First(row => row.Failed);
            menu.Open(table);
            Dispatcher.UIThread.RunJobs();
            Assert.True(resend.IsVisible);
            Assert.True(remove.IsVisible);
            menu.Close();

            table.SelectedItem = table.ItemsSource!.Cast<TransactionRow>().First(row => !row.Failed && !row.Pending);
            menu.Open(table);
            Dispatcher.UIThread.RunJobs();
            Assert.False(resend.IsVisible);
            Assert.False(remove.IsVisible);
            menu.Close();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void ProductionWindowKeepsSourceDrivenDefaultSize()
    {
        var window = new MainWindow();
        try
        {
            Assert.Equal(977, window.Width);
            Assert.Equal(499, window.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void ReceiveOptionsFilterRowsAndNewAddressUpdatesQrSelection()
    {
        var receive = new ReceiveView();
        var window = new Window { Content = receive };
        try
        {
            window.Show();
            DataGrid table = receive.FindControl<DataGrid>("Table")!;
            Assert.Equal(8, table.ItemsSource!.Cast<AddressRow>().Count());

            MenuItem showUsed = receive.FindControl<MenuItem>("ShowUsedMenu")!;
            showUsed.IsChecked = false;
            showUsed.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(5, table.ItemsSource!.Cast<AddressRow>().Count());

            MenuItem showFull = receive.FindControl<MenuItem>("ShowFullMenu")!;
            showFull.IsChecked = true;
            showFull.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.All(table.ItemsSource!.Cast<AddressRow>(), row => Assert.Equal(row.Address, row.AddressText));

            Button create = receive.FindControl<Button>("NewAddressButton")!;
            create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            AddressRow created = Assert.IsType<AddressRow>(table.SelectedItem);
            Assert.Equal(11, created.Index);
            Assert.Contains(created, table.ItemsSource!.Cast<AddressRow>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void CoinFiltersAndFreezeActionMutateSemanticState()
    {
        var coins = new CoinsView();
        var window = new Window { Content = coins };
        try
        {
            window.Show();
            DataGrid table = coins.FindControl<DataGrid>("Table")!;
            Assert.Equal(5, table.ItemsSource!.Cast<CoinRow>().Count());

            MenuItem showSpent = coins.FindControl<MenuItem>("ShowSpentMenu")!;
            showSpent.IsChecked = false;
            showSpent.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(4, table.ItemsSource!.Cast<CoinRow>().Count());

            CoinRow available = table.ItemsSource!.Cast<CoinRow>().First(row => row.State == CoinState.Available);
            table.SelectedItem = available;
            MenuItem freeze = table.ContextMenu!.Items.OfType<MenuItem>().ElementAt(3);
            freeze.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(CoinState.Frozen, available.State);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Capture(Window window, string name)
        => Capture(window, name, new PixelSize(835, 469));

    private static void Capture(Window window, string name, PixelSize expectedSize)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"The {name} view rendered no frame.");
        if (frame.PixelSize != expectedSize)
            throw new InvalidOperationException($"The {name} frame was {frame.PixelSize}, expected {expectedSize}.");
        frame.Save(Path.Combine(OutputDirectory, $"{name}.png"), PngBitmapEncoderOptions.Default);
    }
}
