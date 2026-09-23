# Native killcam and kill streak QA

- Code revision at the FFA Player build: `f99631c`.
- Player: local macOS Development Build, Unity 6000.3.23f1, 1920×1080, muted (`AudioListener.volume == 0`).
- Scenario: `-matchReview`; four local viewports, scripted direct lethal events for deterministic killcam/HUD inspection. `NativeMatchReview` marks this `DIAGNOSTIC_NOT_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE`.
- Automated checks on `f99631c`: EditMode 109/109 passed; PlayMode 86/86 passed; `unity/tools.sh build` passed (`STAR_PROVING_BUILD_PASS 461335505`).
- `ffa-player/kill-notice-and-pair-score.png`: first direct kill, killer `1:0`, victim `0:1`, names in participant colors.
- `ffa-player/killer-orbit.png`: same death after orbit progression; camera looks at the killer from a new angle without switching participant life.
- `ffa-player/double-kill.png`: second direct kill, `2:0`, `Double kill!`.
- `ffa-player/three-kills.png`: third direct kill, `3:0`, `3 kills!`.
- `ffa-player/repeat.png`: new match after Repeat; paired counts and chains are zero in the corresponding JSON snapshot.
- Team Player on the same build: `-teamsDiagnostic -matchReview` completed with `NATIVE_MATCH_REVIEW_COMPLETE`. `team-player/kill-notice-and-pair-score.png` shows the opponent name in the authoritative blue or magenta team color for killer and victim; `team-player/killer-orbit.png` shows the subsequent camera angle. `team-player/double-kill.png` and `team-player/three-kills.png` show the uninterrupted chain. Matching JSON snapshots record team scores and direct pair counts `1:0`, `2:0`, `3:0`; `team-player/repeat.json` resets both counters and chains.

Each PNG has a same-named JSON snapshot with match statistics, profile identity and exact state. FPS visible in these screenshots is diagnostic and does not establish the four-player performance target. The scripted Player review uses four local viewports; physical input, 1/2 viewport visual review, TV and human visual acceptance remain open.
