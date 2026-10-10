# Production 0.1.4 — 2026-10-10

User authorization: «принято, давай все изменеия из main соберем в релиз и опубликуем». All committed runtime main through `358fab9a5e6ebd3aea4aee57e68c8666346aa56c` is included. Production0.1.3 runtime baseline: `97c42610c1b1667c1d664a3159b90fa2044fec0b`. Public source/tag: `13f082a6a923b1e3006e7465405261d14d0b1967`;108 changed Git blobs and1076 exact source paths verified. Foreign dirty primary ProjectSettings were excluded and preserved. This is a release operation with no new product requirements.

## Verification

- Scope selected before launch from actual diff/dependencies:65/65 EditMode and29/29 PlayMode passed,0 failed/skipped. Original XML and commands are retained. Full tests were not authorized or run. Distribution/launcher source is unchanged since the verified0.1.3 baseline; real new-package guards and live transport are checked again.
- Ordinary Windows x64 and macOS release Players passed. Builds/installers were explicitly authorized by this request. Distribution delivers Windows x64/macOS arm64 installers and signed full updates. Generated scene/rig/settings patch and paired custody are preserved; both package sourceDiffSha256 values match that patch.
- Actual launcher and Unity Player PE process manifests declare PerMonitorV2. The launcher-specific validator is unsuitable for Unity's equivalent True/PM spelling; the Unity manifest was parsed separately. This does not establish physical300% Windows acceptance.
- Exact packaged macOS Player reached MAIN_MENU_READY in115.15 seconds including guarded admission/startup. Original160,126,874-byte Lab history SHA256 stayed `7241dbd29c3d2abb7679532fa7a0478cfde7514d39b94b30e4d85f3c862b938b`. No missing bot/native audio resources or runtime exceptions at readiness. Shared muted batch graphics, no physical input or audio acceptance. Only the owned Player was stopped after readiness.

## Publication

Production Latest: https://github.com/afonasev/star-tournament/releases/tag/v0.1.4, published2026-10-10T12:18:52Z. Eight anonymous HTTPS readbacks matched expected size/SHA256. Both live production SDK probes authenticated and downloaded exact0.1.4 full packages; no update activation/local installation. IAB landing renders0.1.4 and both exact installer links with no console errors. Source tag and Latest identities were checked. A GitHub notes-only draft PATCH temporarily reset its tag; the owned draft was explicitly restored to v0.1.4 before publication, with no alternate release published.

Exact eight published artifacts(585,927,849 bytes) and launcher probe configuration are retained at `/Users/eaafonasev/.local/share/star-tournament-distribution/retained/production-0.1.4` for pending install/update/controller/audio/platform acceptance. Full update packages preserve the exact Player bytes; raw Players, layout/portable duplicates and SDK probes can be removed after publication. Historical worktree paths in custody/smoke records are provenance, not future playable paths. Prior retained0.1.1/0.1.2/0.1.3 were untouched. See retention.json and cleanup.json for actual cleanup.

Physical acceptance remains open; steps are in acceptance.md. The user's prior «принято» accepts the faster source audio, not the future installed0.1.4 Player. No local installed-game update or OpenSpec/chat archive is inferred from the publication instruction.
