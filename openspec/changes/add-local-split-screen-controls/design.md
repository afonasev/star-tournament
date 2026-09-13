## Context

См. [proposal.md](proposal.md). Текущий runtime передаёт один physical keyboard/mouse adapter одному local participant и renderer рисует одну camera на canvas. Simulation уже принимает serializable actions по participant ID и обрабатывает managed participants в общем tick, поэтому источник ввода и presentation нужно расширять отдельно от gameplay rules.

## Goals / Non-Goals

**Goals:**

- Сделать device-to-seat bindings browser-owned и сохранить action frames device-neutral.
- Рендерить несколько камер одним canvas через viewport/scissor; DOM HUD привязать к local seat, а не renderer object.
- Сохранить один deterministic simulation tick и единую pause boundary для всех людей.
- Обеспечить понятный reconnect path без неявной смены игрока или устройства.

**Non-Goals:**

- Online transport, authority, prediction/reconciliation и cross-device network play.
- Новые gameplay actions, способности, rebinding произвольных кнопок и несколько keyboard/mouse пар.
- Изменение баланса, arena generation, combat, replay semantics либо правила ботов.

## Decisions

### Device binding отдельно от participant state

Setup сохраняет versioned binding descriptor вместе с local-seat configuration: `keyboard-mouse` либо stable browser gamepad identity plus selected connection slot. Валидатор допускает один keyboard/mouse descriptor и уникальные gamepad descriptors. В snapshot остаются participants и action frames, но не browser API, `Gamepad` objects или mutable device state.

Альтернатива — хранить индекс Gamepad API в snapshot — отклонена: индекс не стабилен между подключениями и нарушает portable replay boundary.

### Per-seat adapters собираются перед одним frame

Keyboard/mouse и gamepad adapters имеют одинаковый lifecycle `drain(tick)`, очищают held state атомарно и публикуют local-only UI intents. Runtime объединяет outputs в один canonical ordered action frame. `View` и `Tab` — held scoreboard intents; `Menu/Start` и `Escape` — edge pause intents. Loss of focus, pointer lock или assigned gamepad вызывает общий pause до simulation step.

Альтернатива — отдельный runner на viewport — отклонена: она дублирует tick/RNG и делает взаимный combat недетерминированным.

### Камеры и HUD принадлежат local seat

Renderer выбирает всех `local-seat` participants в stable semantic-ID order, строит per-seat camera presentation и последовательно применяет viewport/scissor на одном canvas. Layout: 1 — full, 2 — equal left/right, 3 — 2×2 with three equal cameras and a fourth persistent standings panel, 4 — equal 2×2. React DOM получает viewport rectangles and seat identities, накладывает HUD только на игровые прямоугольники, а третий-player standings panel остаётся DOM-only и читает existing simulation projection.

Альтернатива — четыре canvas — отклонена из-за лишних WebGL contexts, inconsistent graphics lifecycle и более высокого TV performance риска.

### Reconnect — explicit и глобальный

При disconnect binding становится unavailable, held input очищается и entire match pauses. Resume возможен только если original device returns and matches its binding, либо после return to setup and explicit rebind; никогда не выбирается «первый свободный» controller. Пауза по pointer-lock распространяется на gamepad seats, потому что browser focus больше не доказан.

### Performance boundary

Split-screen multiplies camera and HUD work. Existing graphics quality remains presentation-only; change добавляет benchmark scenarios for 1/2/3/4 seats and records WebGL submissions, calls, triangles, HUD cadence and input latency. Exact target hardware/FPS stays an open product decision, so this change preserves existing portable gates and reports comparative evidence rather than inventing a new hard budget.

## Risks / Trade-offs

- [Browser gamepad identity changes after reconnect] → require user-visible device label plus explicit binding match; do not auto-reassign.
- [Three cameras plus persistent standings make 3-seat GPU bound] → one canvas/scissor, existing quality tiers and per-layout performance evidence.
- [Keyboard pointer lock conflicts with shared pause] → global lifecycle pauses every adapter and requires an explicit resume action.
- [Legacy saved configuration lacks bindings] → parse as the historical single keyboard/mouse seat only; reject any ambiguous multi-seat migration with an actionable error.

## Migration Plan

1. Bump and validate match-configuration schema with explicit migration for historical single-seat payloads.
2. Land deterministic configuration/action-frame/unit tests before browser presentation.
3. Add adapters, renderer/HUD layouts and setup UI in the isolated worktree.
4. Run muted automated checks, browser checks for every supported layout, then real-controller acceptance and performance evidence before integration.
5. Roll back by restoring the prior schema/runtime; old single-seat configurations remain readable through the compatibility path.
