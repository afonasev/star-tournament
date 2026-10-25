# Shotgun surface and neutral pellets

Authorized follow-up: restore gun texture and replace rejected brown pellets with neutral grey. Baseline main09083300, owned codex/shotgun-surface-fix.

Restore original embedded metal-map bytes to the current gun chassis and titanium, preserving geometry, UV, skins, animations and previous textures. Asset optimization: one shared128x128 texture perGLB, four files gain~2.1KB each, no extra materials, primitives, vertices or draws. Audit output .local/shotgun-surface/asset-audit.json; source recipe tools/restore-shotgun-surfaces.py.

Append a Lab Default with RGB.32/.32/.32, retaining spread4.8, heavy sound and every other effective gameplay value; original metadata/source snapshots stay immutable. No new profile schema version; approved Well Default compatibility correction described below.

Minimal affected checks: muted shared Stage via ShotgunWeightUpdate.Stage; EditMode ShotgunWeightTests and TrooperImportTests (snapshot/value equality, material binding, original geometry budgets and animation/skin contracts); graphical muted ShotgunWeightPresentationTests (18 grey traces, actual textured gun, camera frame). No full tests, Player build or deployment.

First Stage FAIL retained: author compatibility views retained old inherited tracer RGB after editing Default. Add explicit author-only RefreshAuthoringCompatibility (existing migration, derived views only) before staging; original sources/flags/guard untouched. Add regression ChangedDefaultRefreshesOnlyDerivedMissingFieldsBeforeStaging; include affected LabAuthoring tests.

Diagnosis corrected: staged author source includes Wellv3, while prior task narrowed its migration to v4; stale derived views had masked it until the next registry refresh. Extend approved default-only daylight migration to v3-v4 (v1/v2 custom/invalid fixture behavior stays intact), regression both versions. Remove temporary diagnostic logging. Explicit refresh before staging remains covered by its missing-field regression.

Corrected Stage PASS: Default11 hashf569c8a947dcbccff77e9bc3037a999461472b5e86e6f1390d2a3d652c8ed522, spread4.8, greyRGB.32. Previous catalogue entries verified unchanged by staging guard and exact comparison. Focused EditMode command includes ShotgunWeightTests, TrooperImportTests, UniformPickupCircleProfileTests, missing-field refresh regression, historical marks and invalid-version authoring contracts.

Affected EditMode V9U8Xf:18/19 PASS. New regression fixture failed because initial author seed was never persisted, so reopening created a fresh history with different physical row keys; fix fixture by PersistAuthoring before recording original row. Exact JSON/hash and release assertions retained. Material imports, shared meshes, geometry budgets, grayscale and complete Default-value comparison all passed.

Rendering correction: initial camera render matched prior untextured-looking grey gun exactly despite real named maps being bound. Texture-off control revealed the authored ceramic/metal appearance only on the later render, showing first-frame asynchronous shader compilation fallback. Disable async shader compilation only for this isolated Editor capture, restoring its prior value in teardown; retain actual texture-on/off pixel comparison. Earlier screenshot is not accepted as texture visual proof.

Final capture required two warmup renders with yielded frames; disabling asynchronous shader compilation alone did not settle the first render. Stable PlayMode PASS1/1: texture on/off affects15873 pixels, restoring the map reproduces every pixel exactly. Stable image visually inspected. Previous logs retained as diagnostic evidence, not accepted visual proof. Corrected author fixture PASS1/1; all19 selected EditMode cases have passed across the original and focused retry. Restore script idempotence PASS, all GLB hashes unchanged.

Before integration main advanced to8a11a9f9 (Forge placements and bot behavior/registry compatibility). Rebase and repeat affected shotgun/Lab tests plus rendered fixture to cover shared registry migration; geometry/import tests need not repeat. No full suite or build.

Rebased source449a0482 on main8a11a9f9 without conflicts. Post-rebase EditMode10/10 PASS (ShotgunWeightTests; UniformPickupCircleProfileTests; ChangedDefaultRefreshesOnlyDerivedMissingFieldsBeforeStaging). Independent asset audit confirms original binary payload and nodes/meshes/accessors/skins/animations exactly preserved in all4 GLBs.

Post-rebase graphical PlayMode1/1 PASS;15873 texture-sensitive pixels and exact restored-frame equality. Final camera image visually inspected. Durable raw evidence: shared planning docs/evidence/shotgun-surface-fix-2026-10-25. Source-only completion; no Player/build/deploy or human acceptance claimed.
