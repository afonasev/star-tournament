## Context

См. [proposal.md](proposal.md). Simulation уже публикует `shotgun-shot` с origin и упорядоченными pellet directions, а для каждой дробины — `shotgun-impact` с позицией, normal, target/surface identity и pellet index. `FirstPersonRenderer` сейчас сужает эти данные до shooter и позиции и рисует единственный cyan sphere impact; `shotgun-impact` events в итоговом snapshot сортируются по `eventId`, поэтому их порядок относительно shot event не является контрактом.

## Goals / Non-Goals

**Goals:**

- Использовать уже authoritative shot/impact данные для кратких трасс, двухствольного кинетического muzzle effect и material-aware impacts.
- Сохранить rewind handling, resource lifecycle и существующие профильные длительности feedback.
- Сделать эффект видимым и читаемым в first-person browser match без аудио.

**Non-Goals:**

- Не менять оружие, pellet pattern, damage, collision, event schema, GLB assets, audio и Game Design Lab.
- Не добавлять blood, gore, persistent decals, projectile entities или динамическое освещение.
- Не вводить новые числовые gameplay/visual controls: lifetime берётся из уже profile-owned `muzzleFeedbackSeconds` и `impactFeedbackSeconds`; постоянные параметры геометрии являются renderer technical invariants.

## Decisions

### Renderer собирает presentation из существующих событий

`FirstPersonRenderEvent` сохранит необходимые поля существующих snapshot events. До обработки каждого snapshot renderer построит индекс shot events и индекс impacts; для дробины конкретного shot он вычислит canonical impact event ID из tick, shooter ID и pellet index, вместо зависимости от порядка events. Это допускает несколько shooters в одном tick и не требует менять сериализацию/state hash.

Альтернатива — добавить shooter ID в impact event — отвергнута: данные уже однозначно связываются через canonical ID, а изменение расширило бы deterministic event schema без продуктовой необходимости. Альтернатива «сопоставлять соседние события» отвергнута, поскольку reducer сортирует events по ID.

### Один pooled transient effect граф на выстрел

Успешный shot создаёт краткий renderer-owned effect group: две warm-white/amber muzzle shapes на existing muzzle mounts, лёгкий local дым и line/quad trail на каждую дробину. Trail начинается в event origin и оканчивается в authoritative impact position; для отсутствующего impact завершает visual range в уже profile-defined shotgun range. Перед созданием эффект проходит frustum/budget filtering; impact и hitmarker не зависят от этого фильтра.

Вместо отдельного mesh/material на каждую дробину renderer использует разделяемую геометрию/материалы и один transient root на shot. Это делает cleanup, rewind и quality degradation детерминированными для presentation и не раздувает draw calls. Альтернатива с физическими particle bodies отвергнута: она добавляет frame-timing simulation и не несёт gameplay ценности.

### Impacts зависят от material class, а не от новых surface contracts

`targetId !== null` выбирает robot impact: бело-оранжевые искры и короткие небиологические shell fragments. `surfaceId !== null` выбирает hard-surface impact: искры, маленький светлый chip и направленное от normal рассеяние. Renderer не создаёт persistent decal или geometry, не трогает occlusion/collision и удаляет group по existing impact lifetime.

Альтернатива с полной material taxonomy arena отложена: текущий combat event уже различает robot и static surface; новая taxonomy потребовала бы расширения arena/simulation contracts без нужды для этого slice.

### Scope VFX и проверка

Мuzzle/recoil остаются только для local first-person viewmodel, как текущий contract. World-space trails и impacts создаются из всех видимых events, поэтому выстрел бота остаётся читаемым наблюдателю. На rewind renderer очищает все transient effect groups вместе с existing presentation transients. Unit tests фиксируют exact ID association, target/surface variant, expiry и rewind; muted browser playtest подтверждает local shot, wall hit и robot hit.

## Risks / Trade-offs

- [12 дробин создают заметный burst geometry] → shared resources, one group per shot, frustum filtering и удаление по profile duration.
- [Impact arrives раньше shot после event sort] → pre-index all events before effect creation и canonical ID lookup.
- [Трассы могут закрыть прицел] → short fade и нижне-правний muzzle origin; no persistent trail.
- [Скриншот не ловит короткий effect] → добавить deterministic visual debug fixture/paused review state либо capture активного shot состояния в browser QA.

## Migration Plan

1. Расширить renderer event adapter и заменить placeholder meshes новым transient effect graph.
2. Добавить tests и обновить `GAME_SPEC.md`/журнал с утверждённым кинетическим visual direction.
3. Выполнить typecheck/unit suite и muted first-person browser playtest в worktree dev server; приложить screenshots.
4. При регрессии удалить новый renderer-only graph: existing simulation events, profile и saved state остаются совместимы.
