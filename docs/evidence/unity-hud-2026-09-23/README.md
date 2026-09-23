# Native Unity HUD visual evidence

- Source: `refine-unity-split-screen-hud` code worktree, Unity 6000.3.23f1, macOS Development Player, Full HD 1920×1080, build marker `460115164`.
- Capture: `-layoutReview -layoutEvidence ...`; synthetic gamepads, `AudioListener.volume = 0`. This is native Player visual evidence, not physical controller, TV or performance acceptance.
- `1-live`, `2-live`, `3-live`, `4-live`: one opaque timer by local viewport layout; smaller participant-colored names at the bottom of each viewport; icon + value surfaces in the lower corners.
- `4-armor`: the first local seat has 50 armor via a diagnostic session snapshot. No underline appears under any value; the armor pair disappears at zero in the other captures.
- `4-overtime`: data-only match clock fast-forwarded to overtime; the single shared panel shows `ОВЕРТАЙМ`.
- Pixel inspection of `4-armor.png`: heart 25 px vs health number 25 px; shield 25 px vs armor number 26 px; paired cartridges 26 px vs ammo number 25 px. Measurements use colored glyph bounds in the first viewport at this resolution.
- JSON files alongside each PNG record seat count, viewport rectangles, match phase, mute state and screenshot dimensions. Native layout review completed without a Player exception.

Human visual acceptance remains separate from these diagnostics.
