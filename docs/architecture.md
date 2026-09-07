# Architecture

Pure managed C#, `net8.0`, AOT-ready. Layers depend downward only.

```
Crypto.Ed25519   ref10 fe_*/ge_*/sc_* (vendored)
Diagnostics      the log
Crypto           Scalar, Point; Keccak (legacy pad), CRC32, VarInt, derivations, key images,
                 hash_to_ec/scalar, view tags, CryptoNote signatures
Serialization    tx/block parsers, Merkle root, tx_extra, Epee
RingCT           Pedersen, ECDH, CLSAG, Bulletproofs+, MultiExp
Node             monerod JSON-RPC; /getblocks.bin
Wallet           keys, accounts, addresses, subaddresses, scanner, storage
Core             SyncEngine, WalletSession, amounts, daemon address
   Core.Http     HttpSync, WalletConnection — the socket
   Core.Abi      the C interface — exports only
apps             Cli · Tui · Gui.Demo (outside the gate)
```

Applications reference `Core.Http` and nothing else; the C interface references
`Core`. The two know nothing about each other, which is what keeps an HTTP stack
out of a library somebody links into their own application.

The layers stay separate projects rather than folders in one assembly because
"dependencies point downward" is checked by the compiler only while they are. That
rule has already moved two things: `VarInt` into `Crypto`, because view tags need
it, and `Scalar`/`Point` with it.

| project | source |
|---|---|
| `Crypto.Ed25519` | `Vendor/Ref10` ← MoneroRing (Chaos.NaCl, public domain) |
| `Crypto` | `Vendor/MoneroRing` (Keccak and RNG swapped for ours); `Keccak.cs`, `VarInt.cs`, `ViewTag.cs`, `Scalar.cs`, `Point.cs` ours. Scalar and Point live here, not in Crypto.Ed25519: they need `sc_check`/`sc_reduce32`/`hash_to_ec`, which are Monero's additions to ref10 |
| `Serialization` | ours; ref `monero/src/cryptonote_basic`, Epee cross-checked vs monero-oxide |
| `RingCT` | hand port of `src/ringct/*.cc`; CLSAG first in C#; BP+ transcript verified step by step vs monero-oxide |
| `Node` | ours. JSON-RPC over `HttpClient`; `DaemonClient` still uses source-generated `System.Text.Json`, which is the last generator in the tree |
| `Wallet` | `Vendor/MoneroSharp` (English word list only); rest ours |
| `Core` | ours. The engine reads JSON by hand — measured, `JsonSerializer` throws with reflection disabled where `Utf8JsonReader` does not |

## Following the chain

`SyncEngine` does no I/O. Ask it for the next request, send that however you like,
hand back the answer:

```csharp
SyncRequest? Next();
void Supply(ReadOnlySpan<byte> answer);
bool Failed(Exception error);
```

The transport belongs to the host. A wallet linked into a Swift or Rust application
should use that application's networking — its trust store, its proxy settings, its
idea of a timeout — and once the socket is the host's, the loop may as well be too:
a thread of ours whose only job is to call back into the host would be a thread for
nothing. It is not async for the same reason: there is nothing to await when the
caller does the waiting, and an interface with no `Task` in it crosses a C ABI
unchanged.

What stays in the engine is everything that is actually hard and worth not writing
twice: the block locator, the batching, the order blocks must arrive in, settling
an offline restore date, and the rule that a batch which does not advance means
stop rather than ask again.

`HttpSync` is that engine driven over `HttpClient`, so the applications await what
they always awaited. Both faces move the same engine — the session owns it, and a
date settled through the C interface is a date the next save writes down.

## The wallet file

JSON with one opaque field. Settings — nodes, where to fetch more, where a price
comes from, the subaddress lookahead — are readable and editable by hand. Everything
else lives in `secret`: the two keys, and the outputs a scan found.

The split is not "keys versus the rest". An output says what the wallet holds, and
its key image identifies that wallet's spends on the chain, so the scan results are
as revealing as the keys and stay inside.

Settings are outside the authenticated envelope so they can be edited, which means
they can also be changed by somebody else — a node quietly redirected is a privacy
attack. Their hash is stored inside the blob, so the wallet reports that they
changed rather than either refusing to open or saying nothing.

Both halves are meant to grow. Unknown JSON fields are ignored, and the hash is
only compared between files of the same `format`, so adding a setting does not make
every existing wallet look tampered with. Inside the blob, everything after the scan
results is a tagged section — tag, length, bytes — so a reader steps over what it
does not know and a later version can add a field without the order mattering.

Key material is never `byte[]`: `SecretKey`/`PublicKey`/`KeyImage`/`Commitment` are
distinct types with no implicit conversions.

Vectors before code — `tests/vectors/tests.txt` via `tests/Crypto.Tests/TestVectors.cs`.

See [purity-policy.md](purity-policy.md), [hardfork-policy.md](hardfork-policy.md).
