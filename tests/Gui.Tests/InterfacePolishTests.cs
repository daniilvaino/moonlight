using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Moonlight.Gui.Demo;
using Xunit;

namespace Moonlight.Gui.Tests;

/// <summary>
/// Guards the things that make an interface look built rather than assembled: a type ramp with
/// exactly two steps, and captions sitting where they belong inside their control. Both drifted
/// once because nothing measured them — Fluent's own 14pt default sat under a 12pt window style,
/// so 11, 12, 13 and 14 all appeared side by side across the views.
/// </summary>
public sealed class InterfacePolishTests
{
    /// <summary>The size everything a person reads is set in.</summary>
    private const double BodyFontSize = 12;

    /// <summary>The size of a letterspaced micro-label naming a column, a tab or a field.</summary>
    private const double MicroFontSize = 10;

    private static readonly string[] Pages =
        ["HomePage", "HistoryPage", "SendPage", "ReceivePage", "CoinsPage", "ContactsPage"];

    private static readonly string[] Tabs =
        ["HomeTab", "HistoryTab", "SendTab", "ReceiveTab", "CoinsTab", "ContactsTab"];

    [AvaloniaFact]
    public static void TypeRampHasExactlyTwoSteps()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var offenders = new List<string>();
            foreach (string name in Pages.Concat(Tabs))
            {
                Control host = window.FindControl<Control>(name)
                    ?? throw new InvalidOperationException($"Missing {name}.");
                foreach (ILogical logical in host.GetSelfAndLogicalDescendants())
                {
                    if (logical is not Control control)
                        continue;

                    double size = FontSize(control);
                    double allowed = IsMicro(control) ? MicroFontSize : BodyFontSize;
                    if (size == allowed)
                        continue;

                    offenders.Add(
                        $"{name}/{control.GetType().Name} \"{Caption(control)}\" is "
                        + size.ToString(CultureInfo.InvariantCulture) + "pt, expected "
                        + allowed.ToString(CultureInfo.InvariantCulture));
                }
            }

            Assert.True(
                offenders.Count == 0,
                $"The ramp is {BodyFontSize}pt for anything read and {MicroFontSize}pt for a micro-label "
                + "carrying the 'micro' class. These sit off it:\n  " + string.Join("\n  ", offenders));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public static void RailCaptionsSitCentredUnderTheirIcon()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            foreach (string name in Tabs)
            {
                Button tab = window.FindControl<Button>(name)
                    ?? throw new InvalidOperationException($"Missing {name}.");
                TextBlock caption = tab.GetSelfAndLogicalDescendants().OfType<TextBlock>().First();

                Point origin = caption.TranslatePoint(default, tab)
                    ?? throw new InvalidOperationException($"{name} caption is not laid out.");
                double left = origin.X;
                double right = tab.Bounds.Width - (origin.X + caption.Bounds.Width);

                Assert.True(
                    Math.Abs(left - right) <= 1,
                    $"{name}: the caption sits {left:0.##}px from the left and {right:0.##}px from the "
                    + "right of the rail button. It should be centred within a pixel.");
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A micro-label is marked as one, so the ramp cannot be widened by accident.</summary>
    private static bool IsMicro(Control control)
        => control is TextBlock && control.Classes.Contains("micro");

    private static double FontSize(Control control)
        => control switch
        {
            TextBlock text => text.FontSize,
            TemplatedControl templated => templated.FontSize,
            _ => BodyFontSize,
        };

    private static string Caption(Control control)
        => control switch
        {
            TextBlock text => text.Text ?? string.Empty,
            ContentControl { Content: string content } => content,
            TextBox box => box.PlaceholderText ?? string.Empty,
            _ => string.Empty,
        };
}
