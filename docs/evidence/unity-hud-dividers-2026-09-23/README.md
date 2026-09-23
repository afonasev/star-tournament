# Black viewport dividers: native Player evidence

- Source: local macOS Player built from the `codex/add-viewport-dividers` worktree on 2026-09-23; build marker `STAR_PROVING_BUILD_PASS 460254721`.
- Muted `-layoutReview` at 1920×1080 produced the PNG and JSON pairs for 2 live, 3 live, and 4 armor states. These diagnostic frames show a vertical black separator at two seats and vertical plus horizontal separators at three and four seats. The shared timer draws above them.
- `editmode.xml`: 114 passed. `playmode.xml`: 75 passed, including layout assertions for divider count, position, width, opacity, and click-through behavior.
- This is local visual evidence. Human acceptance and physical TV/controller checks remain pending.
