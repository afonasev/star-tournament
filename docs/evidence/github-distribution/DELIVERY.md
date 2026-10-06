# GitHub distribution W3, 06.10.2026

User chose Program Files with installation UAC; no scope choice. Production icon preserved, both checked choices share final wizard screen, desktop creation is validated/reported, versioned filenames published. Physical Windows installation acceptance is pending.

Public source main: add125f65f440688c9fbb02a734565b87f4f364a (later metadata mirror commits may follow). Player source: 72cf05a7b8d49f0dd94a4e8968fbdda42d940a7a; generated authored differences are preserved as patches. Runtime source did not change during publication. Packaging identities bind exact player content markers, source differences and binary hashes.

Install: https://github.com/afonasev/star-tournament/releases/tag/v0.1.0-test.7
Update: https://github.com/afonasev/star-tournament/releases/tag/v0.1.0-test.8
Legacy archive: https://github.com/afonasev/star-tournament/releases/tag/native-test-archive-2026-10-04
Landing: https://robowar.afonasev.tech/

Checks: 342 EditMode and 207 PlayMode passing, Development and both Release targets succeeded; 28 C# security and 22 Python contracts. Native macOS menu rendered silently from the actual universal Release Player. New and old AuthenticatedSource probes downloaded test.8 using real HTTPS (direct GitHub and legacy VPS relay respectively) and verified signature/size/SHA256. Eight new and twelve legacy GitHub assets have readback identities, twelve old VPS routes returned expected lengths, full representative streams for both platforms matched archived SHA256.

VPS: twelve exact binaries, 1760980643 bytes, removed only after verified archive/cutover. Small metadata and signed envelopes retained; diskless bounded relay runs on loopback4192 under DynamicUser with hardening. Global Caddy config validated and reloaded preserving other sites. No old browser assets reintroduced.

Windows-PC wizard/icon/shortcut/UAC and native install/update/profile/offline device acceptance remain open. These are published unsigned test builds, not production acceptance. Planning acceptance.md names test.7→test.8 scenarios; historical accepted/pending decisions retained independently.
