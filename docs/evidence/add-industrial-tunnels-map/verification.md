# Verification — industrial tunnels

Candidate source: `71a0f0b9756e82102a47af89afe51e5f6e45c9f4`. Exact retained app and checksum: `candidate.json`. Local macOS Development build; human acceptance pending.

- Full `make check`: PASS, 294/294 EditMode + 184/184 PlayMode, no skipped tests, native build exit 0. Run on the feature tree before rebasing onto main (original feature commit f6cf808, baseline 706f7a8); XML and compressed logs in `full-check/`.
- Rebased onto main 49499be, preserving integrated weapon colors / Pulse visibility. Rechecked affected contracts: 48/48 profile/history/map EditMode, 1/1 tunnel physics PlayMode, 9/9 weapon materials / colors / rocket presentation PlayMode. XML, scope and logs in `integration/`. Final two source edits only corrected QA camera placement and boxed the SendMessage menu argument; actual native smoke below validates that helper.
- Tunnel physics checks all spawn→pickup/spawn paths, four mutually hidden initial placements, all 16 ramps in both directions and three lateral lanes (96 actual motor traversals), no jumps/recovery; decoration adds no colliders.
- Exact final candidate: native build PASS; muted Player exit 0, no exceptions or script errors. Menu selection, 2 participants, six pickup anchors / no speed, frozen Repeat, 4 viewports: PASS. Natural 5-minute bot match (seed 20261002): 44 kills, 73 pickups, naturally finished. `player/complete.json`, `natural-match.json`, actual 1920×1080 screenshots and compressed Player log.
- Screenshots use scripted camera placements with simulation held, then a separate naturally simulated bot match. They are not a performance benchmark or physical-controller acceptance. FPS 0 on the first capture is the initial sampling window; 60 FPS/4K/TV and four physical devices remain unverified.
- URP emits a shadow-atlas resolution warning for the white point light. No render exceptions or pink/missing materials observed. Human visual/play acceptance remains open.
- Published Combat Bowl data is unchanged. Scene diff only adds the two tunnel profile fields; unrelated build-generated rewrites were restored in this owned worktree.

Selected requirements validated strictly: add-industrial-tunnels-map, industrial-tunnels-arena, authored-arena-selection, game-design-lab. Full planning inventory has unrelated existing invalid changes; they are outside this delivery.
