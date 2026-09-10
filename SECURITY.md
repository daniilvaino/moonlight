# Security

## Reporting

Report privately. Do not open an issue, and do not describe it in a pull request:
this is a wallet, and the people it would reach first are not the ones who should
hear about it first.

Write to **daniil.vaino+security@gmail.com**.

Expect an acknowledgement within a few days. There is no bounty — this is an
unfunded project — and there is credit in the advisory and the release notes for
anyone who wants it. If you would rather not be named, say so and you will not be.

If the report is about a downstream project that vendored this code, tell them too;
this repository has no way to reach their users.

## What is not audited

All of it. No part of this code has been reviewed by anyone outside the project. The
curve arithmetic, the Keccak variant, the ring signatures, the range proofs, the
parsers, the scanner and the encrypted storage are all written here, and the fact
that they agree with monero's test vectors is evidence, not assurance: a vector
corpus proves a thing right where it was looked at.

Treat every release as alpha. Do not point it at a seed phrase controlling funds
you would mind losing.

## What counts

Anything in this list, at any severity, is worth reporting:

- **Key material leaving where it belongs** — a seed, a spend key or a view key
  written to disk unencrypted, reaching the log, crossing the daemon connection, or
  surviving in memory somewhere it was meant to be cleared.
- **Verification that accepts what it should reject** — a CLSAG signature, a
  Bulletproof+ range proof, a key image or a commitment that passes when monero
  would refuse it. A wallet that believes a forged proof believes a payment.
- **Divergence from consensus** — the same block or transaction read differently
  here than by monerod, in a way that changes what the wallet concludes.
- **Scanning that gets ownership wrong** — an output claimed that is not ours, an
  output missed that is, or a key image computed that does not match the one a spend
  would produce.
- **Anything a hostile daemon can do to a client** — a response that makes the
  parser loop, allocate without bound, or throw somewhere that loses a wallet file.
  The connection is untrusted by design and the wallet must survive whatever comes
  back.
- **Privacy loss to the daemon** — a request pattern that tells the node which
  outputs are ours, beyond what the protocol already reveals.
- **Weaknesses in the encrypted storage** — key derivation, nonce reuse, a
  ciphertext whose length or structure says something about what it holds.
- **Timing that depends on secrets** — in the scalar and point arithmetic above all,
  where an attacker who can measure is an attacker who can narrow.

## What does not count yet

- **Anything that requires spending.** There is no transaction builder, no decoy
  selection and no fee calculation, so nothing here signs a transaction that moves
  money. When that arrives this section shrinks and the one above it grows.
- **`apps/Gui.Demo`.** It is a demonstration on made-up data, holds no keys, opens
  no wallet, and is the one part of the tree outside the purity gate. Report a
  finding in it as an ordinary bug.
- **Missing features.** The roadmap in the readme is not a list of vulnerabilities.
- **Errors crossing the C interface as a bare code with no message.** Known, and
  written down in the readme.

## Which versions

The most recent release, and `main`. Nothing older is patched — there is not yet
enough released for a maintained branch to mean anything.
