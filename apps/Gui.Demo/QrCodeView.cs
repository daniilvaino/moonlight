using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using QRCoder;

namespace Moonlight.Gui.Demo;

public sealed class QrCodeView : Control
{
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<QrCodeView, string>(
        nameof(Value),
        "monero:87SfDuFKGnMjEVus9ZgvPBKRGPmb89ATX5cVGc93PJiST");

    private QRCodeData? code;
    private string? renderedValue;

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    static QrCodeView()
    {
        AffectsRender<QrCodeView>(ValueProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.White, Bounds);

        if (!string.Equals(renderedValue, Value, StringComparison.Ordinal))
        {
            code?.Dispose();
            code = QRCodeGenerator.GenerateQrCode(Value, QRCodeGenerator.ECCLevel.M);
            renderedValue = Value;
        }

        if (code is null)
            return;

        int count = code.ModuleMatrix.Count;
        int minX = count;
        int minY = count;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (!code.ModuleMatrix[y][x]) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
        if (maxX < minX || maxY < minY) return;

        int matrixWidth = maxX - minX + 1;
        int matrixHeight = maxY - minY + 1;
        double width = Math.Floor(Math.Min(Bounds.Width, Bounds.Height) - 6);
        double moduleX = width / matrixWidth;
        double moduleY = width / matrixHeight;
        double left = Math.Floor((Bounds.Width - width) / 2);
        double top = Math.Floor((Bounds.Height - width) / 2);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (code.ModuleMatrix[y][x])
                {
                    double x0 = Math.Round(left + ((x - minX) * moduleX));
                    double x1 = Math.Round(left + ((x - minX + 1) * moduleX));
                    double y0 = Math.Round(top + ((y - minY) * moduleY));
                    double y1 = Math.Round(top + ((y - minY + 1) * moduleY));
                    context.FillRectangle(Brushes.Black, new Rect(x0, y0, x1 - x0, y1 - y0));
                }
            }
        }
    }
}
