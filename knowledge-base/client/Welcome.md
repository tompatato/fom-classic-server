# Welcome — 2006 Client RE Vault

This is the durable record of *Face of Mankind* **2006 client** internals. Every
reverse-engineering deep dive gets written up here as a Markdown note — offsets,
RVAs, wire formats, and a `## Reproduce` section. Ad-hoc findings in chat are lost;
notes here are not.

Conventions (see `../../docs/re-methodology.md`):

- Link related notes with `[[wikilinks]]`.
- Include offset/RVA tables for structs and packets.
- End with a `## Reproduce` section: the exact commands that regenerate the
  finding.
- Mark anything not yet verified against the live client *(unverified)*.
- Clean room: justify from the 2006 binary. When FotD prior art seeded a
  hypothesis, note it, then confirm before recording as fact.

Start new notes from [[_Template]].

## Index

_Nothing documented yet — Phase 0 recon in progress. Expected early notes:_

- `Network Library` — which net lib + version the client uses (recon Step 1). ⭐
- `Login Handshake` — connect + login wire format (recon Step 2).
- `Client Architecture` — module/engine split.
- `Packet Transport` — how packets are framed and dispatched.

(Add entries here as notes land. Compare against the FotD vault —
`knowledge-base/client/` in the sibling `fotd-server` checkout — for the shape
these mature into, as a hypothesis source, not ground truth.)
