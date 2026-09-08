## Context

См. `proposal.md` — Why. Текущий collision adapter создаёт одну вертикальную capsule на participant: она одновременно проверяет static geometry и живых участников. Её радиус меньше audited radial bounds robot body, поэтому renderer может пересекать canonical wall, хотя simulation считает body допустимым. Оружие и руки — renderer-only GLB и не должны становиться частью simulation или weapon rules.

## Goals / Non-Goals

**Goals:**

- Сохранить deterministic, renderer-independent simulation authority.
- Сделать живое тело визуально безопасным у canonical static geometry без изменения combat hit volumes.
- Не блокировать mouse/gamepad look и не менять first-person action mapping, pointer lock, pause или viewport policy.
- Отвести weapon presentation у static wall без gameplay side effect.

**Non-Goals:**

- Не превращать weapon в physical collider, не расширять participant-to-participant blocking и не менять рост/боевые габариты тела.
- Не менять geometry/materials арены, сетевую модель, replay payload или трупы.
- Не добавлять collision из GLB bounds в runtime simulation.

## Decisions

### Переотбалансированная profile-owned capsule тела

Единая body capsule получает radius 0,55 м, достаточный для audited world robot body плюс skin, а half-height уменьшается с 0,50 до 0,35 м. Полная высота сохраняется: `2 × (0,55 + 0,35) = 1,80 м`; feet/camera anchor не сдвигаются. Capsule по-прежнему применяется к canonical static geometry и живым participants; она не участвует в damage volumes.

Альтернатива — увеличить `capsule.radius`. Она одновременно меняет высоту, feet/camera anchor и participant blocking, поэтому отклонена. Ориентированный weapon collider также отклонён: он требует swept rotation/position policy рядом со стеной и меняет утверждённый свободный look.

### Единый capsule solve и совместимость

Существующий deterministic capsule solve остаётся единственным movement path. Overlap/spawn/arena traversal автоматически используют обновлённый profile radius; collision identity и profile revision повышаются, чтобы старые checkpoint/replay были явно несовместимы.

### Presentation-only weapon retraction

Renderer строит presentation-only query representation из validated `ArenaDefinition` и renderer-owned conservative envelopes wall-attached facade/portal/lamp decor, а не из Three.js objects. Для world weapon/viewmodel он вычисляет безопасную глубину вдоль видимого weapon direction. При недостаточной глубине weapon и arms плавно переходят в минимальную всегда видимую сложенную позу перед camera near plane: они не scale/fade и не уходят за камеру. Это presentation-only transform и он никогда не записывается в simulation snapshot или action state. В отсутствие presentation geometry weapon остаётся на authored mount и renderer показывает existing actionable startup error вместо частичного gameplay state.

### Валидация и совместимость

Procedural generation, traversal, participant separation и static spawn safety используют обновлённую capsule. Shipped model audit проверяет, что visible body bounds не превышают radius. Determinism workload, fresh reconstruction и reference browser budget сохраняют existing gates.

## Risks / Trade-offs

- [Два последовательных solve дают более раннюю остановку у стены] → profile radius выбирается по body bounds, не по weapon length; тестируются wall/inside-outside corner, portal, ramp и slab.
- [Изменение clearance отклонит прежние procedural seeds] → rerun corpus, сохранять stable validation reason и не менять accepted definition молча.
- [Presentation ray не совпадёт с wall или wall-attached decor] → query строится из canonical ArenaDefinition и conservative renderer-owned envelopes; visual QA проверяет first- и third-person sides, включая декорированную стену.
- [Compatibility break] → bump collision identity/profile revision; runtime rejects old artifacts before tick.

## Migration Plan

1. Добавить profile/contract и тесты до renderer integration.
2. Обновить `GAME_SPEC`, OpenSpec delta и active handoff task.
3. Прогнать unit/determinism/corpus/build, затем muted in-app Browser и screenshots с фактического worktree dev URL.
4. Закоммитить изолированную ветку; интеграция, архивирование и deploy выполняются отдельно только после подтверждённой проверки.
