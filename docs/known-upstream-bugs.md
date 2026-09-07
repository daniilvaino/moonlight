# Known upstream bugs

Bugs in donor code. On re-import at a newer commit, re-apply every fix here and note it
in that folder's `VENDORED.md`.

Entry format: symptom · cause · fix · upstream PR · vector that catches it.

## MoneroSharp — `BigInteger`

**Not applicable to what was taken, and kept as a warning for what might be.** The
slice vendored is the English word list and nothing else, so none of the arithmetic
came with it — see [VENDORED.md](../src/Wallet/Vendor/MoneroSharp/VENDORED.md).

The hazard, if more is ever taken: .NET `BigInteger` is little-endian and
**signed**, so round-tripping 32-byte scalars or keys drops leading zeros and flips
sign when the top bit is set. Base58 and the mnemonic arithmetic were written here
rather than imported partly for that reason.
