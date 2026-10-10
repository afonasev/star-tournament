# Affected QA selected before launch
Baseline `5a13e0dd8a7270706fcd787f3984325597ccd30b`.
Diff:20 new/replaced processed voices, exact catalog, audio resource load/mapping, explicit posthumous speech flag from existing selected line; settings toggles/one-source/effects budget/scheduler remain.

- `unity/tools.sh test-edit-filter BotBanterVoiceCatalogTests`: execute C# catalog set equivalence, unique resources, actual Unity import20 mono nonempty bounded clips.
- `unity/tools.sh test-play-filter 'BotReactionSettingsTests;BotBanterIntegrationTests'`: all20 exact clip mappings, invalid text/source rejection, real death+posthumous callback+respawn, live boast death stop; existing four settings combinations at1/4 viewports, persistence/input, mute/pause/restore/rebind/mix and native event/HUD contracts.
- FFprobe/decode loudness metrics and hashes of every source/processed recording; optional local Whisper base ASR checks transcription against manifest as diagnostic (not a substitute for listening).

PlayMode mutes physical AudioListener. No full suite, natural matches, Player build/release/deploy. Perceptual aggression/clarity and real combat mix remain human acceptance. Generator is reproducible from preserved raw sources; catalog is checked against authoritative C# lines, not a copy of simulation logic.

Initial runtime/code QA:2 EditMode +7 PlayMode passed. After diagnostic ASR caused processing-only revision, refresh actual imported final clips with EditMode resource test and three PlayMode cases (all20 mappings, dead/live lifecycle, effects lifecycle/mix). Catalog set/settings/HUD contracts are unchanged. Main advanced to f53d0c85 with neighbor multikill WAV/evidence and unrelated GAME_SPEC change; fast-forwarded task branch before final checks and reapplied owned GAME_SPEC delta without overlap. Effective final QA dependency baseline f53d0c85350c16364cf73480b6d77f111fda668a.
