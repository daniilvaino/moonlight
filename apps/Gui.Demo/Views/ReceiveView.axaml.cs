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
        new() { Index = 9, IsHidden = true, Address = "87eqoG8zoq1Di3VgrpFX33IQ7wj9kv" },
        new() { Index = 10, IsChange = true, Address = "8B9Nq29Kb4LayiuJCSHeEn2GCAMKFo" }
    ];

    private bool showUsed = true;
    private bool showHidden;
    private bool showChange;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ShowTransactionsRequested;

    public ReceiveView()
    {
        InitializeComponent();
        UiState state = UiStateStore.Load();
        showUsed = state.ReceiveShowUsed;
        showHidden = state.ReceiveShowHidden;
        showChange = state.ReceiveShowChange;
        ShowUsedMenu.IsChecked = showUsed;
        ShowHiddenMenu.IsChecked = showHidden;
        ShowChangeMenu.IsChecked = showChange;
        ShowFullMenu.IsChecked = state.ReceiveShowFull;
        foreach (AddressRow row in rows) row.ShowFullAddress = state.ReceiveShowFull;
        if (state.ReceiveShowFull) Table.Columns[1].Width = DataGridLength.SizeToCells;
        ApplyFilter();
        Table.SelectedItem = Table.ItemsSource.Cast<AddressRow>().FirstOrDefault(row => row.Index == 6);
    }

    private AddressRow? Selected => Table.SelectedItem as AddressRow;

    private void ApplyFilter()
    {
        AddressRow? selected = Selected;
        string query = SearchBox.Text?.Trim() ?? string.Empty;
        Table.ItemsSource = rows.Where(row =>
            row.IsPinned ||
            (
            (showUsed || !row.IsUsed) &&
            (showHidden || !row.IsHidden) &&
            (showChange || !row.IsChange) &&
            (row.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             row.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             row.Index.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase)))).ToList();
        if (selected is not null && Table.ItemsSource.Cast<AddressRow>().Contains(selected))
            Table.SelectedItem = selected;
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void FilterChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string option } item)
            return;
        switch (option)
        {
            case "used": showUsed = item.IsChecked; break;
            case "hidden": showHidden = item.IsChecked; break;
            case "change": showChange = item.IsChecked; break;
            case "full":
                foreach (AddressRow row in rows)
                    row.ShowFullAddress = item.IsChecked;
                Table.Columns[1].Width = item.IsChecked ? DataGridLength.SizeToCells : new DataGridLength(310);
                break;
            case "index": Table.Columns[0].IsVisible = item.IsChecked; break;
            case "labels": Table.Columns[2].IsVisible = item.IsChecked; break;
        }
        UiStateStore.Save(state =>
        {
            state.ReceiveShowUsed = showUsed;
            state.ReceiveShowHidden = showHidden;
            state.ReceiveShowFull = ShowFullMenu.IsChecked;
            state.ReceiveShowChange = showChange;
        });
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

    private void EditLabel(object? sender, RoutedEventArgs e)
    {
        if (Selected is null) return;
        Table.CurrentColumn = Table.Columns[2];
        Table.BeginEdit();
    }
    private void ShowTransactions(object? sender, RoutedEventArgs e)
    {
        if (Selected is { } row) ShowTransactionsRequested?.Invoke(this, row.Address);
    }
    private void TogglePin(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        row.IsPinned = !row.IsPinned;
        ApplyFilter();
        StatusChanged?.Invoke(this, row.IsPinned ? "Address pinned" : "Address unpinned");
    }
    private void HideAddress(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        row.IsHidden = !row.IsHidden;
        ApplyFilter();
        StatusChanged?.Invoke(this, row.IsHidden ? "Address hidden" : "Address shown");
    }
    private async void PaymentRequest(object? sender, RoutedEventArgs e)
    {
        if (Selected is { } row)
            await DemoDialog.ShowInfoAsync(this, "Create Payment Request", $"Address: {row.Address}\nLabel: {row.Label}\n\nAmount and description will be encoded into the payment URI by the connected wallet.");
    }
    private void NewAddress(object? sender, RoutedEventArgs e)
    {
        int index = rows.Max(row => row.Index) + 1;
        var row = new AddressRow { Index = index, Address = $"87newMoonlightAddress{index:D2}kP5MC6vB2drLr4zCsYavfyWf" };
        rows.Add(row);
        ApplyFilter();
        Table.SelectedItem = row;
        StatusChanged?.Invoke(this, $"Address #{index} created");
    }
}
