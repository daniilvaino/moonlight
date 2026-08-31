# Known upstream bugs

Bugs in donor code. On re-import at a newer commit, re-apply every fix here and note it
in that folder's `VENDORED.md`.

Entry format: symptom · cause · fix · upstream PR · vector that catches it.

## MoneroSharp — `BigInteger`

_TBD during vendoring._ Known class: .NET `BigInteger` is little-endian and **signed**, so
round-tripping 32-byte scalars/keys drops leading zeros and flips sign when the top bit is set.
