# Lab release staging timeout — verified fix

Baseline: `bf2f7629` on `codex/production-0-1-3`. Task branch: `codex/lab-staging-timeout` in its isolated managed worktree.

Historical registry cache lookup now precedes the legacy predecessor probes. Exact ordered descriptor paths compare without sorting; reordered paths retain the complete ordinal multiset comparison. Validation still checks every hash, reconstructs trusted metadata, enforces read-only values and numeric ranges, and preserves historical revisions. The 180 s timeout and all original assertions are unchanged. Two additional cases verify reordered current/historical catalogues, detached snapshots, invalid-value rejection and source byte equality.

Full QA also found two test paths left stale by the previously integrated results-frame container. Only those paths in NativeMatchIntegrationTests and NativeSeatLayoutTests changed; assertions and runtime UI are unchanged.

## Verified checks

- Unchanged focused staging test: 1/1 passed, 138.046 s.
- Complete EditMode: 427/427 passed, zero failed/skipped; staging 154.604 s under its original 180 s limit.
- Focused results-path rerun: 2/2 passed, with all existing assertions.
- Complete graphics-enabled PlayMode: 252/252 passed, zero failed/skipped, 989.356 s. All three capture cases executed and produced 16 PNGs.
- Final source fingerprints match the tested snapshots; packaged LabReleaseCatalog bytes match baseline; `git diff --check` passed after restoring owned Unity-generated changes.

The complete gate comprises the full EditMode receipt and full unfiltered PlayMode receipt on unchanged runtime/EditMode sources. Full testing was explicitly requested in the original task. No further run is needed for commit or metadata; future full runs require separate human authorization under the latest 2026-10-10 policy.

## QA route and retained evidence

EditMode was the completed first stage of `make check-full`. The initial PlayMode stage was normally cancelled through its owned unity-run client: general fixtures read the default user Lab history (eight completed startup reads totalled 759.71 s; runtime startup uses persistMigration:false). The next headless full PlayMode found the two stale paths and three capture cases that Assert.Ignore without graphics/output directories. These attempts remain diagnostic receipts, not passed gates.

Final PlayMode used unity-run shared, batchmode with graphics, no testFilter, an isolated STAR_TOURNAMENT_QA_LAB_HISTORY under this worktree, and STAR_TOURNAMENT_HIT_CAPTURE / STAR_TOURNAMENT_SHOTGUN_CAPTURE output directories. No Player build was needed or performed. Complete XML/log receipts, source fingerprints, capture hashes and images are in `docs/evidence/lab-staging-timeout/`. Two rendered health/shield frames were inspected; this is Editor evidence, not named human/device/visual/audio acceptance.

## Delivery and cleanup

Source changes and persistent QA evidence are preserved in the task Git commit. Owned scene/prefab/project-settings changes and the interrupted runner's temporary InitTestScene were removed. Disposable Library, Logs, UserSettings and .local QA copies are removed after source preservation; the branch/worktree and committed evidence remain for review and integration. Other worktrees, processes, caches and user data are outside cleanup scope.

Merge, Player/distribution builds, deployment, installed-game update and human/device acceptance remain pending separate instructions. No merge or deployment was performed.
