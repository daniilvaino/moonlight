using QRCoder;

namespace Moonlight.Tui.Screens;

/// <summary>
/// A QR code drawn with block characters. Two rows of modules share one line —
/// terminal cells are about twice as tall as they are wide, so a code drawn one
/// row per line comes out stretched and phones struggle with it.
/// </summary>
internal static class Qr
{
    private const char Both = '█';    // █ upper and lower set
    private const char Upper = '▀';   // ▀
    private const char Lower = '▄';   // ▄
    private const char Neither = ' ';

    public static string[] Render(string text)
    {
        using QRCodeData data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);

        int size = data.ModuleMatrix.Count;
        List<string> lines = [];

        // A quiet border is part of the specification, not decoration: without it
        // a scanner cannot find the code's edges.
        const int quiet = 2;

        for (int y = -quiet; y < size + quiet; y += 2)
        {
            System.Text.StringBuilder line = new(size + (quiet * 2));

            for (int x = -quiet; x < size + quiet; x++)
            {
                bool upper = Module(data, size, x, y);
                bool lower = Module(data, size, x, y + 1);

                line.Append((upper, lower) switch
                {
                    (true, true) => Both,
                    (true, false) => Upper,
                    (false, true) => Lower,
                    _ => Neither,
                });
            }

            lines.Add(line.ToString());
        }

        return [.. lines];
    }

    /// <summary>Outside the code is quiet space, which reads as an unset module.</summary>
    private static bool Module(QRCodeData data, int size, int x, int y)
        => x >= 0 && y >= 0 && x < size && y < size && data.ModuleMatrix[y][x];
}
