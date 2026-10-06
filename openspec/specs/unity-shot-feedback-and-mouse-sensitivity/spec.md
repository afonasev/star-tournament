# unity-shot-feedback-and-mouse-sensitivity Specification

## Purpose
Capability задаёт наблюдаемую native Unity presentation существующего hitscan-дробовика и безопасную сохраняемую настройку чувствительности мыши для keyboard/mouse local seat.

## Requirements

### Requirement: Derived дробь и impact остаются presentation-only
После допустимого native `shotgun-shot` event renderer SHALL показать отдельные дробины как короткоживущие читаемые световые пути от видимых muzzle mounts до resolved stopping point и один читаемый material-aware impact только в resolved world или participant contact point. Дробь, impact и cleanup MUST быть derived из immutable результата существующего shot event и MUST NOT создавать collision, decals, damage, ammo, cooldown, replay state или gameplay RNG.

#### Scenario: Видимый полёт и попадание
- **WHEN** допустимый выстрел имеет видимый путь и хотя бы одно попадание в world либо participant hit volume
- **THEN** native Player показывает несколько различимых дробин в движении от корректного muzzle mount и краткий impact в фактической конечной точке, а authoritative combat result не меняется

#### Scenario: Промах на дальности
- **WHEN** допустимый выстрел не пересекает world или participant hit volume до profile-defined range
- **THEN** renderer завершает дробины на существующей дальности без contact impact и не создаёт ложный hit feedback

#### Scenario: Очистка эффекта
- **WHEN** истекает profile-defined presentation lifetime либо матч dispose/repeat/menu
- **THEN** все принадлежащие shot feedback runtime objects освобождаются без сохранённых следов и следующий матч не наследует эффект

### Requirement: Пользовательская mouse sensitivity
Обычный native Settings UI SHALL позволять keyboard/mouse игроку изменить и сохранить user override чувствительности мыши в той же единице, что и profile-defined base sensitivity. Настройка MUST использовать metadata именованного Balance Lab profile: stable path, группу, подпись, описание, единицу, minimum, maximum и step; UI MUST показывать текущее значение в этой единице и валидировать границы до сохранения. Profile default применяется при отсутствии либо невалидности preference; gamepad look и другие local seats MUST NOT получать override.

#### Scenario: Default и сохранение
- **WHEN** preference отсутствует при запуске native Player
- **THEN** Settings UI показывает profile-defined default, а явное изменение сохраняется и восстанавливается после пересоздания UI

#### Scenario: Крайние значения
- **WHEN** игрок поочерёдно выбирает minimum и maximum через обычный Settings UI
- **THEN** generated mouse look delta остаётся в profile-defined допустимом диапазоне и UI не может сохранить значение за его пределами

#### Scenario: Focus и устройства
- **WHEN** игрок открывает Settings вне captured gameplay, изменяет sensitivity и затем начинает либо продолжает keyboard/mouse матч
- **THEN** настройка не захватывает и не снимает cursor сама, а существующая потеря focus/lock по-прежнему очищает held gameplay input; gamepad look остаётся без изменения
