# Local build cleanup — 2026-10-10

Authorized by the user: «очисти лишние сборки игры».

The JSON audit records nine deliberately removed paths: eight obsolete macOS Player bundles and one ZIP archive, freeing 3,981,340,672 allocated bytes. All removed source revisions are ancestors of retained accepted Player dfc650bbf05b / 489466109. Their features have recorded human acceptance; earlier variants are superseded. Per-file SHA-256 checksums, acceptance bindings, exact paths and replacement identity were saved before removal.

This audit supersedes historical playable links to these exact removed binaries. Source commits, manifests, screenshots, useful logs and historical acceptance decisions remain preserved.

Retained: the accepted integrated Player, newer accepted menu F4 Player, the exact pending complete-native-game-design-lab candidate and archive, other candidates with unresolved retention status, all production 0.1.1–0.1.4 packages, installed applications, Unity caches and the foreign voice-tuning worktree. A newer release package does not itself establish acceptance or remove an older platform candidate requirement.

Verification: no live Star Tournament Player/Unity build process; ancestry and archived acceptance records checked; every deleted path absent; retained integrated/menu/Lab Players present; pre-existing Git source changes unchanged. This housekeeping changes no game code and needs no Unity tests, full test run, Player build or deployment.
