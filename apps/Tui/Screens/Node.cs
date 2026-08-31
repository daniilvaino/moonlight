using Moonlight.Node;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>Which daemon we are talking to, and whether it is answering.</summary>
internal sealed class Node : FrameView
{
    private readonly TextField address;
    private readonly Label status;

    public Node(Uri daemon)
        : base("Node")
    {
        ArgumentNullException.ThrowIfNull(daemon);

        Add(new Label("daemon") { X = 2, Y = 1, ColorScheme = Colors.Dialog });
        address = new TextField(daemon.ToString()) { X = 12, Y = 1, Width = Dim.Fill(12) };

        Button check = new("Check") { X = Pos.AnchorEnd(11), Y = 1 };
        check.Clicked += () => _ = Check();

        Add(new Label("status") { X = 2, Y = 3, ColorScheme = Colors.Dialog });
        status = new Label("not checked") { X = 12, Y = 3, Width = Dim.Fill(2) };

        Add(address, check, status);
    }

    public Uri Address => new(address.Text?.ToString() ?? "http://127.0.0.1:18081/");

    public async Task Check()
    {
        status.Text = "asking…";
        SetNeedsDisplay();

        try
        {
            using DaemonClient client = new(Address);
            ulong height = await client.GetHeightAsync().ConfigureAwait(true);

            status.Text = $"height {height}";
        }
        catch (Exception e) when (e is HttpRequestException or DaemonException or UriFormatException or TaskCanceledException)
        {
            // A daemon that is not there is the ordinary case, not a crash.
            status.Text = $"unreachable — {e.Message}";
        }

        SetNeedsDisplay();
    }
}
