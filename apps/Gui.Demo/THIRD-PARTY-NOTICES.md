# Third-party notices — Moonlight.Gui.Demo

The notices in the root of this repository cover vendored source: code copied into
`Vendor/` directories and compiled with ours. The demo is the one application that
also ships other people's *binaries*, which arrive as NuGet packages and are not
covered there.

| component | licence | copyright |
|---|---|---|
| [Avalonia](https://avaloniaui.net) | MIT | Copyright 2013–2026 © The AvaloniaUI Project |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | © Microsoft Corporation |
| [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | MIT | © Microsoft Corporation |

`libSkiaSharp` and `libHarfBuzzSharp` are native builds that carry a good deal more
inside them — Skia itself, ANGLE, libpng, zlib and others, under their own terms.
Microsoft ships the full set of notices for those in the native-asset package, and
those are the authoritative text.

A release archive carries all of it in `licenses/`, collected from the packages the
build actually restored rather than transcribed here: transcribed notices go stale
against the package they describe, and a notice that no longer matches what shipped
is worse than one that is merely long. See `.github/demo-licences.sh`.

The .NET runtime files beside the application are covered by the MIT licence of the
.NET platform.
