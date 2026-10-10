# Menu credit footer

User request: add “© 2026 Evgeniy Afonasev · afonasev.tech · Made with Codex” to the match-loading footer and every menu screen. Scope: shared noninteractive menu footer, match-loading footer, preserve startup credit. Common footer remains visible on submenus/pause/results and hidden during running gameplay. Release label moved slightly above it.

QA: UI gate + affected native-menu/loading screen fixtures + graphics-enabled Editor Canvas captures. No full gameplay suite, Player build or deployment required for this local copy/layout change. Human runtime/device acceptance remains separate.

Results: UI gate 16/16, latest affected graphics scenarios 9/9. Initial footer-results test remained in overtime; fixture now gives an explicit winner and completes without changing runtime. 11 native Canvas captures (1600×900) inspected, including main/release label, match-loading, map, roster/editor, identity, profiles, settings, Lab, pause and results. Footer text remains readable and clear of controls. QA summary: qa/summary.json.
