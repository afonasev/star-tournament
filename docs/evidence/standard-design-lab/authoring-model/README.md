# Authoring model foundation

Historical foundation at4f4c4379; later `../runtime/` evidence now records actual Editor wiring and Git-tracked initial data. Missing-integration statements below describe that earlier slice.

This slice adds the production C# model and project-path service; it does not yet switch the Editor/Player runtime to author mode or deliver Git-tracked initial author data. Existing production startup continues to use packaged catalogue/local history.

`DesignLabHistory(...authoring:true)` makes saved package/Default profiles editable while Default remains undeletable. The package seeds an absent author file once, importing in version order and keeping original package provenance. Existing author files own names, deletions and flags; reload does not reapply package flags. Automatic in-memory compatibility adaptation does not rebind author flags. Original snapshots/hashes remain in the history.

Mark/unmark is separate from immutable version content and current selection. `MarkForRelease(profileId,revision,marked)` can clear a historical incompatible mark without selecting/activating that revision; marking an incompatible version is rejected. Invalid/missing marked Default is ReleaseError, not StorageError: editing remains available, while reset/export reject until fixed. Reset/startup choose highest marked Default revision, not most recent click/sequence. Changing flags does not auto-activate a version.

`BuildReleaseCatalog()` projects all and only marked compatible saved revisions with detached snapshots and preserved known/unknown dates. It neither writes nor builds/publishes. Production rejects this author-only API and retains data-level package protection. Package startup now chooses Default by greatest declared Revision and retains authored display name.

`LabProjectStorage.Open(absoluteUnityProjectRoot,baseline,seed)` resolves `DesignLab/authoring-history-v1.json` under a StarTournament Unity project with an ancestor Git marker. First open does not create data; explicit author mutations save atomically at that path. Fixture tests use an owned marker, not a real Git commit. Actual non-ignored repository path checked in project-path-check.json, but file creation/tracking and Editor/Development-client mode wiring remain open. No UI claims commit/push; save-destination status still needs integration.

Affected real Unity JsonUtility tests/commands: qa-scope.md. No Player build/full suite/installer/release/publication. Parent scope remains every applicable requirement of all five pinned packages; this model does not complete Lab05/06 or native acceptance.

Legacy alias normalization now applies only to non-author history without a package catalogue. An explicitly authored or packaged Default display name is shown verbatim, including a name equal to the old legacy alias. This does not change profile IDs or snapshot content.
