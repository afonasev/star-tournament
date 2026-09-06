## Purpose

Capability делает смерть участника читаемой спортивной анимацией робота и оставляет на арене корректно лежащее тело, направленное от смертельного выстрела.

## ADDED Requirements

### Requirement: Направленная смерть робота
Когда versioned shotgun event переводит текущую жизнь participant в dead state, renderer SHALL показать только renderer-only последовательность: реакцию на удар, падение от origin смертельного выстрела и неподвижную позу. Направление падения MUST быть горизонтальным направлением от origin выстрела к жертве; при недоступном связанном shot event renderer MUST использовать стабильный fallback, выведенный из сохранённого yaw жертвы. Анимация MUST работать для local-seat, bot и stationary fixture, не создавая collision, damage, input или camera state.

#### Scenario: Смертельный выстрел справа
- **WHEN** shotgun event с origin справа от жертвы завершает её текущую жизнь
- **THEN** робот кратко реагирует на удар и падает от origin в левую для стрелка сторону, затем сохраняет финальную позу

#### Scenario: Renderer получает уже погибшую жизнь
- **WHEN** renderer впервые получает snapshot, в котором текущая жизнь уже dead, а связанного shot event нет
- **THEN** renderer показывает стабильную лежачую позу, выведенную из yaw жертвы, без изменения authoritative state

### Requirement: GLB-тело и contact с полом
Пока authoritative corpse lifetime активен, renderer SHALL показывать GLB-робота той же participant identity с world weapon в руках вместо placeholder geometry. Корень позы MUST быть расположен на presentation floor contact, а не в центре participant capsule; identity emissives MUST быть приглушены. Тело MUST исчезнуть по существующему authoritative corpse expiry и не зависеть от respawn новой life.

#### Scenario: Тело bot после смерти
- **WHEN** погибает bot, чья simulation position является центром капсулы
- **THEN** его GLB-тело лежит на floor contact без зависания над ареной

#### Scenario: Respawn до истечения тела
- **WHEN** participant получает новую life до истечения corpse lifetime предыдущей life
- **THEN** новая active модель и тело предыдущей life отображаются раздельно до corpse expiry

### Requirement: Проверяемый death review
Muted animation-review sandbox SHALL предоставлять проверяемый прогон направленной смерти для LOD0 и LOD1: начало реакции, контакт с полом и финальную позу. Sandbox MUST сохранять renderer-only границу и не создавать authoritative match state.

#### Scenario: Проверка LOD1
- **WHEN** reviewer выбирает LOD1 и запускает death review
- **THEN** sandbox последовательно показывает три фазы направленного падения и финальную позу с weapon attachment
