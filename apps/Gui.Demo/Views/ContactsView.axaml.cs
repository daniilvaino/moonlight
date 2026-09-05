using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Moonlight.Gui.Demo.Models;

namespace Moonlight.Gui.Demo.Views;

public sealed partial class ContactsView : UserControl
{
    private readonly List<ContactRow> rows =
    [
        new() { Name = "CCS donation", Address = "88sDCmPMEhMWKPitFXyJ9hSeFESwE6SAVCTrPo3WiRySiA3J8352Zcx2BTS7pVTb9G3pmxPJ2FpSy89M9nj5nQJ32D" },
        new() { Name = "Exchange withdrawal", Address = "84EVJnHdiF9TzKZmtBnQqLBFmYkPnJhkJ2sVvrLPtBnQqLBFmYkPnJhkJ2sVvrLPtBnQqLBFmYkPnJhkJ2sVdiF9T" },
        new() { Name = "Cold storage", Address = "87X7dr78DNqLBFmYkPnJhkJ2sVvrLPtBnQqLBFmYkPnJhkJ2sVvrLPtBnQqLBFmYkPnJhkJ2sVvrLPtBnr78DN" }
    ];

    public event EventHandler<string>? StatusChanged;

    /// <summary>Raised when a contact is chosen to be paid, which sends the user to the Send tab.</summary>
    public event EventHandler<string>? PayToRequested;

    public ContactsView()
    {
        InitializeComponent();
        ApplyFilter();
    }

    private ContactRow? Selected => Table.SelectedItem as ContactRow;

    private void ApplyFilter()
    {
        string query = SearchBox.Text?.Trim() ?? string.Empty;
        bool full = ShowFullAddressesMenu.IsChecked;
        foreach (ContactRow row in rows)
            row.ShowFullAddress = full;

        Table.ItemsSource = rows
            .Where(row =>
                row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Address.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void FilterChanged(object? sender, RoutedEventArgs e) => ApplyFilter();

    private void NewContact(object? sender, RoutedEventArgs e)
    {
        rows.Add(new ContactRow { Name = "New contact", Address = string.Empty });
        ApplyFilter();
        StatusChanged?.Invoke(this, "Contact added");
    }

    private void ImportCsv(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Import CSV · not connected");

    private void ExportCsv(object? sender, RoutedEventArgs e) => StatusChanged?.Invoke(this, "Export CSV · not connected");

    private async void CopyAddress(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            StatusChanged?.Invoke(this, "Select a contact");
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(row.Address);
        StatusChanged?.Invoke(this, "Address copied");
    }

    private void PayTo(object? sender, RoutedEventArgs e)
    {
        if (Selected is { } row)
            PayToRequested?.Invoke(this, row.Address);
        else
            StatusChanged?.Invoke(this, "Select a contact");
    }

    private void DeleteContact(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            StatusChanged?.Invoke(this, "Select a contact");
            return;
        }

        rows.Remove(row);
        ApplyFilter();
        StatusChanged?.Invoke(this, "Contact deleted");
    }
}
