# GUI evaluation harness

Three things are checked, in the order they are worth checking.

## Behaviour

`VisualCaptureTests` drives the real controls: column toggles, the search box revealing the
transaction id, receive filters and address creation, coin filters and freezing. These are
ordinary assertions about state, and they are what catches a broken interaction.

## Appearance guards

`InterfacePolishTests` measures the two properties that drifted silently once and would drift
again unwatched:

- the type ramp has exactly two steps — 12pt for anything read, 10pt for a micro-label
  carrying the `micro` class;
- every rail caption sits centred within a pixel of its button.

Both are measured off the laid-out tree rather than asserted against the stylesheet, so they
catch the theme underneath changing its mind. That is how the first one found Fluent's own
14pt default sitting under a 12pt window style, with 11, 12, 13 and 14 all on screen at once,
and how the second found the active tab's accent border pushing its caption two pixels over.

## Pixel regression

For every primary view the harness renders the real `MainWindow` at 977x499 through the
Avalonia headless Skia backend and produces:

- `*-actual.png` — the rendered application;
- `*-diff.png` — dim grey for matching pixels and magenta for differences;
- `*-report.json` — dimensions, changed-pixel ratio, MAE, and thresholds.

Generated artifacts go to `artifacts/gui-eval/` and are git-ignored. Approved baselines live in
`Baselines/<platform>/<size>/` and are reviewed like source code.

This tier catches *unintended change*. It cannot tell you the interface is right, because the
baselines are the interface's own output — approve one only after looking at it.

```sh
dotnet test tests/Gui.Tests/Gui.Tests.csproj

MOONLIGHT_UPDATE_VISUAL_BASELINES=1 \
  dotnet test tests/Gui.Tests/Gui.Tests.csproj
```

Baseline updates are deliberately explicit and refused in CI. The baselines cover the macOS
Skia renderer only; the test skips elsewhere.

None of this replaces a native Appium smoke test for focus, menus, accessibility, DPI, and
platform window behaviour.
