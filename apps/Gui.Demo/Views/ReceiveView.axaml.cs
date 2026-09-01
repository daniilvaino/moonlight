using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Moonlight.Gui.Demo.Models;

namespace Moonlight.Gui.Demo.Views;

public sealed partial class ReceiveView : UserControl
{
    private readonly List<AddressRow> rows =
    [
        new() { Index = 4, IsPinned = true, Address = "82VjQ2LZqkNcWqS7B2drLr4zCsYavfyWfPX6NSP5MC6", Label = "Exchange" },
        new() { Index = 1, IsUsed = true, Address = "87X7dGWaQB6WoRXcqAeZGkEMGpaVif3rcMHr78DN" },
        new() { Index = 2, IsUsed = true, Address = "84EVJeyFpUs6mh5BSeQCDinvdAuwpxW92FdiF9T" },
        new() { Index = 3, IsUsed = true, Address = "88usdUphRq6CGJuQWSvuSyPoVVacreHJRFiFf" },
        new() { Index = 5, Address = "86ymLxZ6tn8CXKcDYQ2E6mJ4EehW4PFnmV3oT" },
        new() { Index = 6, Address = "87SfDuFKGnMjEVus9ZgvPBKRGPmb89ATX5cVGc93PJiST" },
        new() { Index = 7, Address = "82t8DfAJeJnQhFu54MmWnATX5cVGc93PJiST" },
        new() { Index = 8, Address = "86REVZR95cuYydrQwCJtNH7dT1jhgp9N" },
        new() { Index = 9, Address = "87eqoG8zoq1Di3VgrpFX33IQ7wj9kv" },
        new() { Index = 10, Address = "8B9Nq29Kb4LayiuJCSHeEn2GCAMKFo" }
    ];

    private bool showUsed = true;
    public event EventHandler<string>? StatusChanged;

    public ReceiveView()
    {
        InitializeComponent();
        ApplyFilter();
        Table.SelectedItem = rows.First(row => row.Index == 6);
    }

    private AddressRow? Selected => Table.SelectedItem as AddressRow;

    private void ApplyFilter()
    {
        string query = SearchBox.Text?.Trim() ?? string.Empty;
        Table.ItemsSource = rows.Where(row =>
            (showUsed || !row.IsUsed) &&
            (row.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             row.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             row.Index.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void FilterChanged(object? sender, RoutedEventArgs e)
    {
        showUsed = sender is MenuItem { IsChecked: true };
        ApplyFilter();
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Selected is { } row)
            QrCode.Value = $"monero:{row.Address}";
    }

    private async Task CopyAsync(string text, string status)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
        StatusChanged?.Invoke(this, status);
    }

    private async void CopyAddress(object? sender, RoutedEventArgs e)
    {
        if (Selected is { } row) await CopyAsync(row.Address, "Address copied");
    }

    private async void CopyLabel(object? sender, RoutedEventArgs e)
    {
        if (Selected is { } row) await CopyAsync(row.Label, "Label copied");
    }

    private void EditLabel(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Double-click a label to edit it");
    private void ShowTransactions(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Address transactions selected");
    private void TogglePin(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Pin state changed");
    private void HideAddress(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Address hidden");
    private void PaymentRequest(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Payment request editor · not connected");
    private void NewAddress(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "New address · not connected");
}
