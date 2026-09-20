## Purpose

Определяет сериализуемую основу жизненного цикла боя Unity для людей и будущих ботов, независимо от камеры, устройств, геометрии попаданий и UI.

## ADDED Requirements

### Requirement: Profile-owned combat state
Система SHALL иметь отдельный именованный профиль здоровья, стартового/пополняемого боезапаса, cooldown и ожидания respawn; все числовые настройки имеют metadata и жёсткую валидацию. Состояние SHALL не содержать scene objects и SHALL экспортироваться как независимая копия с participant/life identity.

#### Scenario: Isolated snapshot
- **WHEN** потребитель сериализует и изменяет экспортированное состояние
- **THEN** исходное состояние не меняется; JSON сохраняет cooldown, held-fire и death fields без scene references

#### Scenario: Invalid tuning
- **WHEN** профиль содержит нечисловое, выходящее за диапазон либо дробное значение целого боезапаса
- **THEN** создание runtime отклоняется до изменения состояния

### Requirement: Edge-triggered continuous combat
Система SHALL разрешать один выстрел по новому нажатию при истёкшем cooldown и живом участнике. Выстрел расходует одну единицу; при достижении нуля боезапас SHALL пополняться немедленно без сброса cooldown. Удерживание SHALL не давать автоогонь. Остановленный local runtime не передаёт simulation time; focus/reconnect adapter очищает held input перед resume.

#### Scenario: Held trigger and refill
- **WHEN** последний патрон израсходован, cooldown прошёл, а кнопка всё ещё удерживается
- **THEN** боезапас пополнен, но следующий выстрел не разрешён до release и нового нажатия

### Requirement: Life-scoped damage and death
Разрешённый upstream урон SHALL применяться к конкретной живой жизни; здоровье не ниже нуля, applied damage не выше оставшегося здоровья. Переход в смерть SHALL происходить однократно и хранить killer participant/life identity для будущей killcam. Некорректный урон SHALL отклоняться без mutation; повторное попадание по умершей/старой жизни SHALL быть no-op.

#### Scenario: Overkill and stale hit
- **WHEN** смертельный урон превышает оставшееся здоровье, затем приходит повторное или устаревшее попадание
- **THEN** applied damage ограничен оставшимся здоровьем, death отмечена один раз, новая жизнь не получает старое попадание

### Requirement: Explicit respawn readiness
Система SHALL отсчитывать profile-owned ожидание в simulation time и разрешать явный respawn только после его завершения. Выбор свободной spawn position принадлежит будущему adapter; core SHALL не выбирать позицию и не менять Transform. Respawn SHALL увеличить life identity и восстановить здоровье и стартовый боезапас без переноса held fire.

#### Scenario: Respawn transition
- **WHEN** адаптер пытается возродить погибшего до и после истечения ожидания
- **THEN** ранняя попытка не меняет state; своевременная создаёт новую живую жизнь с полным запасом

### Requirement: Acceptance boundaries
Этот внутренний срез SHALL оставлять diagnostic Player поведение прежним и SHALL не объявлять damage integration, killcam presentation, spawn safety либо четырёхместный полный матч готовыми.

#### Scenario: Validation report
- **WHEN** срез передаётся
- **THEN** приложены domain tests, Player regression и явный следующий adapter этап; physical/performance acceptance остаётся открытой
