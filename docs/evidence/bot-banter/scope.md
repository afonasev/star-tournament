# Bot banter: affected verification
Baseline: ccfd8c1740763f7f55763e58ada7a10ada8da767.
Diff: additive fire/damage attribution, presentation history/scheduler, HUD lifecycle. AI, gameplay snapshots and weapons unchanged.

- `unity/tools.sh test-edit-filter BotBanterTests`: predicates, actual shield/HP, lives, revenge, cooldown, no repeats.
- `unity-run --shared ... -runTests -testPlatform PlayMode -testFilter "BotBanterIntegrationTests;DamageBonusAlertTests;UnifiedDamageSessionTests"`: real attack/damage/death order and exact snapshot parity, native HUD 1–4 views, pause/restore/repeat; neighboring applied damage and shared alert contracts. Graphics enabled for Editor UI capture; no Player build.

Full, Player/release/deployment and physical TV/gameplay visual acceptance remain separate. New worktree has its own Library and isolated QA Lab history. Sound muted by fixtures.

Integration refresh: main advanced to b003ac65c0ab8550a67ce003b7063cd27aa2f6ef (combat music mix). Diff affects NativeGameAudio and audio profile/history defaults; NativeCombatSession, ProvingGround and banter source contracts unchanged. Rebased implementation8db4d69559790a47c3fdf1829a8373ec99af75ed. Selected additional PlayMode filter `BotBanterIntegrationTests;DamageBonusAlertTests` to verify shared scene/bootstrap and alert subscribers with new audio/profile code. No reason to repeat the unchanged reducer/ballistic tests or run full.
