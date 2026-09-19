# Unity participant roster — 2026-09-20

Verified internal change `add-unity-participant-roster`, base `45e7f27`, branch `codex/unity-participant-roster`. [Build/source identity](identity.json). Profile `unity-native-roster-v1@1`.

## Evidence

- **66/66 EditMode, 49/49 PlayMode, Mac Development build PASS**. XML included. Final own-corpse camera fix is covered by the last PlayMode run/build; EditMode precedes this presentation-only fix.
- Immutable composition validates2–8 participants /1–4 local views, metadata, vetted palette, unique/permuted mapping, independent snapshots and unavailable sources. Actual local action participant7 routing, view/capsule counts, eight safe spawns, FFA/teams, nonlocal killer, frozen Repeat and invalid-draft recovery pass.
- Candidate generation and search have independent profile budgets; failure activates no partial roster. FFA8:82 nodes ~0.434ms; teams1/7:43 nodes ~0.389ms; oversized candidate rejection ~0.176ms (diagnostics, machine-dependent).
- **18 PNG +18 state JSON per FHD/4K**, plus performance JSON/raw samples. Every capture focused/muted, exact1920×1080 or3840×2160. Both completion markers present; no runtime exceptions. Final contact sheets inspected; full killcam inspected after own-corpse occlusion fix.
- [FHD overview](fhd-contact-sheet.png), [4K overview](4k-contact-sheet.png), [native CUA journey](cua-journey.md). Controls captures are separate from automated fixture journeys.
- Astra read-only architecture/review findings fixed: bounded candidate work, safe invalid-profile preview, activation after full placement, composition colors. Final review reported no blockers; it was a code review, not an independent test run.

## Performance limits

| Resolution | Samples | p50 ms | p95 ms | p99 ms | Worst ms |
|---|---:|---:|---:|---:|---:|
| FHD |643|8.346|9.490|71.427|75.316|
| 4K |617|8.345|9.562|200.298|217.967|

Mac14,10 / Apple M2 Pro, Unity6000.3.23f1. Short foreground Update intervals include screenshot/UI/rebuild work; result fast-forward is outside the measurement window. GPU timing and allocation counters unavailable. **DIAGNOSTIC_NOT_TARGET_60FPS_ACCEPTANCE**; long-match/reference-hardware gate remains open. Earlier pre-fix runs retained in vault, not used as final evidence.

## Scope and retention

Fixtures are not playable AI. Shipping human setup remains2–4; bot planner/setup/solo-vs-bots/evaluation remain required. Physical controllers/TV, final art, complete migration, main integration/archive/distribution are not accepted by this slice.

Vault: `/Users/eaafonasev/Documents/Codex/qa-vault/star-tournament/participant-roster-2026-09-20` retains Player zip, logs/XML, final and superseded captures, identity and SHA256 manifest.
