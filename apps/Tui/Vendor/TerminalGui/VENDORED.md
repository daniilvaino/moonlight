# Vendored: Terminal.Gui v1 (SharpOS fork)

| | |
|---|---|
| repo | https://github.com/daniilvaino/SharpOS — `vendor/Terminal.Gui` |
| commit | `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` (2026-08-21) |
| licence | MIT |
| imported | 2026-08-31 |
| scope | the curated compile-in module named by `TerminalGui.props`: `Core`, `Types`, `Windows`, `Views`, the `SharpOS/` shims and driver |

Not a plain Terminal.Gui: the fork replaced NStack with its own `Rune`/`TextExtensions`
and reads no packages at all. `Terminal.Gui/ConsoleDrivers/` (the ncurses, kernel32 and
NetDriver implementations, ~57 P/Invoke sites) is **not** part of the module and was not
copied. The compiled set has 0 P/Invoke and 0 `PackageReference`.

`XtermSharp/CharWidth.cs` comes from the same repo's `vendor/XtermSharp` (MIT); the props
file needs it for character widths. The rest of XtermSharp is not copied.

Requires **C# 14** — `RuneCount`/`ConsoleWidth` are extension properties, used at ~54
call sites. Target framework stays `net8.0`; only the compiler must be new.

## Hosts

`MoonlightHost` selects the input side. `SharpOSDriver` renders for both — it writes ANSI
through `Console.Write` either way.

| | `console` (default) | `sharpos` |
|---|---|---|
| size | `Console.WindowWidth/Height`, `COLUMNS`/`LINES` | `AppConsole.TryGetSize` |
| loop | `Console/ConsoleMainLoop.cs` | `SharpOS/SharpOSMainLoop.cs` |
| keys | `Console/ConsoleKeyMap.cs` (`ConsoleKeyInfo`) | `SharpOS/SharpOSKeyMap.cs` (set-1 scan codes) |
| `DefineConstants` | — | `SHARPOS` |

The SharpOS driver, loop and key map stay in the tree untouched in substance: the app is
meant to run on SharpOS eventually, and deleting them would mean writing them again.

## Differences from upstream (SharpOS fork)

| file | change | reason |
|---|---|---|
| `Terminal.Gui/ConsoleDrivers/**` | not copied | stock drivers, not in the module; carry P/Invoke |
| `Terminal.Gui/Resources/**` | not copied | `StringsShim` replaces the resource machinery |
| `SharpOS/SharpOSDriver.cs` | `using SharpOS.AppSdk` behind `#if SHARPOS`; size via new `TerminalSize`; `PrepareToRun` picks the loop per host; init message reworded | run on a terminal without forking the renderer |
| `SharpOS/TypeIdentity.cs` | `GetEETypePtr()`/`EETypePtr` behind `#if SHARPOS`, `GetType()` otherwise | no NativeAOT-nostd runtime API off SharpOS |
| `SharpOS/TerminalSize.cs` | **added** (Moonlight) | the one host difference in the driver |
| `Console/ConsoleMainLoop.cs`, `Console/ConsoleKeyMap.cs` | **added** (Moonlight) | `System.Console` counterparts of the SharpOS pair |
| `Autocomplete.cs`, `IAutocomplete.cs`, `TextField.cs`, `TextView.cs` | dropped `using Rune = System.Rune;` | replaced by a project-wide `Using` alias; `System.Text.Rune` exists on net8 and collided |
| `TerminalGui.props` | host switch, `Rune` alias, `Nullable=disable`, `CharWidth.cs` path | see above |
| `TerminalGui.csproj` | **added** (Moonlight) | wraps the module as a library so vendored code keeps its own warning bar |

Nothing here is a bug fix, so there is nothing to send back upstream yet.
