# Production 0.1.3 — 2026-10-10

Authorization: user «давай все выкатим на прод». Exact runtime/package source is committed private main `97c42610`; public source/tag is `fd17c8e5c1fc018a88dbfa6282f6e5b492f2700f`. Foreign dirty primary ProjectSettings were excluded. This release includes all committed runtime changes through that snapshot, with no new product requirements.

## Verification

- Minimum affected scope selected from production 0.1.2 runtime baseline `4249c096` and feature dependencies: 84/84 EditMode and 93/93 unique PlayMode cases have passing evidence. Initial graphical PlayMode: 90 passed, 3 explicit capture skips without output environment paths. Only those three were run separately with their capture paths and passed. Original XML, reasons and combined latest-case mapping retained; no claim of an all-green initial invocation. Full tests were not authorized and were not run.
- Windows DPI contracts 6/6; GitHub publication contracts 12/12; actual launcher security contracts 49/49.
- Ordinary Windows x64 and universal macOS release builds passed. These were explicitly authorized by this production request. Signed distribution delivers Windows x64 and macOS arm64 installers/full updates. Generated build scene/rig/settings changes are preserved as a patch, then restored in the owned worktree.
- Both new Windows launcher and Unity Player EXEs declare PerMonitorV2 in their actual PE process manifests. Real NSIS installer icon resources match all seven approved ICO entries. This does not establish physical Windows 300% behavior.
- Exact packaged macOS Player reached MAIN_MENU_READY in 146.02 seconds including guarded admission/startup. Original 160,126,874-byte Lab history SHA256 stayed `7241dbd29c3d2abb7679532fa7a0478cfde7514d39b94b30e4d85f3c862b938b`. Shared, muted batch graphics; no desktop/device or sensory acceptance claim. The owned Player was stopped after this bounded smoke.

## Publication

Production Latest: https://github.com/afonasev/star-tournament/releases/tag/v0.1.3, published 2026-10-10T08:52:44Z. All eight anonymous HTTPS readbacks match expected size/SHA256. Both live production SDKs authenticated metadata and downloaded the exact full 0.1.3 package; no local installed-game activation was performed. Current IAB landing renders 0.1.3 and both versioned installer URLs with no console errors. See publication.json, sdk-summary.json, source-tag.json, landing.json and landing.jpg.

Exact eight published artifacts (584,196,600 bytes) are retained under `~/.local/share/star-tournament-distribution/retained/production-0.1.3` for physical installation, update, 300% scaling, controller and sensory acceptance. Prior retained releases were untouched. Raw Players, payload/layout duplicates, SDK download probes and owned caches are deliberately removed after hashes/logs and these retained artifacts are saved; historical worktree paths in custody records are provenance, not live playable paths. See retention.json and cleanup.json.

Physical acceptance remains pending; steps are in acceptance.md. Source commits, builds and production publication do not substitute for it. No OpenSpec or chat acceptance/archive is inferred from the publication command.
