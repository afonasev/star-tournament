# Approved spatial audio — 3 October 2026

Implementation/source revision: d50f44e96147c2381b2278f11e04b4d61db13186.
Final macOS candidate: /Users/eaafonasev/Projects/star-tournament/.local/player-candidates/approved-spatial-audio-d50f44e-acc48d359beb/StarTournamentProvingGround.app
Identity: local-macos-player-candidate/d50f44e96147c2381b2278f11e04b4d61db13186/487357754.
SHA256 bundle tree: acc48d359beb7fd300e7c764f659e5efb6508b5e3c71ae8dbd6990d3d11bc3f5.

Full make check passed EditMode 323/323, PlayMode 197/197 on original baseline 9c13743 plus audio patch. After rebase onto main 53af60a: unchanged audio hashes, affected integration EditMode 20/20 and PlayMode 14/14, two natural 300-second bot matches and final build passed. Verification.json describes source binding and scope.

Native Player motion smoke passed step/jump/land routing for 1 and 4 viewports with synthetic devices and muted output. Screenshots inspected. First default graphics launch stalled in Metal; owner stopped it. Retry with -force-gfx-direct completed successfully. This flag forces single threaded rendering per Unity Player command-line documentation: https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player .

Human in-game listening remains pending. See planning change integrate-approved-spatial-audio/acceptance.md. Source clips, recipes, hashes and licenses are in docs/audio/approved-2026-10-03. Evidence-only commits do not change this candidate's source revision or bytes.
