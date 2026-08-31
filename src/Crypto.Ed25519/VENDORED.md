# Vendored: ref10 (via MoneroRing → MoneroSharp → Chaos.NaCl)

| | |
|---|---|
| repo | https://github.com/NeoSoft99/MoneroRing — `MoneroRing.Crypto/MoneroSharp/NaCl/Ed25519Ref10/` |
| commit | `642e443` |
| licence | **public domain** (Chaos.NaCl by CodesInChaos; ref10 itself by djb, from SUPERCOP) |
| imported | 2026-08-31 |
| scope | 45 files: `fe_*`, `ge_*`, `sc_*`, `FieldElement`, `GroupElement`, base tables |

Taken through MoneroRing rather than from MoneroSharp directly: MoneroRing already
carries this copy, and its own Monero extensions are written against it. Copying the same
files from two places is how two copies drift apart.

Not copied — NaCl's ed25519 signing and X25519, which Monero does not use and which drag
in `Sha512`/`CryptoBytes`: `keypair.cs`, `open.cs`, `sign.cs`, `scalarmult.cs`.

Namespace stays `MoneroSharp.NaCl.Internal.Ed25519Ref10`. Renaming it would touch every
file and break the next re-import for no gain.

The Monero-specific extensions (`ge_fromfe_frombytes_vartime`, `ge_mul8`, `ge_scalarmult`,
`sc_mulsub`, `sc_reduce32`, …) live in `src/Crypto/Vendor/MoneroRing/` instead of here:
upstream declares them as part of the same `RingSig` partial class as the crypto layer,
and splitting them out would mean editing every one of them.

`Scalar.cs` / `Point.cs` are still to be written, and are ours, not vendored.

## Differences from upstream

| file | change | reason |
|---|---|---|
| `keypair.cs`, `open.cs`, `sign.cs`, `scalarmult.cs` | not copied | ed25519 signing / X25519; unused, and they need Sha512 + CryptoBytes |
| `Vendor/.editorconfig` | **added** (Moonlight) | analyzers and nullable warnings off for vendored code; our own files keep the full bar |
