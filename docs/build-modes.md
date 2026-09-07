# Build modes

Three ways to build the same code, for three different answers to "what is this
artifact for". The managed library is one build for every platform; the two native
modes are one build per operating system and architecture.

| mode | command | produces | per platform? |
|---|---|---|---|
| managed | `dotnet build` | `Moonlight.Core.dll` and the applications | no — one assembly runs anywhere the runtime does |
| NativeAOT | `dotnet publish -r <rid>` | the two applications, and `Core.Abi` as a shared library | yes |
| bflat | `bflat build … --stdlib DotNet` | the same three, smaller | yes |

Measured on `win-x64`, and each one run rather than only built:

| | NativeAOT | bflat |
|---|---|---|
| `Core.Abi` | 2726 KB | 1900 KB |
| `moonlight` (Cli) | 6868 KB | 5147 KB |
| `moonlight-tui` | 7420 KB | 5521 KB |

`apps/Gui.Demo` is managed only: it is an Avalonia application, and outside the
purity gate on purpose.

## What checks what

| | runners |
|---|---|
| managed build, the whole suite, NativeAOT applications | `ci` — linux x64 and arm64, windows x64, macOS arm64 |
| the C interface, against the artifact just built | `ci`, every runner |
| the same, plus all three bflat artifacts | `bflat` — linux x64 and arm64 |
| a hermetic build of the applications, the suite, the gate | `nix` — one runner per system the flake claims |
| the purity gate, and that a sterile restore works with no network | `purity-gate` |

`tests/Abi.Native` is the one thing that opens the door rather than testing the
room behind it. It loads the shared library that was just built, looks up every
function `moonlight.h` declares, and drives a sweep through them. The managed tests
cannot see a renamed entry point or a header that has drifted from the exports —
both leave them green and every foreign caller broken. It is not run by
`dotnet test`, because it needs a published library to exist and would otherwise
pass by finding nothing.

## Managed

What the three applications use, and what the tests run against. Nothing platform
specific, no publish step, and the assembly is the same bytes on every machine —
so it is built once.

## NativeAOT

`apps/Cli` and `apps/Tui` set `PublishAot`, so `dotnet publish -r <rid>` gives a
single executable with no runtime to install.

`src/Core.Abi` publishes as a native shared library:

```sh
dotnet publish src/Core.Abi -c Release -r linux-x64
```

`src/Core.Abi` sets `PublishAot` and `NativeLib` in the project rather than on the
command line, because `NativeLib` alone quietly produces an ordinary managed
publish that looks like it worked.

The `-r` is required here and not for the applications. Publishing an application
with `PublishAot` infers the host's identifier; publishing a shared library refuses
to, with `RuntimeIdentifier is required for native compilation`.

On Windows the link step shells out to `vswhere.exe` by name. It is installed at
`C:\Program Files (x86)\Microsoft Visual Studio\Installer`, which is not on PATH in
an ordinary shell, and without it the build fails at the last step with MSB3073 —
having compiled everything first.

Targets we build for: `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64`. The .NET
10 SDK is not packaged for `x86_64-darwin`, which is why the flake leaves it out.

## bflat

[bflat](https://github.com/bflattened/bflat) compiles C# straight to native code
with no SDK and no MSBuild. It is how the library gets small.

```sh
bflat build $(find src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \
                       -not -path '*/Core.Http/*') \
    GlobalUsings.cs \
    --target Shared -o libmoonlight.so \
    --stdlib DotNet --no-reflection --no-globalization --no-stacktrace-data
```

`Core.Http` is left out on purpose: the library is for a host that does its own
networking, and excluding it is what keeps `HttpClient` out of the binary.

Those four flags are the configuration, not a choice among several: reflection off
is the point, and the other three cost nothing a wallet uses.

On Linux the release needs two things from the system that it does not carry, and
the first is reported misleadingly.

Its object writer links against libc++ — the x64 archive ships `libc++.so.1`
without `libc++abi.so.1`, the arm64 archive ships neither — and a missing
dependency of the object writer is reported as the object writer itself being
missing, which reads like a broken download.

Its bundled linker on arm64 wants `libtinfo.so.5`; the x64 one references no such
library. Ubuntu 24.04 ships only ncurses 6, and a symlink is not enough — the
symbols are versioned, so the loader then refuses with
`NCURSES_TINFO_5.0.19991023 not found`. The `libtinfo5` compatibility package is
still in the archive and is what to install.

`.github/workflows/bflat.yml` does both.

Measured on the crypto, serialization, RingCT and logging layers — 104 files, with
nothing else rooted:

| | |
|---|---|
| `--stdlib DotNet` | 1170 KB |
| `--no-reflection` | 964 KB |
| `+ --no-globalization --no-stacktrace-data` | 638 KB |
| the same, with JSON reading rooted | 783 KB |

Two things to know before using it.

**No MSBuild means no implicit usings.** A `GlobalUsings.cs` holding what
`<ImplicitUsings>enable</ImplicitUsings>` would have generated has to be passed
alongside the sources. For the terminal application that file also needs the
aliases `TerminalGui.props` declares — `BitArray`, `Encoding`.

**bflat's own compiler is older than our language.** It is Roslyn from the .NET 8
era, and the vendored Terminal.Gui uses C# 14 extension members, which it reports
as a stray brace. The language moved but the IL did not: `net8.0` is still `net8.0`.
So a project that outruns bflat's compiler is built with our SDK and handed over as
an assembly —

```sh
dotnet build apps/Tui/Vendor/TerminalGui -c Release
bflat build <sources> -r .../Moonlight.Vendor.TerminalGui.dll -o moonlight-tui
```

— because `bflat build` takes `-r, --reference`, and its compiler back end reads IL
like any other. This works for anything that hits a language-version wall, not just
this one project.

**No source generators** — which is why the tree no longer has one. bflat does not
run them, so a `JsonSerializerContext` simply does not exist there and the file
declaring it will not compile; feeding it pre-generated sources would make the
build depend on having run `dotnet build` first, in the right order. It would not
have helped anyway: measured, `JsonSerializer` throws with reflection disabled,
while `Utf8JsonReader`, `Utf8JsonWriter` and `JsonDocument` all work. So
`WalletDocument`, `SyncEngine` and `DaemonClient` read and write JSON by hand, and
the command above is the whole command.

`--stdlib Zero` is not a target for this wallet. It has no `System.Exception` at
all, and error handling here is exceptions at every layer — a malformed blob must
be impossible to swallow quietly. A crypto-only library could be built that way,
and that is a separate thing to want.

## With Nix

`flake.nix` builds the two NativeAOT applications hermetically — pinned SDKs,
pinned NuGet, no network during the build. The shared library is not in the
flake yet.

```sh
nix build .#moonlight          # apps/Cli  -> result/bin/moonlight
nix build .#moonlight-tui      # apps/Tui  -> result/bin/moonlight-tui
nix run   .# -- version
nix flake check                # both apps, the test corpus, the purity gate
nix develop                    # SDK 10 + SDK 8 + pwsh, then build by hand
```

The dev shell carries no C toolchain on macOS: ILC calls `clang`, `dsymutil` and
`strip` by name, and Nixpkgs' `strip` rejects the flags it passes, so Xcode's
command line tools stay in front. The packages build hermetically on all three
platforms regardless.

NuGet is locked in `nix/deps/*.json`. Only `tests/` pulls packages; the two apps
pull none, so their locks are empty and have to stay that way. The ILCompiler is
not an exception — it comes from the combined SDK, and listing it as well makes
two inputs offer the same package to `configureNuget`, which links them with a
bare `ln -s` and fails on the second. After changing a `PackageReference`,
regenerate the matching lock (`.#moonlight-tui` for `tui.json`,
`.#checks.<system>.tests` for `tests.json`):

```sh
$(nix build --no-link --print-out-paths .#moonlight.passthru.fetch-deps) "$PWD/nix/deps/cli.json"
```

## Why HttpClient stays out of the small builds

Measured: rooting `DaemonClient` takes the binary from 1.5 MB to 4.3 MB. HttpClient
costs about 2.8 MB, more than everything else together.

It is not in the linked library because it does not need to be. `SyncEngine` does
no I/O — it says what to send, the host sends it — so a build that never touches
`HttpSync` never pays for HTTP. See [architecture](architecture.md).
