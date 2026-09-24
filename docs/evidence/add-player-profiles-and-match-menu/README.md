# Player profiles and match menu: native evidence

The `player-review` images were captured from the macOS Development Player at
1920 × 1080 with muted audio and synthetic Input System gamepads. The review
uses an isolated `-profilesPath`; `profiles.json` is fixture data, not a user
profile. `01`–`08` and `10` are from one Player process. `09` is from a second
process using the same profile file, so it checks persistence across restart.

`complete.txt`, `restart-complete.txt`, `player-log-summary.txt`, and the
per-frame JSON files record the automated observations. The images show the selected
“Арена / эфир” menu direction, profile creation, disabled occupied profile,
blocked start after disconnect, and Y rebind into the disconnected seat.

EditMode: 110/110 passed. PlayMode: 106/106 passed. macOS Player build passed.
These are automated checks; physical gamepad, mouse, and human visual acceptance
remain separate.
