## Context

См. `proposal.md`. Сейчас renderer накладывает одинаковую map на base shell, GLB instances и мелкие box-details; несколько из них занимают близкие или coplanar planes. Accent base surfaces map не получают, поэтому на игровой дистанции появляются крупные cyan-участки. Текущая графика не изменяет simulation data.

## Goals / Non-Goals

**Goals:**

- Создать отдельные material families для wall/ceiling, floor и accent architecture.
- Сделать geometry ownership однозначным: GLB instance либо procedural fallback, а не обе визуальные детали на одном plane.
- Сохранить texture factory и sampler settings presentation-only.

**Non-Goals:**

- Не менять `ArenaDefinition`, GLB authored geometry, collision proxies, lighting model, quality menu или texture compression format.
- Не добавлять team color в окружение и не менять physical clearance.

## Decisions

### Surface-class material families

Wall shell, ceiling-facing architecture и panels получают одну светлую panel map с restrained gray detail. Floor продолжает использовать dark navy composite map. Accent solids получают отдельную pale panel map с узкой cyan edge treatment, а не cyan base-color fill. Это сохраняет visual hierarchy `wall > floor > accent` и не требует новых semantic slots.

### No coplanar detail strategy

Base shell остаётся единственным mesh на canonical wall plane. Detail boxes получают fixed outward presentation offset, а GLB bay replaces procedural bay fallback; frame/seam geometry сокращается до одной separated layer. Polygon offset остаётся defensive fallback для authored GLB triangles, но не используется для разрешения overlapping design.

### Texture source approach

Обновить project-bound raster assets на tile-safe graphic panel surfaces без текста и participant identity. Их размеры и repeat settings остаются совместимы с существующим browser renderer; authored compression остаётся будущим заменяемым этапом согласно `GAME_SPEC`.

## Risks / Trade-offs

- [Новая map может дать слишком частый pattern на длинной стене] → normalise repeat по surface span и проверить representative small/medium/large camera views.
- [Outward visual offset может создать ложное впечатление другой коллизии] → ограничить offset декоративной толщиной и проверять с близкой камерой; canonical shell остаётся visual boundary.
- [GLB имеет собственные UV/material assumptions] → clone materials/UV только presentation instance и проверять disposal.

## Migration Plan

1. Обновить GAME_SPEC/changelog и OpenSpec contracts.
2. Изменить texture assets и renderer surface-class material assignment.
3. Удалить coplanar presentation overlaps, добавить unit tests.
4. Проверить production build, muted browser playtest и screenshots; rollback удаляет только presentation assets/factory mappings.
