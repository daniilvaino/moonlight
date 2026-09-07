# Purity policy

## Rule

`src/`, `apps/Cli/`, `apps/Tui/` are **sterile**:

- 0 `PackageReference` — restores fully offline.
- 0 `DllImport` / `LibraryImport` / `NativeLibrary` — managed only.
- `UnmanagedCallersOnly` in `src/Core.Abi` and nowhere else. Direction is the whole
  of the rule: the three above reach for native code we would then depend on, which
  is what costs us one artifact that runs wherever the runtime does. An export only
  lets somebody else call us and adds no dependency at all. Confined to one project
  so the exemption stays small enough to read, and the gate checks that.
- 0 native assets (`runtimes/*/native`) in the restore graph.

Outside the gate: `tests/**`, `apps/Gui.Demo/`. Nothing sterile may reference the demo.

Target is `net8.0`. Everything is AOT-compatible (`IsAotCompatible`, trim/AOT analyzers on);
`PublishAot` is set only on `apps/Cli` and `apps/Tui`, so the ordinary build keeps working.

## Enforcement

1. `Directory.Build.targets` fails any sterile project declaring a `PackageReference`.
2. `tools/purity-gate.ps1` (run in CI) scans for interop attributes, `PackageReference`
   in project files, native assets in `project.assets.json`, and missing `VENDORED.md`.

```
pwsh tools/purity-gate.ps1
```

## Vendored code

Every `Vendor/` tree carries a `VENDORED.md`: upstream repo, commit, licence, full list
of differences. Vendored code is not held to our warning bar — relax it via a
`Directory.Build.props` in the `Vendor/` folder, or a `#pragma warning disable` header
when it compiles into one of our projects (list that as a difference).

## Licences

Verified on the clones in `work/`:

| donor | licence | use |
|---|---|---|
| MoneroRing | MIT | copy |
| MoneroSharp | MIT | copy |
| QRCoder | MIT | copy (vendored as source, never from NuGet — `apps/Tui` is sterile) |
| Terminal.Gui fork | MIT | copy |
| monero-csharp, monero-lws-csharp | MIT | copy (RPC models) |
| monero-oxide | MIT, per crate | copy — every crate checked, all MIT |
| p2pool-consensus | MIT | copy |
| ZkpSharp | see repo | read only for now |
| monero | BSD-3 | `tests.txt` copied with attribution; `src/ringct` hand-ported |
| **skunkworks** | **GPL-3** | **read only — never copy** |
| **decentralized-message-queue (Determ)** | **multi-licence, pending counsel** | **read only — never copy** |
| **CantiLib** | **AGPL** | **read only — never copy** |
