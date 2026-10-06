## Context

См. proposal.md; NativeCombatSession уже владеет combat adapter, CombatLife — life, motor — Transform. Исторический match-session-lifecycle и prototype-v1@12 задают scoring, а GAME_SPEC §5 — UI/end rules. Read-only Astra review выявил sequential Shoot→damage: он подавляет взаимный lethal fire внутри tick.

## Goals / Non-Goals

**Goals:** подключённый четырёхместный FFA с отдельным data-only match state, безопасным lifecycle и native UI.
**Non-Goals:** общий replay/restore, teams/bots/layouts, полная production menu/Balance Lab; см. proposal.

## Decisions

- Data-only NativeMatchState владеет stats, ledger, chains, ticks/config/result; NativeCombatSession передаёт только applied damage и death и завершает tick после reduction. Встроить scoring в UI отвергнуто: presentation не владеет правилами.
- После timers/motors собирается один live-target snapshot и admitted shots; все pellets разрешаются до damage. Stable reduction сохраняет overkill/stale-life handling и позволяет mutual lethal. End evaluation идёт после всех deaths, перед respawn; finished не тикает и не принимает damage. Tick counter исключает накопительную ошибку float на duration/assist/gap boundary.
- Новый unity-native-match-v1@1: descriptor-backed duration/target и scoring; настройки валидируются теми же metadata и шагами, cumulative totals монотонны. Копия профилей/config замораживается на Begin и сохраняется при Repeat.
- Repeat пересоздаёт session/state/presenter, сбрасывает motors в authored spawn и отключает старую session. Presenter Dispose отписывает handlers, немедленно скрывает и удаляет corpses. UI/cameras/arena/NavMesh/EventSystem переиспользуются, поэтому reload сцены не нужен. Полный scene reload отвергнут из-за device ownership и риска дублей.
- Per-seat Tab/View table поверх своего viewport, общая full-screen result table, per-seat timer; общая типографика/сеточные размеры выводятся из существующего ui.fontSize. Колонки выровнены отдельными cells. Цвета — стабильная identity palette, не tuning. Menus освобождают cursor, выбор кнопок доступен gamepad; настройки config только в setup. Pause/focus/disconnect продолжают останавливать все clocks; Repeat без ready devices недоступен, setup позволяет переназначение.

## Risks / Trade-offs

- Deferred Unity Destroy → сначала деактивация corpse и отписка; тест удерживает ссылку старой session и проверяет отсутствие mutation.
- Shot reduction может повлиять на старые тесты → regression плюс mutual lethal/последний tick, real adapter tests.
- Маленький split viewport → screenshot FHD/4K, короткие заголовки и profile-derived font.
- Синтетический review → явно DIAGNOSTIC, physical four-controller/TV и performance остаются открытыми.

## Migration Plan

Изолированный worktree, база fast-forward e735de7. Tests→build→muted Player evidence→handoff 19→отдельный commit. Main/integration/archive/deploy не выполняются.
