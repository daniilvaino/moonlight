using Moonlight.Diagnostics;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// The log, as it happens. A wallet that syncs in the background needs somewhere
/// to say what it did, or a scan that quietly stopped looks exactly like one that
/// found nothing.
/// </summary>
internal sealed class Journal : View
{
    private readonly ListView list;
    private readonly List<string> rows = [];

    public Journal()
    {
        list = new ListView(rows) { X = 1, Y = 0, Width = Dim.Fill(1), Height = Dim.Fill() };
        Add(list);

        Reload();
    }

    /// <summary>Rereads the log. Called on the main thread; the log is written from others.</summary>
    public void Reload()
    {
        rows.Clear();

        foreach (Entry entry in Log.Recent()) rows.Add(entry.ToString());

        if (rows.Count == 0) rows.Add("nothing logged yet");

        list.SetSource(rows);

        // Newest last, and the end is the interesting part. SelectedItem alone only
        // moves the selection — the pane goes on showing the oldest lines — so the
        // top is moved too. Bounds can still be empty here, hence the floor.
        list.SelectedItem = rows.Count - 1;
        list.TopItem = Math.Max(0, rows.Count - Math.Max(list.Bounds.Height, 1));

        SetNeedsDisplay();
    }
}
