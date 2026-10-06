# 14 — Player wall clearance

> Статус: CLOSED — остаток работ CANCELLED / SUPERSEDED переходом на Unity, 2026-09-19, по явному решению пользователя. Исторические реализации и результаты ниже сохранены; незавершённая приёмка не объявляется успешной. Не возобновлять browser-разработку или интеграцию по этому handoff. Unity получает новую реализацию и тесты. См. `../../BROWSER_BACKLOG_CLOSURE.md`.

## Scope

`prototype-v1` uses a 0.55 m participant capsule radius and 0.35 m half-height, preserving 1.80 m total height while keeping the robot body outside canonical walls. Weapon clipping uses presentation-only retraction into an always-visible folded pose; renderer-owned conservative envelopes also protect it from wall-attached decor without changing simulation collision.

## Acceptance

- Deterministic collision, spawn and procedural-arena validation use the new capsule geometry.
- Combat hit volumes remain unchanged.
- Muted in-app Browser evidence covers body and weapon at walls, corners, portals, ramps, barriers and decorated walls in first- and third-person presentation.
