# Death audio audibility — 2026-10-10

Baseline: `97c42610` (main). Scope: raise only death feedback for own and nearby participants; preserve selected three variants, hearing range, pan, user volume, single split-screen output and gameplay.

Implementation: `audio.deathGain` in `unity-proving-ground-v1`, default 2 (+6 dB before shared normalization), range 0–4, step .05. Death voices finish under shot-pool saturation. Stop the dying participant’s cutter before calculating the death mix. Historical Lab snapshots retain hashes and custom controls; trusted predecessor projection appends the new descriptor on migration.

Affected QA commands (no build):
- `unity/tools.sh test-edit-filter 'NativeMusicTests;DesignLabHistoryTests;NativeAudioSpatialTests'`: audio descriptor/history compatibility plus unchanged distance/pan contracts.
- `unity/tools.sh test-play-filter 'ApprovedSpatialAudioTests;NativeAudioRoutingTests;NativeRoundMusicTests;NativeMenuMusicTests'`: death own/remote gain, range, mute, saturated pool and shared effects/music budget; neighbor routing and music normalization.

Full tests, Player/release builds and deployment deferred to separate explicit human commands. Automated gain/routing tests do not prove listening acceptance in real gameplay.

Results: EditMode 63/63; PlayMode 15/15; zero failed/skipped/inconclusive. Evidence: `docs/evidence/death-audio-audibility/`.

Source/evidence integrated into main. Task worktree archived after tests; unrelated main ProjectSettings retained. Real gameplay listening remains open.
