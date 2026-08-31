# MOONLIGHT

A Monero wallet in pure managed C#. No P/Invoke, no native binaries, no NuGet packages in
the parts that hold keys — one artifact that runs wherever the runtime does, and every
byte of it readable in this repository.

**Status: skeleton.** The scaffold, the purity gate, the TUI and the test corpus are in.
No crypto yet — that is the next commit, and it gets written against 5945 vectors that
are already here.

## Build

```
dotnet build Moonlight.slnx
dotnet test  Moonlight.slnx
dotnet publish apps/Cli -c Release      # NativeAOT, ~1.2 MB
dotnet publish apps/Tui -c Release      # NativeAOT, ~1.9 MB
pwsh tools/purity-gate.ps1
```

Targets `net8.0`, compiled as C# 14 — so **SDK 10 is required to build**, though nothing
newer than .NET 8 is required to run. Everything is AOT-compatible with the trim and AOT
analyzers on; `PublishAot` is set only on the two apps, so the ordinary build stays
ordinary.

## Layout

```
src/                 sterile: 0 packages, 0 P/Invoke
  Crypto.Ed25519     ref10 field/group/scalar arithmetic; Scalar, Point
  Crypto             Keccak (legacy pad), CRC32, derivations, key images,
                     hash_to_ec/scalar, view tags, CryptoNote signatures
  Serialization      VarInt, tx/block parsers, Epee portable storage
  RingCT             Pedersen, ECDH, CLSAG, Bulletproofs+, MultiExp
  Node               monerod JSON-RPC; /getblocks.bin over Epee
  Wallet             keys, accounts, addresses, scanner, decoys, fees,
                     tx builder, encrypted storage
apps/
  Cli                sterile
  Tui                sterile; vendored Terminal.Gui v1
  Gui.Demo           outside the gate — thin showcase, packages allowed
tests/               outside the gate
  vectors/           the corpus (8.4 MB)
work/                git-ignored scratch for donor clones
```

Layers depend downward only. Key material never travels as `byte[]`: `SecretKey`,
`PublicKey`, `KeyImage` and `Commitment` are separate types with no implicit conversions
between them — the compiler as the first line of defence against the classic footguns.

## Purity

`src/`, `apps/Cli` and `apps/Tui` allow **0 `PackageReference`, 0 `DllImport`, and no
native assets in the restore graph**. The sterile tree restores fully offline.

Enforced twice, because a rule nothing checks is a preference:

1. `Directory.Build.targets` fails the build of any sterile project that declares a package.
2. `tools/purity-gate.ps1` scans the sources and the restore graph in CI, and refuses a
   `Vendor/` directory with no `VENDORED.md`.

`tests/` and `apps/Gui.Demo` are outside the gate on purpose. Nothing sterile references
the demo. Details in [docs/purity-policy.md](docs/purity-policy.md).

## TUI

`apps/Tui` vendors Terminal.Gui v1 from the [SharpOS](https://github.com/daniilvaino/SharpOS)
fork — the curated compile-in module, which has no `ConsoleDrivers` and therefore no
P/Invoke, and replaces NStack with its own `Rune`. The wallet is meant to run on SharpOS
eventually, so the kernel driver stays in the tree; `MoonlightHost` picks the input side.

| | `console` (default) | `sharpos` |
|---|---|---|
| size | `Console.WindowWidth/Height`, `COLUMNS`/`LINES` | `AppConsole.TryGetSize` |
| loop, keys | `ConsoleMainLoop`, `ConsoleKeyMap` | `SharpOSMainLoop`, `SharpOSKeyMap` |

Rendering is shared: the driver writes ANSI through `Console.Write` on both.

## Tests

Vectors before code. The harness for `tests/crypto/tests.txt` was the first thing
committed, so every crypto file since is born under the whole corpus.

| | |
|---|---|
| `tests.txt` | 5945 lines, 20 operations, from monero (BSD-3) |
| grammar | transcribed from monero's reference runner; **every line** checked against it |
| corpus | CLSAG rings, real block transactions and address vectors from monero-oxide (MIT) |

Corpus tests assert the vector files themselves are intact — a truncated vector file is
the one failure that makes every later test pass for free. Inventory, including what we
do *not* have vectors for, in [docs/test-corpus.md](docs/test-corpus.md).

## Donors

Steal what is stealable; port what is not. Every `Vendor/` tree carries a `VENDORED.md`
with the upstream commit and a full list of local changes.

| | licence | use |
|---|---|---|
| MoneroRing | MIT | ref10 + crypto layer — copied |
| MoneroSharp | MIT | mnemonic, Base58, network prefixes — copied, nothing else |
| Terminal.Gui (SharpOS fork), XtermSharp | MIT | the TUI — copied |
| monero-oxide | MIT (per crate, all checked) | Rust reference; BP+ transcript intermediates |
| monero | BSD-3 | vectors copied; `src/ringct` ported by hand |
| skunkworks, CantiLib, Determ | GPL-3 / AGPL / undecided | **read only — never copied** |

`work/` holds the clones; `pwsh tools/clone-donors.ps1` fetches them all.

## Docs

[architecture](docs/architecture.md) ·
[purity policy](docs/purity-policy.md) ·
[test corpus](docs/test-corpus.md) ·
[hard forks](docs/hardfork-policy.md) ·
[upstream bugs](docs/known-upstream-bugs.md) ·
[notices](THIRD-PARTY-NOTICES.md)

## Roadmap

1. ref10 and the crypto layer, against `tests.txt`.
2. Serialization: VarInt, tx and block parsers, Epee.
3. RingCT: Pedersen and ECDH, then CLSAG — the first implementation in C# — then
   Bulletproofs+, transcript verified step by step against monero-oxide.
4. Wallet: scanner, decoys, fees, tx builder, encrypted storage.
5. Integration: stagenet end to end, plus our own verify-chain over real blocks.

MIT licensed.
