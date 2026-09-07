# Hard-fork policy

Target the current consensus rules; refuse to build a tx we can't prove valid. Never
guess at an unimplemented rule.

Fork impact by area:

| area | typical change |
|---|---|
| `RingCT` | new/changed proof system — the big one |
| `Serialization` | tx version, field layout |
| `Wallet/Fees.cs` _(planned)_ | weight/clawback formula |
| `Wallet/Decoys.cs` _(planned)_ | selection distribution |
| `Node` | RPC and binary endpoints |

## FCMP++

Replaces ring signatures (so CLSAG and decoy selection). When it lands: keep CLSAG for
verifying old txs and restoring old wallets; add the new proof system alongside, gated on
fork version; port from C++, cross-check vs monero-oxide; vectors first.

Version-dependent behaviour sits behind an explicit fork-version check — never a date,
never a "current" default. Unknown future versions are an error.
