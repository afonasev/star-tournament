## Context

Generator создаёт perimeter с `wallHeightMeters`, но применяет скрытый `min(wallHeightMeters, 3.5)` к primary interior architecture. Presentation ceiling ориентируется на максимальную canonical surface высоту, поэтому формируется разрыв.

## Goals / Non-Goals

**Goals:** убрать height cap для primary walls, сохранить низкие canonical buttress как явно intentional cover и верифицировать deterministic identities.

**Non-Goals:** не менять размеры арены, doorway/corridor widths, rendering textures, profile controls или правила движения.

## Decisions

Primary architecture получает `wallHeightMeters` прямо из active generator preset. Buttresses сохраняют отдельно заданную долю высоты primary wall: они семантически являются укрытиями, а не частью floor-to-ceiling chains. Это меняет canonical content, поэтому fixture hashes обновляются, а acceptance проверяет full-height primary surfaces для всех size.

## Risks / Trade-offs

- [Изменятся arena hashes] → обновить versioned fixture expectations и проверить replay-compatible identity rejection.
- [Больше colliders высоты] → выполнить gameplay validation и collision test corpus для всех sizes.
