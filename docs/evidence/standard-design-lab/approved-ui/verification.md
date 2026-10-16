# Approved Lab 18–19 UI

Baseline: `623c6bfb` on `codex/game-standard-alignment`.
Human approval: 2026-10-17 reply to call_088bfb1891cd4330ab7b8a0fb4152541: «Да, реализовать оба макета».

Affected behavior: approved05/18 workspace placement and boolean controls; approved19 version inspection, guarded compatible selection, project historical unmark without activation/draft loss.

Focused PlayMode filters: `DesignLabWorkspaceTests.BooleanSwitchesKeepDraftSeparateAndSaveOnlyOffOrOn;DesignLabWorkspaceTests.RevisionSelectionKeepsFramesRunningUntilAtomicPublication;LabAuthoringRuntimeTests.HistoricalInspectionAndUnmarkPreserveActiveBaseDraftAndOriginal;LabAuthoringRuntimeTests.ReleaseResetGuardsDraftAndPreservesEverySavedRevision`.
These cover protected/two-state controls, save readback, no draft publication, async selection, historical persistence and original integrity, discard guards, rendered1080p/720p/short viewport. Pure boolean data guards are unchanged from baseline.

Run through `tools/unity.sh shared -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter <above> -testResults <owned-output>/results.xml -logFile <owned-output>/unity.log`. Graphics enabled for real Editor captures. No Player build/full tests/release/deploy. Runtime/device/human acceptance remain separate.

## Results

Initial run:4/4 Passed (`initial.xml`): switches/save, nonblocking selection, historical unmark, reset/discard. Final state:2/2 Passed (`final.xml`) after explicit selected-base/active captions, author original-row visibility and inspected-row scroll reveal. The two unchanged neighboring selection/reset contracts passed in the initial run; final checks were repeated only for affected screens. Four unique tests, no full suite. XML and compressed Editor logs retained.

One intermediate run:1 Passed/1 Failed (`name-fixture-error.xml`). The long-name fixture exceeded the existing48-character product limit, so profile creation was correctly rejected; subsequent file-read assertion failed. Corrected fixture uses a valid43-character name and now asserts persistence before editing. Product name validation was retained.

Real graphics-enabled Editor PNGs viewed at1920×1080,1280×720 and1920×650 for the workspace; historical screen at1280×720. Checked toolbar/search separation, protected boolean controls, distinct selected state and focus outline, gamepad right/submit, long-name abbreviation, footer hints, selected base vs inspected version, active identity, safe Close and disabled incompatible activation. Geometry assertions cover toolbar/control separation and search width. Source and PNG SHA256 manifests bind the final captures. No physical/device/human acceptance claimed.

Editor runs were shared, muted from startup, with isolated QA history/project paths. No Player/build/full/release/deploy. Baseline data validation guards are unchanged. Original snapshot/hash/date and all rows survive historical unmark; compatible selection still opens discard confirmation. Source/execution identity and broader requirement audits remain open in the child change.
