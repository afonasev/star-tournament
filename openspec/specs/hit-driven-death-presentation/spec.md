# hit-driven-death-presentation Specification

## Purpose

Capability делает смерть участника читаемой спортивной анимацией робота и оставляет на арене корректно лежащее тело, направленное от смертельного выстрела.

## Requirements

### Requirement: Направленная смерть робота
Когда versioned shotgun event переводит текущую жизнь participant в dead state, renderer SHALL показать только renderer-only последовательность: реакцию на удар, падение, сдвиг тела от origin смертельного выстрела и неподвижную позу. Направление падения и сдвига MUST быть горизонтальным направлением от origin выстрела к жертве; при недоступном связанном shot event renderer MUST использовать стабильный fallback, выведенный из сохранённого yaw жертвы. Анимация MUST работать для local-seat, bot и stationary fixture, не создавая collision, damage, input или camera state.

#### Scenario: Смертельный выстрел справа
- **WHEN** shotgun event с origin справа от жертвы завершает её текущую жизнь
- **THEN** робот кратко реагирует на удар, падает и сдвигается от origin в левую для стрелка сторону, затем сохраняет финальную позу

#### Scenario: Renderer получает уже погибшую жизнь
- **WHEN** renderer впервые получает snapshot, в котором текущая жизнь уже dead, а связанного shot event нет
- **THEN** renderer показывает стабильную лежачую позу, выведенную из yaw жертвы, без изменения authoritative state

### Requirement: Профильный renderer-only сдвиг тела
`presentation-balanced-v1` SHALL содержать descriptor `death.corpseSlideDistanceMeters` со shipped значением 0,65 м, диапазоном 0–1,5 м и шагом 0,05 м. Renderer MUST применить это расстояние ровно один раз за `death-fall` вдоль event-derived либо fallback направления и MUST сохранять final rest pose до authoritative corpse expiry. Параметр MUST не входить в `GameDesignProfile`, simulation snapshot, replay, state hash, collision или gameplay knockback.

#### Scenario: Конец фазы падения
- **WHEN** death-fall достигает финальной позы при shipped profile
- **THEN** root GLB-тела находится на 0,65 м дальше от shot origin по горизонтали, чем позиция death contact, и далее не дрейфует

#### Scenario: Одновременные выстрелы
- **WHEN** в tick смерти есть несколько shotgun event
- **THEN** renderer использует origin event, чей `shooterId` совпадает с `killerId` target-destroyed event, а не произвольный выстрел

### Requirement: GLB-тело и contact с полом
Пока authoritative corpse lifetime активен, renderer SHALL показывать GLB-робота той же participant identity с world weapon в руках вместо placeholder geometry. Корень позы MUST быть расположен на presentation floor contact, а не в центре participant capsule; identity emissives MUST быть приглушены. Тело MUST исчезнуть по существующему authoritative corpse expiry и не зависеть от respawn новой life.

#### Scenario: Тело bot после смерти
- **WHEN** погибает bot, чья simulation position является центром капсулы
- **THEN** его GLB-тело лежит на floor contact без зависания над ареной

#### Scenario: Respawn до истечения тела
- **WHEN** participant получает новую life до истечения corpse lifetime предыдущей life
- **THEN** новая active модель и тело предыдущей life отображаются раздельно до corpse expiry

#### Scenario: Передача модели телу
- **WHEN** death-fall достигает финальной позы
- **THEN** dynamic target и GLB-corpse имеют один и тот же final transform, но одновременно видима только одна из двух моделей

### Requirement: Проверяемый death review
Muted animation-review sandbox SHALL предоставлять проверяемый прогон направленной смерти для LOD0 и LOD1: начало реакции, контакт с полом и финальную позу. Sandbox MUST сохранять renderer-only границу и не создавать authoritative match state.

#### Scenario: Проверка LOD1
- **WHEN** reviewer выбирает LOD1 и запускает death review
- **THEN** sandbox последовательно показывает три фазы направленного падения и финальную позу с weapon attachment
