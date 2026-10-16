# Authoring runtime integration

Source changes are based on approved Lab05/06 and pinned template0c2d4562683252d54736f294dc1af463050eac17. Editor defaults to the Unity project's `DesignLab/authoring-history-v1.json`; Development Player accepts `-labProject ABSOLUTE_UNITY_PROJECT`. A compiled production client does not enter authoring mode. QA history/review overrides keep existing tests in isolated production behavior unless an owned QA project is explicitly selected.

Author Default loading is critical initialization: project data/flags resolve before menu-ready. Production history remains deferred. A missing/incompatible marked project Default keeps Lab available and Play routes to Lab until Default is fixed/reset or the user explicitly selects/saves a valid saved configuration. Viewing/cancelling a draft and mark/rename operations do not silently activate last session's experiment. The currently active version and author file destination are shown; successful write says Git commit/push was not performed. Reset-to-release uses the same data authority and approved draft guard.

The bootstrap command below creates the initial project source only if absent, reuses runtime descriptor preparation against the checked-in scene, performs readback and closes only the scene it opened. It does not regenerate/save the scene, build Player, stage release catalogue, commit/push or publish. An existing dirty open scene or author file is refused. Actual Git tracking/readback proof is recorded after execution.

## Commands
Silent Editor authoring: `UNITY_RUN_MODE=shared unity/tools.sh editor -noaudio -muted`.

Explicit initial data creation: `UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -nographics -executeMethod StarTournament.ProvingGround.Editor.LabReleaseBuild.BootstrapAuthoring -quit -logFile <owned-log>`.

Development Player after a separately authorized build from this source: direct executable with `-noaudio -muted -labProject /absolute/path/to/star-tournament/unity`. Existing installed binaries are not updated by this source change.

Automated wrappers set `STAR_TOURNAMENT_QA_LAB_HISTORY` to owned data and thereby retain production-mode fixture behavior. New author fixtures set `STAR_TOURNAMENT_QA_LAB_PROJECT` to their own temporary project; do not point QA at human author files.

Affected commands/results live in qa-scope.md/verification.json. Screenshots are real offscreen Editor renderings with graphics enabled; -nographics is sufficient only for serialization/bootstrap checks. No physical/native Player/human acceptance inferred. Explicit release projection/build-tool integration, historical incompatible-row unmark UI, full05 reconciliation and all remaining parent requirements stay open.

## Initial source proof
Explicit bootstrap created the198579-byte compact author history with one Default profile. Its selected storage key4 corresponds to immutable saved package version2; UI/reference labels and projection use that saved identity. Source records retain original hashes c1c6487e.../0ad5f6be... alongside current interpreted views eb9cfd93.../1bfe89dc.... The projection correction exports original snapshots/dates/numbers for package origins; it does not publish current interpreted bytes under reused version IDs. Actual read-only ValidateAuthoring returned entries2/defaultVersion2/effectiveHash1bfe89dc..., and current scene/package catalogue bytes match pre-bootstrap fingerprints.
