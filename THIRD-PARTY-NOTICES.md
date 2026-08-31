# Third-party notices

MOONLIGHT is MIT ([LICENSE](LICENSE)). It contains code from the projects below; each
`Vendor/` dir has a `VENDORED.md` with the exact commit and local changes.

| component | location | licence | upstream |
|---|---|---|---|
| MoneroRing — ref10 | `src/Crypto.Ed25519/Vendor/Ref10/` | MIT | _TBD_ |
| MoneroRing — crypto layer | `src/Crypto/Vendor/MoneroRing/` | MIT | _TBD_ |
| MoneroSharp — mnemonic, Base58, prefixes | `src/Wallet/Vendor/MoneroSharp/` | MIT | _TBD_ |
| Terminal.Gui v1 (SharpOS fork) | `apps/Tui/Vendor/TerminalGui/` | MIT | daniilvaino/SharpOS @ `0c00476` |
| XtermSharp — `CharWidth.cs` only | `apps/Tui/Vendor/TerminalGui/XtermSharp/` | MIT | daniilvaino/XtermSharp @ `1bed529`, from migueldeicaza/XtermSharp |

XtermSharp's MIT notice carries four copyright lines — the xterm.js authors, SourceLair,
Christopher Jeffrey and Miguel de Icaza — and all four travel with the code.
| monero — `tests/crypto/tests.txt` | `tests/vectors/tests.txt` | BSD-3 | monero-project/monero @ `78fb311eb` |
| monero-oxide — test vectors | `tests/vectors/{clsag,blocks,addresses}/` | MIT | monero-oxide/monero-oxide @ `731657a` |

Ported by hand, no code copied: `monero/src/ringct` (BSD-3).

<!-- Append each component's verbatim LICENSE below at import time. -->
