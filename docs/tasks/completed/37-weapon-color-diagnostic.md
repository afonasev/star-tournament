# Weapon color diagnosis

User scope: inspect why weapons held by other players look colorless and compare the damage bonus states. Diagnosis only; no rendering fix, integration, publication or acceptance.

Branch: `codex/weapon-color-diagnostic`, based on `706f7a849dfb6b60ee6db479da850ad13e7916b9`.

Completed: dedicated CLI capture harness, successful native build, muted Player exit 0, 24 screenshots and material/snapshot dumps. Other actors' rifle/rocket/cutter materials are replaced with the body's neutralizing identity shader while actor roots are inactive. First-person materials remain correct; the shotgun is excluded by name and becomes red under the bonus.

Evidence and exact scope: [weapon-color-diagnostic](../../evidence/weapon-color-diagnostic/README.md).

Next step if correction is requested: include inactive ancestors in the weapon exclusion, verify authored material preservation and all bonus transitions, and select the appropriate integration gate. Existing source investigation and captures should be reused.

Ownership: this worktree/branch and its ignored `unity/Builds/StarTournamentProvingGround.app` are retained for the follow-up. No foreign checkout or process was changed. Diagnostic completion does not establish human acceptance or release readiness.

## Авторизованное исправление

2026-10-02 пользователь поручил «давай исправим». Change `fix-world-weapon-colors`, source commit `2f19d2b`. Inactive ancestor lookup исправлен, добавлен regression; full make check и 24 native Player captures прошли. [Исправление, evidence и exact candidate](../../evidence/fix-world-weapon-colors/README.md). Human acceptance остаётся отдельной.
