# Vendored: MoneroSharp (narrow slice)

| | |
|---|---|
| repo | https://github.com/rohanrhu/MoneroSharp |
| commit | `98bc40f` |
| licence | MIT |
| imported | 2026-08-31 |
| scope | the English word list, and nothing else so far |

Explicitly **not** taken:

- the bundled NaCl — it duplicates `Crypto.Ed25519/Vendor/Ref10`, and two ed25519
  backends in one wallet is one too many;
- Base58 — it routes through LINQ and `BitConverter`, which reads the machine's byte
  order. Writing the encoder was shorter than vendoring one and fixing it;
- the mnemonic code — ported from `electrum-words.cpp` instead. The word list is the
  part worth taking; the arithmetic around it is fifteen lines.

The word list was checked word for word against monero's own `src/mnemonics/english.h`:
all 1626 identical.

Re-apply the fixes from [known-upstream-bugs.md](../../../../docs/known-upstream-bugs.md)
if any further code is taken.

## Differences from upstream

| file | change | reason |
|---|---|---|
| | | |
