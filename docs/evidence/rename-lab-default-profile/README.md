# Default profile label

Implementation `157b8b5` is integrated into main through the related release-catalogue change. The initial capacity-blocked attempts remain in `queued-runner.log` / `ui-gate-result.json` as iteration evidence, not the final status.

Final functional gate: 351 EditMode + 211 PlayMode and native development build passed on `35c7191`. After integration with the current menu: 4 affected Lab/menu PlayMode tests passed on `4549ee5`. Muted isolated native smoke shows Default, immutable histories, local marks and restart behavior; the personal history file is byte-for-byte unchanged.

See `../ship-lab-release-revisions/README.md` and `verification.json` there for durable XML, PNGs, exact candidate identity and limitations. Regular macOS release candidate: `/Users/eaafonasev/Projects/star-tournament-candidates/ship-lab-release-revisions/Star Tournament Unity Proving Ground.app` (source `4549ee5`). Older rename candidate retained at `/Users/eaafonasev/Projects/star-tournament-candidates/rename-lab-default-profile/StarTournamentProvingGround.app` for pending acceptance. Implementation worktrees/branches may be removed after their integrated evidence is preserved. Human/platform acceptance and public publication have not been claimed.
