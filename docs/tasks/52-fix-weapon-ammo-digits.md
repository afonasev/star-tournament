# Weapon ammo digits on Windows

Owner: current Codex chat (2026-11-01). Worktree: `/Users/eaafonasev/.codex/worktrees/fix-ammo-digits/star-tournament`; branch `codex/fix-ammo-digits`; baseline `main@c2c0d43e3687370d62334008576aac6c7918b5e5`. Main already had unrelated GraphicsSettings/QualitySettings and generated files; preserve them.

User report: the weapon-mounted shotgun and rocket counters show maximum or zero on Windows; rocket reserve arc is correct. Expected behavior remains GAME_SPEC.md weapon-local authoritative ammo displays; no product decision changes.

Investigation: runtime supplies current ammo to both displays; initial Metal GPU fixture renders 18/17/12/11/08/07/01/00/100 correctly (2/2 PASS). Original Windows symptom is not reproduced on this host. Both shaders use ternary selection with dynamic component accesses outside the vector bounds in nonselected operands (`_DigitsB[index-4]`, `_DigitsC[index-8]`). Backend flattening makes this unsafe. Replace selection with constant, valid component accesses. Treat as a targeted portability repair, pending confirmation of the Windows symptom.

Affected QA: GPU pixel-change fixture for both real display components; existing ShotgunAmmoDisplayTests and PulseAmmoDisplayTests for firing, owner isolation, empty, restored and switched state. Shared runner with `-batchmode -noaudio -muted`, graphics enabled for captures. No full suite, Player/build/install/release/deploy. Raw evidence: `.local/ammo-digits/`; durable results will be copied to shared planning evidence.

Status: source repair verified, 4/4 affected checks PASS on Metal; nine reference images per weapon remain byte-identical. Windows/Direct3D and human acceptance remain NotRun; no claim that the Windows symptom is reproduced or confirmed resolved. Durable evidence: `/Users/eaafonasev/Projects/star-tournament/planning/docs/evidence/fix-weapon-ammo-digits-20261101/`. Source integration follows after this commit; verify ancestry independently. No spec change, build or publication.
