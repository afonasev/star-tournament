# Data compatibility investigation

Historical audit at613034bf. The later `migration/` directory records the actual v0.1.2 release boundary and the implemented versioned storage strategy; its current source/QA supersedes runtime-unchanged statements below.

Requirement UPDATES-DATA-01, approved template `0c2d4562683252d54736f294dc1af463050eac17`. Scope: current recovery branch versus prior application source `edd095ae`; this is not verification against every installed/published release.

| Storage | Reader/writer and startup behavior | Recovery implications |
| --- | --- | --- |
| `Application.persistentDataPath/player-profiles-v1.json` | `PlayerProfiles.cs`: exact document Version 1, IDs/names validated, constructor reads without saving; explicit CRUD uses temporary file plus replace. Legacy missing gamepad fields resolve to defaults. | Current branch has identical source to baseline. Unsupported document version throws and leaves bytes intact. No automatic profile reset. |
| `Application.persistentDataPath/design-lab-history-v2.json` | `DesignLabHistory.cs`, `LabParameterStorage.cs`: schema 1 legacy and schema 2 compact, trusted registry/hash validation. Background loader uses `persistMigration:false`; in-memory migration is not a storage rewrite. | Original v1 history path is separate. Unsupported/corrupt data reports StorageError; mutation and compaction are blocked. Explicit legacy compaction retains a byte-for-byte `.schema1-backup.json`; failed publish keeps source/selection. |
| Unity PlayerPrefs | Audio, graphics, UI, mouse, gamepad, FPS, last played map; same keys/readers/writers as baseline. Writes originate in explicit settings/profile actions, display confirmation or starting a match. Audio Effects action freezes legacy Voice first. | Recovery does not delete/reset preferences. No migration added here. Tests do not establish native macOS plist/Windows registry location or installed-build behavior. |
| Startup ready marker and update command/status paths | `NativeStartupHealth.cs`, `NativeUpdateBridge.cs`: launcher-owned session IPC paths, not player profile storage. | Challenge marker/command files are not a user-data backup. Never restore or delete a profile through these paths. |
| `ProvingTelemetry.cs` report/screenshot | Explicit diagnostic output directory from launch arguments. | QA output is not game progress/history; exclude from production user-data snapshot claims. |

`source-equivalence.json` records exact SHA-256 equality for 13 production data/preferences files against `edd095ae`. The only runtime changes since that baseline are startup readiness acknowledgement and its caller. This supports absence of a newly introduced format migration in this recovery slice; it does not prove compatibility with older release snapshots or future format changes.

## Current strategy and limits

Preserve existing storage paths, exact version checks and no-write startup loading. Incompatible Lab data remains untouched and its controls are disabled; incompatible player document aborts startup so launcher recovery can act. App rollback restores application bytes only; it is not a transaction covering user data. Saved scalar preferences use unchanged keys and meanings in this slice.

UPDATES-DATA-01 and task 2.3.2 remain OPEN. Before closing: identify actual previous installed release data readers and the candidate's complete schema/registry identities; exercise previous → candidate → failed startup → previous on owned copies, including preferences, profiles, Lab histories and explicit post-start mutations. Future incompatible format changes require a versioned storage path or verified reversible migration/backup strategy before release. Existing schema1 backup alone is not a universal data rollback mechanism. No native installed rollback, native plist/registry preservation, crash-during-user-data-migration, or cross-platform acceptance is claimed here.

See `qa-scope.md` and `verification.json` for selected checks. No runtime/design changes, Player build, installer build, full suite or publication in this slice.
