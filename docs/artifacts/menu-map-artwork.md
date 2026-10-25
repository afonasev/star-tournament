# Selected map artwork in the menu

User request: replace current map menu pictures with the four selected renders.
Small explicit asset/UI replacement under conditional-procedures.md; no new UI flow, gameplay or full OpenSpec change needed.

Owner: current chat 01a16f94-ee31-7320-98bf-6296736fa8bd. Worktree `/Users/eaafonasev/.codex/worktrees/menu-map-artwork/star-tournament`, branch `codex/menu-map-artwork`, baseline `main@af700aa8d6e9248ec3ebc61c9f64505ef6683061`. Primary checkout's existing ProjectSettings and other work are preserved. Capture-only branch remains separate.

Assets copied byte-for-byte from saved selections in shared planning: Well1, Lunar1, Tunnels2, Combat Bowl2. Shared ArenaPreviewGraphic now uses a RawImage with centered cover crop; compact lobby card, selector and historical setup card use the same source. No stretched image; existing map focus/confirmation behavior stays intact.

Minimum check selected before execution: GPU PlayMode `SingleScreenLobbyTests.SelectedMapArtworkFollowsPreviewAndConfirmation`. It loads the real menu, focuses all four maps, checks artwork/preview-only selection, confirms each map, verifies the compact card, and captures both surfaces. No full suite, natural matches or Player build are needed for this UI-only diff. Exact source SHA256 compared against selection.json.

Command: STAR_TOURNAMENT_LOBBY_QA_DIR=<output> STAR_TOURNAMENT_QA_LAB_HISTORY=<output>/lab-history.json UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -runTests -testPlatform PlayMode -testFilter SingleScreenLobbyTests.SelectedMapArtworkFollowsPreviewAndConfirmation -testResults <output>/results.xml -logFile <output>/editor.log. Wrapper adds -noaudio -muted.

Durable evidence: shared planning `docs/evidence/menu-map-artwork-2026-10-25/`. Results/integration recorded after verification. Player build/publication are outside this request.

Verification: Passed1/1; eight actual1920×1080 UI captures inspected (compact lobby and map picker for each map). All four resource PNGs match the saved selection SHA256 byte-for-byte. Canonical single-screen-match-lobby specification and GAME_SPEC updated with exact chosen artwork mapping. No runtime/gameplay/build changes.
