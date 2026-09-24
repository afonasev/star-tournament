# Combat Bowl surface flicker — native Player review

- Map: `combat-bowl-v1@14`; authoring: `combat-bowl-ring-r7-authoring-v1@8`; presentation: `orbital-league-ring-v1@8`.
- Capture state: scripted eight-participant FFA, four local viewports for timing, seed `20260923`, muted audio, native macOS Development Player. `review.json` confirms Repeat.
- Images: `16-live-hall-opponent.png` (central hall), `08-wide-west-gateway.png` (wall/floor junction), `09-wide-diagonal.png` (upper diagonal deck), `17-live-basement-opponent.png` (tunnel walls).
- Checks: EditMode 109/109 and PlayMode 107/107 passed; `editmode.xml` and `playmode.xml` are retained here. The focused presentation suite also passed 12/12, including coplanar floor/wall ownership and collision invariance.
- Diagnostic on current Mac at 1920×1080: 360 frames, four viewports, median 15.75 ms and p95 17.32 ms (`performance.json`). This is not reference-hardware or human acceptance.
- Native Player visual inspection of these captures found no competing floor/wall patches in the central hall, west gateway, basement, or diagonal deck. A static image cannot prove every moving-camera angle; human Player review remains open.
