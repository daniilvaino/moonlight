# Vendored: QRCoder (generator only)

| | |
|---|---|
| repo | https://github.com/Shane32/QRCoder |
| commit | `faa9fb8` |
| licence | MIT (© 2013-2025 Raffael Herrmann) |
| imported | 2026-09-01 |
| scope | `QRCodeGenerator.cs`, `QRCodeData.cs`, the `QRCodeGenerator/` partials, `BitArrayExtensions.cs`, `DataTooLongException.cs` |

Only the part that produces the module matrix. None of the renderers are here —
`ArtQRCode`, `BitmapByteQRCode`, `Base64QRCode` and the rest draw images, and the
TUI draws with block characters instead. The `PayloadGenerator` tree is not here
either: a Monero address is a string, and vCard, WiFi and Girocode payloads are
not something a wallet has any use for.

Its build props supply global usings that the module list reproduces:
`BitArray`, `System.Globalization`, `System.Text`, `System.Text.RegularExpressions`.

## Differences from upstream

| file | change | reason |
|---|---|---|
| `QRCodeGenerator.cs` | removed the four `CreateQrCode`/`GenerateQrCode` overloads taking `PayloadGenerator.Payload` | the payload tree is not vendored; the string overloads are what a wallet uses |
| renderers, `PayloadGenerator/`, `Extensions/` beyond `BitArrayExtensions` | not copied | image output and payload builders, neither of which a terminal needs |
