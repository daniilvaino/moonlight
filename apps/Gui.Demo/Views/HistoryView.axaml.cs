using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Moonlight.Gui.Demo.Models;

namespace Moonlight.Gui.Demo.Views;

public sealed partial class HistoryView : UserControl
{
    private readonly List<TransactionRow> rows =
    [
        new() { Status = "◷", StatusBrush = Brush.Parse("#727B80"), Date = new(2025, 4, 1, 9, 43, 0), TransactionId = "1f62ff84e6c532fa8909ad738ae6a063", Description = "Donation", Amount = -0.010031m, FiatAmount = -2.00m },
        new() { Status = "●", StatusBrush = Brush.Parse("#55B834"), Date = new(2025, 4, 1, 9, 16, 0), TransactionId = "4d02e6c4fba31ed6991e726cd958ce41", Description = "", Amount = 0.010000m, FiatAmount = 1.99m },
        new() { Status = "✓", StatusBrush = Brush.Parse("#3AAE34"), Date = new(2025, 4, 1, 9, 13, 0), TransactionId = "30d65635ec46ae411fc26873d04acac0", Description = "Exchange", Amount = 0.020000m, FiatAmount = 3.98m },
        new() { Status = "✓", StatusBrush = Brush.Parse("#3AAE34"), Date = new(2025, 3, 28, 18, 22, 0), TransactionId = "6e2cc99f5489953cf507b59761632f87", Description = "Mining payout", Amount = 0.000010m, FiatAmount = 0.00m }
    ];

    public event EventHandler<string>? StatusChanged;

    public HistoryView()
    {
        InitializeComponent();
        Table.ItemsSource = rows.OrderByDescending(row => row.Date).ToList();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text?.Trim() ?? string.Empty;
        Table.ItemsSource = rows.Where(row =>
            row.DateText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.TransactionId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.AmountText.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void ToggleTransactionId(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
            Table.Columns[1].IsVisible = item.IsChecked;
    }

    private async void CopyTransactionId(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is not TransactionRow row)
            return;

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(row.TransactionId);
        StatusChanged?.Invoke(this, "Transaction ID copied");
    }

    private void BeginDescriptionEdit(object? sender, RoutedEventArgs e) =>
        StatusChanged?.Invoke(this, "Double-click a description to edit it");

    private void ShowDetails(object? sender, RoutedEventArgs e) =>
        StatusChanged?.Invoke(this, Table.SelectedItem is TransactionRow row ? $"Transaction {row.TransactionId[..8]}…" : "Select a transaction");
}
