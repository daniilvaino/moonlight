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
        double module = Math.Floor(Math.Min(Bounds.Width, Bounds.Height) / count);
        double width = module * count;
        double left = Math.Floor((Bounds.Width - width) / 2);
        double top = Math.Floor((Bounds.Height - width) / 2);

        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (code.ModuleMatrix[y][x])
                    context.FillRectangle(Brushes.Black, new Rect(left + (x * module), top + (y * module), module, module));
            }
        }
    }
}
