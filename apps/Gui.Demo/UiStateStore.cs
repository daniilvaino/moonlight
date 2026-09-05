using System.Text.Json;

namespace Moonlight.Gui.Demo;

internal sealed class UiState
{
    public bool[] HistoryColumns { get; set; } = [true, false, true, true];
    public bool HistoryFullTxid { get; set; }
    public bool ReceiveShowUsed { get; set; } = true;
    public bool ReceiveShowHidden { get; set; }
    public bool ReceiveShowFull { get; set; }
    public bool ReceiveShowChange { get; set; }
    public bool CoinsShowSpent { get; set; } = true;
}

internal static class UiStateStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static UiState? cached;

    public static bool Enabled { get; set; }

    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Moonlight",
        "ui-state.json");

    public static UiState Load()
    {
        lock (Gate)
        {
            if (!Enabled) return new UiState();
            if (cached is not null) return cached;
            try
            {
                cached = File.Exists(StatePath)
                    ? JsonSerializer.Deserialize<UiState>(File.ReadAllText(StatePath), Options) ?? new UiState()
                    : new UiState();
            }
            catch (JsonException)
            {
                cached = new UiState();
            }
            catch (IOException)
            {
                cached = new UiState();
            }
            catch (UnauthorizedAccessException)
            {
                cached = new UiState();
            }
            return cached;
        }
    }

    public static void Save(Action<UiState> update)
    {
        if (!Enabled) return;
        lock (Gate)
        {
            UiState state = Load();
            update(state);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                File.WriteAllText(StatePath, JsonSerializer.Serialize(state, Options));
            }
            catch (IOException)
            {
                // UI preferences are best-effort and must never break wallet interaction.
            }
            catch (UnauthorizedAccessException)
            {
                // Sandboxed deployments may not expose a writable application-data folder.
            }
        }
    }
}
