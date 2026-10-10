# Combat / music mix — 2026-10-10

Baseline: `ccfd8c17`. Branch/worktree: `codex/combat-music-mix`, `/Users/eaafonasev/.codex/worktrees/combat-music-mix/star-tournament`. Scope: own and nearby weapon fire must stand out over music; preserve approved timbres, spatial hearing range/pan, user volume and previous death/ready feedback.

Evidence before edit: the rifle bank loudest 50 ms is -21.3 dBFS; Reactor's first five seconds reach -10.9 dBFS on the same measure. A close remote rifle is reduced further by remote gain .4; current music gain .55*.32 can mask it. Source measurements are diagnostic and do not prove listening acceptance.

Implementation: Lab `audio.weaponGain` default 2; apply to rifle/shotgun/rocket one-shots and cutter loop. Audible combat (shots/explosions/cutter) ducks music to a floor .25, weighted by distance/user effects gain against reference .3, with .3-second release on the music clock. General mix budget remains <=1; do not silence the theme at saturation. No asset, simulation or input changes. Trusted append-only migration and runtime fallback for historical releases preserve original profiles/hashes/settings.

Affected QA selected before launch:
- `unity/tools.sh test-edit-filter 'NativeMusicTests;NativeAudioSpatialTests'`: new defaults/history migration including profiles predating previous feedback fixes, existing music migrations and unchanged distance/pan.
- `unity/tools.sh test-play-filter 'ApprovedSpatialAudioTests;NativeAudioRoutingTests;NativeRoundMusicTests;NativeMenuMusicTests'`: actual source gains for own/near/far gunfire, all three shot weapons and cutter, full music/user controls, bounded shared mix, smooth release/pause/restore, old release fallback, earlier death/ready/routing/music lifecycle contracts. Simulation, triggers, physics and natural matches are unchanged from the verified baseline, so no broad gameplay/full regression.

Full suite, Player/release builds, publication and installed-game update deferred to explicit commands. Real listening acceptance remains open.

QA attempt 1: NotRun — compiler rejected a new test notice whose sequence/shooter arguments were swapped. Corrected constructor arguments before rerun; runtime source unchanged.

EditMode: 15/15. Initial PlayMode: 23/24; new own/near comparison counted volumes on stopped pool slots as audible output. Corrected accounting to playing sources, retaining the same <=1.0001 cap and adding an exact one-playing-voice assertion. Runtime source unchanged. Repeat only the corrected method:
`unity/tools.sh test-play-filter 'ApprovedSpatialAudioTests.OwnAndNearbyGunshotsGainForegroundOverFullVolumeMusic'`.

Final QA: EditMode 15/15; all 24 distinct PlayMode cases closed (23 initial + corrected own/near scenario). Runtime unchanged between passing runs. Evidence: `docs/evidence/combat-music-mix/`. Source/spec/tests/evidence integrated into main; owned task worktree/cache archived and merged branch removed. Real gameplay listening, full tests, builds and publication remain deferred.
