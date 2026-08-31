# Third-party notices

MOONLIGHT is MIT ([LICENSE](LICENSE)). It contains code from the projects below; each
`Vendor/` dir has a `VENDORED.md` with the exact commit and local changes.

| component | location | licence | upstream |
|---|---|---|---|
| ref10 via MoneroRing (Chaos.NaCl / djb) | `src/Crypto.Ed25519/Vendor/Ref10/` | public domain | NeoSoft99/MoneroRing @ `642e443` |
| MoneroRing — crypto layer | `src/Crypto/Vendor/MoneroRing/` | MIT | NeoSoft99/MoneroRing @ `642e443` |
| MoneroSharp — English word list | `src/Wallet/Vendor/MoneroSharp/` | MIT | rohanrhu/MoneroSharp @ `98bc40f` |
| Terminal.Gui v1 (SharpOS fork) | `apps/Tui/Vendor/TerminalGui/` | MIT | daniilvaino/SharpOS @ `0c00476` |
| XtermSharp — `CharWidth.cs` only | `apps/Tui/Vendor/TerminalGui/XtermSharp/` | MIT | daniilvaino/XtermSharp @ `1bed529`, from migueldeicaza/XtermSharp |
| monero — `tests/crypto/tests.txt` | `tests/vectors/tests.txt` | BSD-3 | monero-project/monero @ `78fb311eb` |
| monero-oxide — test vectors | `tests/vectors/{clsag,blocks,addresses}/` | MIT | monero-oxide/monero-oxide @ `731657a` |

XtermSharp's MIT notice carries four copyright lines — the xterm.js authors, SourceLair,
Christopher Jeffrey and Miguel de Icaza — and all four travel with the code.

Ported by hand, no code copied: `monero/src/ringct` (BSD-3).

<!-- Append each component's verbatim LICENSE below at import time. -->
