## Context

База `6f8ed15` содержит honest perception и NavMesh физической арены, но только test-only follower. Read-only Astra memo 2026-09-20 подтвердил отсутствие новых продуктовых развилок и рекомендовал NavMesh queries → обычные действия → существующий motor.

## Goals / Non-Goals

**Goals:** реальный reusable исполнитель движения, data-only route/state, явные опоры/переходы, ограниченный recovery, профиль и native evidence. Полный объём миграции отслеживается в `docs/UNITY_MIGRATION_MATRIX.md`.

**Non-Goals:** NavMeshAgent как второй motor, полный planner/weapon/support, расширение состава, purchases, browser acceptance и полная схема replay.

## Decisions

1. `NativeNavigationProvider` владеет native query и ссылкой на конкретную arena. Native route содержит скопированные позиции и semantic support/transition IDs. Фиксированная арена объявляет lower/upper supports, stairs/ramp с концами; будущий генератор заменит fixture definition. Отвергаем неверный этаж, partial route и чужую поверхность. Выбор NavMesh вместо browser A* сохраняет готовый геометрический backend; NavMeshAgent не используется из-за конкурирующей власти над Transform.
2. Исполнитель получает собственные pose/life, copied knowledge и navigation capability. Enemy goal обновляется только записью knowledge, сохраняет provenance и снимается при expiry. Static search goal допустима как знание карты. Нет session/raw-enemy доступа.
3. Waypoint проверяется в XZ и по высоте/опоре. Путь перепроверяется при смене цели, restore или recovery; запросы ограничены профильной частотой. Новая цель во время перехода не заставляет идти к прежнему входу. World steering переводится в local Move с учётом применяемого yaw, оставляя будущему weapon policy возможность отдельного aim.
4. Progress измеряется уменьшением оставшегося пути, а не колебаниями позиции. Recovery — ограниченные боковые/обратные обычные команды, затем replan; исчерпание попыток даёт явный Blocked. Collider остаётся включён. Состояние и snapshot хранят intent, cursor, deadlines и attempt count; restore атомарно валидирует конфигурацию и перепроверяет путь, не обещая bitwise physics.
5. Новый `unity-bot-navigation-v1@1` использует общий descriptor registry/Inspector; initial tuning переносится из botRules и уточняется native motor fixtures. Snapshot/profile не держат Unity handles. Числа протокола тестов/diagnostic UI не являются shipping tuning.
6. Development Player review подключает навигационные actions перед единственным `Session.Tick`: Observe → Sample → navigation → session. Pause/results не тикают AI; Repeat создаёт новые экземпляры. Это реальное движение через штатный lifecycle, но без shipping bots setup.

## Risks / Trade-offs

- `SamplePosition` игнорирует препятствия и может выбрать другой этаж → ограниченные sampling/height guards и semantic transition validation. Источник: Unity 6.3 `AI.NavMesh.SamplePosition`.
- `CalculatePath` синхронный и возвращает true для partial → проверять PathComplete, измерять запросы и не выполнять на каждом tick. Источник: Unity 6.3 `AI.NavMesh.CalculatePath`.
- Global NavMesh API → route проверяется относительно geometry/границ конкретной арены и требует активную owned surface; повторное создание и чужие поверхности получают негативные fixtures. Для одинаковых fixed fixtures это доказательство физической допустимости в owned PhysicsScene, а не эксклюзивного происхождения native query из одного NavMeshData. Unique agentType не нужен этому срезу; для будущих неодинаковых generated scenes контракт проверяется заново.
- Успех DTO/tests ошибочно принимается за bots → отдельный статус navigation diagnostic; полный AI/roster audit остаётся открытым.
- Свободный переход может ошибочно активировать recovery → оба направления, три lanes, mid-transition replan и отсутствие recovery в tests.

## Migration Plan

Добавить contracts/profile, adapter/controller, tests, native diagnostic и evidence. Не менять browser runtime. Проверенный срез коммитится отдельно; никакого Unity deploy вместо browser. Следующие changes продолжают полную цель в этой сессии согласно уточнению пользователя.
