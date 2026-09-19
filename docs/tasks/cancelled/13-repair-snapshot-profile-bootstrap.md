# 13 — Repair snapshot profile bootstrap

> Статус: CLOSED — остаток работ CANCELLED / SUPERSEDED переходом на Unity, 2026-09-19, по явному решению пользователя. Исторические реализации и результаты ниже сохранены; незавершённая приёмка не объявляется успешной. Не возобновлять browser-разработку или интеграцию по этому handoff. Unity получает новую реализацию и тесты. См. `../../BROWSER_BACKLOG_CLOSURE.md`.

- Change: `repair-snapshot-profile-bootstrap`.
- Worktree: `/Users/eaafonasev/.codex/worktrees/a891/star-tournament`.
- Branch: `codex/repair-snapshot-profile-bootstrap`; base `3ae6bd1`.
- Scope: playable snapshot parser/cache, serializer/hash and fixed-step boundary receive the exact validated active profile context. Historical v6 snapshot readability remains explicit; arbitrary different identities still fail before an action frame.
- Checks: `npm test` — 59 files / 348 tests; `npm run build`; `openspec validate repair-snapshot-profile-bootstrap --strict`; `git diff --check`.
- Visual evidence: in-app Browser loaded `prototype-v1 v7` (`fnv1a64-v1:369a772fc3ab5a9f`), created the match and rendered its first-person arena/HUD with no `SimulationSnapshotError`. Pointer lock was denied by the browser automation surface, so keyboard/mouse movement is not claimed.
- Stand: `http://127.0.0.1:4189/?muted=1` (HTTP 200 checked 2026-09-06).
- Screenshot: [initial-match PNG](../../../../visualizations/2026/09/06/01a0760f-45ab-71b2-ac01-8c1d6e143595/snapshot-profile-match.png), seed `3947830884`; created by the project browser script after match creation and visually inspected.
- Open: do not merge, archive or deploy in this task.
