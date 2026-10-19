# Retained startup backups through Windows repair/upgrade

Baseline `a4246020`, branch `codex/game-standard-alignment`. This replaces the temporary refusal for retained startup attempts and covers first manual upgrade with no prior update journal. It is source integration, not installed Windows acceptance.

## State and transfer

`WindowsRecoveryStore.TransferForInstaller` writes only the fixed sibling stage, while keeping the logical final root and PayloadSnapshot.InstallPath unchanged. It reads the source journal when one exists; otherwise a newer installer captures the existing current/bootstrap directly. It never creates a recovery directory in the original root just to prepare transfer. A same-version repair with no history does not invent a downgrade snapshot. Existing history/no-Attempt still creates a previous-version fallback for a newer installer.

An unconfirmed installed target keeps its original verified known-good payload/bootstrap; newer installer targets do not promote that unconfirmed image into a fallback. A confirmed/nonempty or missing latch preserves the current version floor. Same-version repair preserves its state and conservative latch; newer upgrade captures current floor and retains **all** earlier backups as archives. Repeated confirmed upgrades extend a validated bounded id-only Retained list; archives cannot select a restore target or an executable. Only current attempt can restore. Archives are removed after the existing exact current startup/restoration attestation authorizes Cleanup. Interrupted cleanup/state writes retain the active latch until the final journal clears; deletion is resumable.

Readiness latch content is normalized to empty/nonempty while preserving polarity, not interpreted as an execution or cleanup grant. The original current latch is held read-only with FileShare.Read|Delete through outer commit: new append is denied while whole-root rename remains possible. The native semantics require Windows and are NotRun here. New and copied latches always use the protected append-only creator supplied by the Windows adapter. Other files/directories use protected creators and independently enumerated paths; no user metadata supplies a store destination.

Source capturing/prepared/restoring states are first completed/restored by the elevated installer resume path under source/root/product ownership, lease and cross-user idle guards, before actual version inspection. Restored and partially deleted snapshots carry their deletion intents; newer target captures the actual restored current as its fallback. The outer installer rollback restores the original root/journal byte-for-byte, including after separate process exit at copy/capture/rename checkpoints. Candidate recovery is marked installed only after the outer root swap/readback. A fresh cross-user process check occurs after backup transfer and before root rename.

The extra store/current/bootstrap data is sized before copy, including conservative manifest overhead. Low space fails before original root mutation. Write bounds keep protected state within the reader limit. No arbitrary source paths or manifest entries are executed or used as deletion paths.

## Legacy playback compatibility

The pre-change main launcher at `edd095ae` rejects maintenance protocol other than1; the old protocol2 producer at `a4246020` required2 for activation. Read-only source snippets are in `legacy-protocol-source.txt`. Captured attempts now include optional PreviousProtocol. Old protocol2 records that omit it retain the known producer default2; a new capture records the actual1/2, with conservative1 when the old contract is missing.

Startup restoration atomically restores the **configuration** from Previous plus its captured protocol, while retaining the new protected maintenance executable/key. This allows a protocol1 launcher to play offline after fallback without overwriting a live/elevated broker. Legacy six-argument activation is not enabled as an unsafe fallback: its next update uses the new installer, and recovery feedback records that retry route. Protocol2 fallback keeps the existing explicit restart retry route. These are source/metadata compatibility checks; actual legacy Player execution remains a platform gate.

## Results and limits

39 actual transfer cases passed, including four abrupt child exit/reopen cases;40 installer transaction,35 protected recovery,40 shared payload and19 Python NSIS routing/preprocessor cases passed:173 selected checks. Generic installer fixtures now have actual versioned root layouts because first-upgrade retention requires a real previous payload. One added test initially targeted manifest.json instead of the real atomic manifest.json.write-* path; its injection was corrected, assertions were not reduced, and the failed log is retained. Broker Debug win-x64 compile:0 warnings/errors. Existing negative/nullable fixture warnings in the test harness are separate from the broker result.

`--windows-installer-transfer-native` is an explicit NotRun on macOS. On Windows it exercises actual FILE_APPEND_DATA readiness writing and the deny-append/share-delete directory-rename lease with owned temporary files. It does **not** cover protected ACLs, UAC, NSIS GUI, real Player or original-user privilege boundaries. Native/render/device/human acceptance stays open. No Unity/full/Player/installer/release build or publication occurred.

When both old version records or the protected recovery journal are destroyed/conflicting, or a new floor cannot be captured from damaged metadata, the source remains fail-closed. Same-version repair can repair floor metadata before a newer installer captures it. Unknown recovery data without its journal is preserved rather than guessed/deleted. Real user-data format/migration/rollback compatibility and saved test-channel integration remain mandatory parent work before full recovery completes or merges.

Commands, exact source SHA256 and logs: `verification.json`. The whole parent goal remains open; this does not narrow it to Windows installer recovery.
