<p align="center">
  <img src="media/icon-purple.svg" width="96" alt="moonlight — a purple hexagon with a white letter M">
</p>

<h1 align="center">moonlight</h1>

<p align="center">a Monero wallet in pure managed C#, from the curve up</p>

<p align="center">
  <img src="https://img.shields.io/badge/pure_c%23-100%25-7c3aed?style=flat-square&labelColor=30363d" alt="pure C#: 100%">
  <img src="https://img.shields.io/badge/p%2Finvoke-0-7c3aed?style=flat-square&labelColor=30363d" alt="P/Invoke: 0">
  <img src="https://img.shields.io/badge/packages-0-7c3aed?style=flat-square&labelColor=30363d" alt="packages: 0">
  <img src="https://img.shields.io/badge/vectors-5539%2F5945-f2640a?style=flat-square&labelColor=30363d" alt="vectors replayed: 5539 of 5945">
  <img src="https://img.shields.io/badge/license-MIT-4b5563?style=flat-square&labelColor=30363d" alt="license: MIT">
</p>

No P/Invoke, no native binaries, no NuGet packages in the parts that hold keys — one artifact that runs wherever the runtime does, and every byte of it readable in this repository.

**Status: early.** Crypto, serialization, the daemon client, RingCT and a scanning wallet are in. The TUI syncs a wallet to the mainnet tip — pruned blocks, so the proofs a scan never reads are not fetched — and finds real payments to its own addresses. The CLSAG signatures and the Bulletproof+ range proof of a real transaction verify against this code, and it reads that transaction's change amount back. All of it is also a linkable library with a C interface, so a user interface in any language can drive the same wallet. What is missing is the other half of spending: decoy selection, fees and the transaction builder. Nothing here spends money yet.

## Build

```sh
dotnet build Moonlight.slnx
dotnet test  Moonlight.slnx
dotnet publish apps/Cli     -c Release  # NativeAOT
dotnet publish apps/Tui     -c Release  # NativeAOT
dotnet publish src/Core.Abi -c Release -r linux-x64   # a shared library, and it
                                                      # needs the identifier given
pwsh tools/purity-gate.ps1
bflat build ...                         # no SDK, no MSBuild, smaller — docs/build-modes.md
```

Targets net8.0, compiled as C# 14 — SDK 10 to build, nothing newer than .NET 8 to run. Everything is AOT-compatible with the trim and AOT analyzers on.

Three modes build the same code — managed, NativeAOT, and bflat, which gets the shared library down to 1900 KB. Sizes, flags, and how each artifact was run rather than only built: [docs/build-modes.md](docs/build-modes.md).

With Nix it is hermetic — pinned SDKs, no network during the build:

```sh
nix build .#moonlight          # apps/Cli  -> result/bin/moonlight
nix build .#moonlight-tui      # apps/Tui  -> result/bin/moonlight-tui
nix flake check                # both apps, the test corpus, the purity gate
```

## Layout

```
src/                 sterile: 0 packages, 0 P/Invoke
  Crypto.Ed25519     vendored ref10 field/group/scalar arithmetic
  Diagnostics        the log
  Crypto             Scalar, Point; Keccak (legacy pad), VarInt, view tags,
                     derivations, key images, CryptoNote signatures
  Serialization      transaction and block parsers, ids, Merkle root, Epee
  RingCT             Pedersen, ECDH, CLSAG, Bulletproofs+, MultiExp
  Node               monerod JSON-RPC; /getblocks.bin over Epee
  Wallet             keys, accounts, addresses, subaddresses, scanner,
                     restore heights, encrypted storage
  Core               the sync engine, the open wallet, amounts
  Core.Http          the socket: the engine driven over HttpClient
  Core.Abi           the C interface, exports only
apps/
  Cli                sterile
  Tui                sterile; vendored Terminal.Gui v1
  Gui.Demo           outside the gate — the interface, on made-up data so far
tests/               outside the gate
  vectors/           the corpus (8.3 MB)
media/               brand assets
```

```mermaid
flowchart TD
    Tui["apps/Tui"]:::sterile --> TG["Vendor Terminal.Gui"]:::sterile
    Tui --> CoreHttp
    Cli["apps/Cli"]:::sterile --> CoreHttp
    Gui["apps/Gui.Demo"]:::outside --> CoreHttp

    CoreHttp["Core.Http<br/>the socket"]:::sterile --> Core
    Abi["Core.Abi<br/>the C interface"]:::bridge --> Core

    %% Abi is a consumer of Core like the three above it, not a layer under them.
    %% The invisible link only lifts it into their row; it draws nothing.
    Abi ~~~ CoreHttp

    Core["Core<br/>sync engine, open wallet"]:::sterile --> Wallet["Wallet"]:::sterile
    Wallet --> RingCT["RingCT"]:::sterile
    Wallet --> Node["Node"]:::sterile
    RingCT --> Ser["Serialization"]:::sterile
    Node --> Ser
    Ser --> Crypto["Crypto"]:::sterile
    Crypto --> Ed["Crypto.Ed25519"]:::sterile
    Node --> Diag["Diagnostics"]:::sterile

    classDef sterile fill:#2b1d4d,stroke:#7c3aed,stroke-width:1px,color:#e9e4f7
    classDef outside fill:#4a2a10,stroke:#f2640a,stroke-width:1px,color:#f7ece4
    classDef bridge fill:#2b1d4d,stroke:#f2640a,stroke-width:2px,color:#e9e4f7
```

Applications reference `Core.Http` and nothing else; `Core.Abi` references `Core`, and the two know nothing about each other. That is what keeps an HTTP stack out of a library somebody links into their own application. Following the chain is a state machine in `Core` that does no I/O — it says what to send, the host sends it, the answer goes back in — so a user interface written in anything can link the library and keep its own networking. Details in [docs/architecture.md](docs/architecture.md).

Layers depend downward only. Key material never travels as `byte[]`: constructing a `Scalar` or a `Point` is the only place bytes are checked, so holding one means holding something the curve will accept.

## Purity

`src/`, `apps/Cli` and `apps/Tui` allow **0** `PackageReference`, **0** `DllImport`, and no native assets in the restore graph. The sterile tree restores fully offline.

Enforced twice, because a rule nothing checks is a preference:

1. `Directory.Build.targets` fails the build of any sterile project that declares a package.
2. `tools/purity-gate.ps1` scans the sources and the restore graph in CI, and refuses a `Vendor/` directory with no `VENDORED.md`.

`Core.Abi` is sterile too — just the one project allowed to export a symbol out of managed code, hence the orange edge in the diagram. `tests/` and `apps/Gui.Demo` are outside the gate on purpose. Nothing sterile references the demo. Details in [docs/purity-policy.md](docs/purity-policy.md).

## TUI

<p align="center">
  <img src="media/tui-dashboard.png" width="720" alt="the moonlight dashboard: address, balance, scan progress caught up at block 3753117, and one output">
</p>

Caught up to the mainnet tip, with a payment found — every byte of that scan done by the code in this repository.

`apps/Tui` vendors Terminal.Gui v1 from the SharpOS fork — the compile-in module: no ConsoleDrivers, no P/Invoke, which is how a whole UI toolkit lives inside the purity gate. That fork, because the wallet is meant to run on SharpOS eventually.

## Tests

Vectors before code. The harness for monero's `tests/crypto/tests.txt` was the first thing committed, so every crypto file since is born under the whole corpus: 5945 lines, 20 operations, 5539 replayed line by line — including monero's deterministic test generator, reproduced byte for byte, draw pattern included. Corpus tests assert the vector files themselves are intact, because a truncated vector file is the one failure that makes every later test pass for free. Inventory in [docs/test-corpus.md](docs/test-corpus.md).

Point `MOONLIGHT_DAEMON` at a node and the chain tests run too: every block must parse to the last byte, every transaction must hash to the id the daemon listed.

## Donors

Steal what is stealable; port what is not. Every `Vendor/` tree carries a `VENDORED.md` with the upstream commit and a full list of local changes.

| donor                                   | licence                      | use                                            |
|-----------------------------------------|------------------------------|------------------------------------------------|
| MoneroRing                              | MIT                          | crypto layer — copied                          |
| ref10, through MoneroRing               | public domain (Chaos.NaCl, djb) | field, group and scalar arithmetic — copied |
| MoneroSharp                             | MIT                          | the English word list — copied, nothing else   |
| Terminal.Gui (SharpOS fork), XtermSharp, QRCoder | MIT                | the TUI — copied                               |
| monero-oxide                            | MIT (per crate, all checked) | Rust reference; BP+ transcript intermediates   |
| monero                                  | BSD-3                        | vectors copied; src/ringct ported by hand      |
| skunkworks, CantiLib, Determ            | GPL-3 / AGPL / undecided     | read only — never copied                       |

`work/` holds the clones; `pwsh tools/clone-donors.ps1` fetches them all.

## Docs

[architecture](docs/architecture.md) · [build modes](docs/build-modes.md) · [purity policy](docs/purity-policy.md) · [test corpus](docs/test-corpus.md) · [hard forks](docs/hardfork-policy.md) · [upstream bugs](docs/known-upstream-bugs.md) · [notices](THIRD-PARTY-NOTICES.md)

## Roadmap

1. ~~ref10 and the crypto layer, against `tests.txt`.~~
2. ~~Serialization: transactions, blocks, ids, Merkle root, Epee.~~
3. ~~Node: monerod JSON-RPC and `/getblocks.bin`.~~
4. ~~RingCT: Pedersen, ECDH, CLSAG, Bulletproofs+ both ways — verification checked against proofs monero made.~~ Pippenger for the multiexp remains.
5. ~~Wallet: seeds, addresses, subaddresses, scanner, balance, encrypted storage.~~ Decoys, fees and the transaction builder remain.
6. ~~One library the applications share, following the chain without owning the socket, offered to C as a linkable artifact — with CI building all three modes and checking the exported symbols against the header.~~ Errors still cross the C boundary as a bare code with no message.
7. Spending: decoy selection, fee and weight, the transaction builder.
8. Integration: stagenet end to end, plus our own verify-chain over real blocks.

---

<p align="center"><img src="media/hex-purple.svg" width="10" alt=""></p>

<p align="center">MIT licensed.</p>
