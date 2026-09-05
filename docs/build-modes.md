# Build modes

Three ways to build the same code, for three different answers to "what is this
artifact for". The managed library is one build for every platform; the two native
modes are one build per operating system and architecture.

| mode | command | produces | per platform? |
|---|---|---|---|
| managed | `dotnet build` | `Moonlight.Core.dll` and the applications | no — one assembly runs anywhere the runtime does |
| NativeAOT | `dotnet publish -p:PublishAot=true -r <rid>` | the two applications, and `Core.Abi` as a shared library | yes |
| bflat | `bflat build … --stdlib DotNet` | a small shared library | yes |

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
bflat build $(find src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*') \
    GlobalUsings.cs \
    --target Shared -o libmoonlight.so \
    --stdlib DotNet --no-reflection --no-globalization --no-stacktrace-data
```

Those four flags are the configuration, not a choice among several: reflection off
is the point, and the other three cost nothing a wallet uses.

Measured on the crypto, serialization, RingCT and logging layers — 104 files, with
nothing else rooted:

| | |
|---|---|
| `--stdlib DotNet` | 1170 KB |
| `--no-reflection` | 964 KB |
| `+ --no-globalization --no-stacktrace-data` | 638 KB |
| the same, with JSON reading rooted | 783 KB |

The whole library including `Core.Abi` comes to 2109 KB, against 2726 KB for the
same sources through NativeAOT. Both were driven through a full scan from Python
over ctypes, so the difference is size rather than behaviour.

Two things to know before using it.

**No MSBuild means no implicit usings.** A `GlobalUsings.cs` holding what
`<ImplicitUsings>enable</ImplicitUsings>` would have generated has to be passed
alongside the sources.

**No source generators.** `JsonSerializerContext` does not exist under bflat, and
it would not help anyway: measured, `JsonSerializer` throws with reflection
disabled, while `Utf8JsonReader`, `Utf8JsonWriter` and `JsonDocument` all work.
That is why `SyncEngine` reads JSON by hand.

`--stdlib Zero` is not a target for this wallet. It has no `System.Exception` at
all, and error handling here is exceptions at every layer — a malformed blob must
be impossible to swallow quietly. A crypto-only library could be built that way,
and that is a separate thing to want.

## Why HttpClient stays out of the small builds

Measured: rooting `DaemonClient` takes the binary from 1.5 MB to 4.3 MB. HttpClient
costs about 2.8 MB, more than everything else together.

It is not in the linked library because it does not need to be. `SyncEngine` does
no I/O — it says what to send, the host sends it — so a build that never touches
`HttpSync` never pays for HTTP. See [architecture](architecture.md).
