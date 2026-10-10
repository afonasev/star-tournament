# Menu cursor follows the input device

Baseline: `b13997c7`. Local menu/input presentation fix: hide the mouse cursor when using gamepad navigation/buttons; restore it on mouse movement, click or scroll. Preserve the choice across menu transitions and ignore small stick drift. Running gameplay cursor capture is unchanged.

QA selection before execution:
- `make check-ui`: viewport/menu ownership and independent pause transitions; inspected runner, no Player build or natural matches.
- `./unity/tools.sh test-play-filter "SettingsGamepadNavigationTests;SetupGamepadNavigationTests"`: synthetic device switching and existing settings/setup navigation contracts in Editor Play Mode.
- `git diff --check`.

Full suite, Player build, release/deploy and physical gamepad acceptance are deferred. No build is needed to test cursor visibility and Input System state in Editor Play Mode.

Validation: layout 3/3; existing menu/pause/settings/setup cases 25/25; new cursor case 1/1 after configuring focus-independent synthetic mouse input. Evidence: `docs/evidence/menu-gamepad-cursor/`. Integration with `5ccbd0ba`: cursor switching, complete settings navigation and pause/resume 3/3 on `7399edb4`. No runtime source changes after verification. All 29 selected unique cases passed across runs. XML and compressed logs saved in Git. Task-owned Unity caches, temporary scenes and QA history are removed after saving evidence. Physical cursor/gamepad acceptance remains open. First PlayMode run was stopped during loading of the shared user Lab history (NotRun); retry uses a task-owned `STAR_TOURNAMENT_QA_LAB_HISTORY` path, preserving the user history. Keyboard capture and running gameplay cursor policy are unchanged.
