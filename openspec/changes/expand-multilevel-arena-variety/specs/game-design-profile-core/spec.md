## ADDED Requirements

### Requirement: Профиль структурного разнообразия
Profile SHALL содержать descriptors для весов закрытых и открытых композиций, диапазонов высот тоннелей, комнат и залов, полезной площади закрытого этажа и структурного разнообразия seed corpus. Все параметры MUST иметь stable path, группу, подпись, описание, единицу, min/max и шаг; те же metadata MUST управлять UI и валидацией. Release weights MUST обеспечивать преобладание закрытых самостоятельных этажей без исключения открытых вариантов.

#### Scenario: Несовместимые локальные высоты
- **WHEN** выбранные высоты нарушают capsule clearance, перекрытие соседнего этажа или headroom рампы
- **THEN** profile либо generated candidate отклоняется без уменьшения capsule и ослабления physical validation

### Requirement: Профильные budgets multi-level arena
Immutable generator profile SHALL содержать descriptor-backed bounds для hall dimensions, layer height, slab thickness, headroom, tunnel clearance, ramp width/length и bypass length. Cross-field validation MUST отклонять profile, нарушающий controller slope, capsule headroom либо три effective capsule diameters.

#### Scenario: Изменён capsule
- **WHEN** effective capsule diameter меняется и minimum tunnel clearance становится меньше трёх диаметров
- **THEN** profile validation отклоняет configuration до generation

### Requirement: Параметры лестниц
Версионированный профиль SHALL содержать descriptor metadata вероятности stairs, размеров ступеней и сглаживания камеры. Cross-field validation MUST проверять autostep height/width, capsule clearance и headroom.

#### Scenario: Слишком высокая ступень
- **WHEN** ступень превышает допустимый autostep либо недостаточна её проступь
- **THEN** конфигурация отклоняется до запуска матча
