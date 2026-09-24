# F4-GRIP-01: Strata right grip correction

User feedback: the right hand appeared to touch Strata with fingertips instead of holding it. The FPS model now uses the prior F3/F2 Orbital hands selected by the user. The F4 Oxblood world body and Strata weapon remain. A continuous palm swell on Strata and profile-driven right finger curls bring the palm and fingers around the primary grip without changing the authored clips, gameplay or weapon identity.

- `right-grip-side.png`, `right-grip-opposite.png`, `right-grip-under.png`: close views of exported GLB geometry with the same additive finger curl used by the Player.
- `equipment-model.png`, `equipment-side.png`, `hands-and-weapon.png`, `hand-detail.png`, `body-model.png`: full exported-model views. `asset-audit.json` and `export-contract.json` record hashes, clips, material slots and triangle counts.
- `editmode.xml`: 109/109 passed. `playmode.xml`: 106/106 passed. `unity/tools.sh build`: `STAR_PROVING_BUILD_PASS 469487272`.
- `player/`: six muted native macOS Development Player captures at idle, fire, smoke, settled, motion and two-seat world states. The diagnostic process exited 0 and `player.log` contains `VECTOR_REVIEW_COMPLETE` with no logged exception. The samples use real session actions and synthetic gamepads.

These are implementation and diagnostic results. The user still needs to judge the grip and overall visual quality in the Player. No physical controller, television or performance acceptance is claimed.
