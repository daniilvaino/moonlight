using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Moonlight.Gui.Demo.Models;

namespace Moonlight.Gui.Demo.Views;

public sealed partial class CoinsView : UserControl
{
    private readonly List<CoinRow> rows =
    [
        new() { PublicKey = "1a937bbf0e45d6bb", TransactionId = "1f62ff84e6c532fa", Address = "88usd…RFiFf", BlockHeight = 3380455, Amount = 0.010000000000m, State = CoinState.Locked },
        new() { PublicKey = "61edba731b3cf332", TransactionId = "4d02e6c4fba31ed6", Address = "84EVJ…diF9T", BlockHeight = 3380454, Amount = 0.020000000000m, State = CoinState.Spent },
        new() { PublicKey = "c1cafd7a57069ec4", TransactionId = "30d65635ec46ae41", Address = "87X7d…r78DN", BlockHeight = 3377835, Amount = 0.000000010000m, State = CoinState.Available },
        new() { PublicKey = "d7642ac984aa6730", TransactionId = "6e2cc99f5489953c", Address = "82VjQ…P5MC6", Label = "Exchange", BlockHeight = 3372112, Amount = 0.000010000000m, State = CoinState.Frozen },
        new() { PublicKey = "f431bc19a5fd89de", TransactionId = "7b59ac0215723edb", Address = "86ymL…mV3oT", BlockHeight = 3369004, Amount = 0.015000000000m, State = CoinState.SelectedForSpend }
    ];

    public event EventHandler<string>? StatusChanged;

    public CoinsView()
    {
        InitializeComponent();
        Table.ItemsSource = rows.OrderByDescending(row => row.BlockHeight).ToList();
    }

    private CoinRow? Selected => Table.SelectedItem as CoinRow;

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text?.Trim() ?? string.Empty;
        Table.ItemsSource = rows.Where(row =>
            row.PublicKey.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.TransactionId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderByDescending(row => row.BlockHeight).ToList();
    }

    private async Task CopyAsync(string text, string status)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
        StatusChanged?.Invoke(this, status);
    }

    private async void CopyPublicKey(object? sender, RoutedEventArgs e) { if (Selected is { } row) await CopyAsync(row.PublicKey, "Public key copied"); }
    private async void CopyTransactionId(object? sender, RoutedEventArgs e) { if (Selected is { } row) await CopyAsync(row.TransactionId, "Transaction ID copied"); }
    private async void CopyAddress(object? sender, RoutedEventArgs e) { if (Selected is { } row) await CopyAsync(row.Address, "Address copied"); }
    private async void CopyAmount(object? sender, RoutedEventArgs e) { if (Selected is { } row) await CopyAsync(row.AmountText, "Amount copied"); }
    private void SpendCoins(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, $"{Table.SelectedItems.Count} coin(s) selected for spending");
    private void EditLabel(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Double-click a label to edit it");
    private void FreezeCoin(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Coin freeze state changed");
    private void SweepCoin(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Sweep selected coin");
    private void ShowDetails(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, Selected is { } row ? $"Coin {row.PublicKey[..8]}…" : "Select a coin");
}
