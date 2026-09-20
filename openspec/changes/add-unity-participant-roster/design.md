## Context

База `45e7f27` имеет reducer 2–8 участников, но сцена/presentation/spawn/таблица привязаны к 2–4 local seats. Мотивация и границы в proposal. Read-only Astra memo 2026-09-20 подтверждает выбранный prerequisite; новых продуктовых развилок нет.

## Goals / Non-Goals

Цель — явное ownership participant runtime против local views/devices, без переписывания motor/combat. Shipping planner и UI создания ботов следуют отдельно; unsupported bot action source блокирует старт. Не вводим сетевую модель или полную replay-схему.

## Decisions

- Сохраняем stable participant index в пределах session. Immutable metadata содержат kind, base name, resolved color и nullable bot difficulty; diagnostic fixture — отдельный kind. Teams остаются gameplay roster. Отдельный immutable mapping seat→participant валидирует уникальность/диапазон и ровно одно место на local human.
- N motors/bodies/lives/actions и H cameras/arms/HUD. Не увеличиваем все массивы до8 с null views: это сохранило бы ошибочную модель. Session остаётся единственным владельцем combat/life, renderer читает его.
- Каждый advancing tick очищает actions; input каждого seat записывается в mapped participant, затем nonlocal sources получают собственные slots. ShowRoster/pause/devices — local-seat actions; индексы combat/death/scoring — participant. Diagnostic fixtures не получают synthetic devices.
- Presentation отдельно рендерит N bodies и H local views. Death/corpse работает для любого участника; killcam только для mapped local human, killer lookup по всем N и точному life ID. Nickname берётся из frozen metadata.
- Culling использует четыре owner-body layers по local seat, четыре arms layers, общий nonlocal visual layer. Capsule layer остаётся общим gameplay layer. Participant7→seat0 получает visual owner layer0; body index никогда не складывается с layer base.
- Standings имеет capacity header+8+2, actual row count из состава, утверждённую palette12 из browser reference, отдельные kind labels и mapping-based human highlight. Восьмистрочная четверть FHD проверяется визуально.
- FFA/teams используют общий initial allocator с physically valid distinct candidates, заранее рассчитанной несовместимостью overlap/separation, стабильным поиском и pruning. Node budget и candidate budget имеют profile metadata; failure различает NoValidPlacement, SearchBudgetExceeded и CandidateBudgetExceeded. Initial-only grid использует существующие geometry/spacing/separation параметры для восьми FFA без ослабления дистанции. Полная успешная расстановка предшествует активации capsules. Separation не уменьшается для прохождения теста.
- Repeat сохраняет exact frozen roster/metadata/mapping/profiles/device assignments, пересоздаёт session/presentation и очищает input/corpses/events. Menu освобождает session, изменения setup снова доступны. Focus loss/disconnect проверяют только H устройств, останавливая всех N.

## Risks / Trade-offs

- [Случайное совпадение seat и participant] → обязательный test seat0→participant7 и перестановки.
- [Hidden cameras/лишние arms] → runtime counts 1+7 и4+4; повторное 4→1→4 проверяет disposal.
- [Combinatorial spawn search] → budget/nodes/time telemetry, невозможные fixtures, atomic failure; exhaustion не называется доказанной невозможностью.
- [Слишком мелкий scoreboard] → FHD/4K quarter/full views восьми участников и двух team totals.
- [Performance] → measured CPU/frame diagnostic восьми тел; target60FPS/physicalTV остаются открытыми.

## Migration Plan

Сначала contracts/tests, затем ownership/action/presentation/spawn/table integration, затем opt-in Player journey и регрессия обычного setup. Полные EditMode/PlayMode/build, Astra review, evidence и отдельный commit. Изолированный worktree сохраняет базу и обратимость; main/archive/deploy не входят в этот срез.

Read-only implementation review: исправлены rollback из mutable invalid profile, частичная активация capsules и team-total color ownership; повторный review блокеров не нашёл. Preflight сохраняет recoverable setup, preview использует last-valid frozen copies. Legacy CLI camera probe ограничен реальным числом local views.
