## ADDED Requirements

### Requirement: Полные профили AI и непрерывного боезапаса
GameDesignProfile SHALL включать полные `rules.bots.easy`, `rules.bots.normal`, `rules.bots.hard`, общие navigation/cooperation параметры и `rules.weapons.doubleBarrelShotgun.emptyRefillAmmo`. Последнее SHALL иметь shipped значение 20 для `continuous-combat-v1`. Каждое числовое поле MUST иметь единый descriptor path/group/label/description/unit/min/max/step и участвовать в profile hash; refill MUST быть положительным целым. Никакие AI overrides MUST NOT менять damage, health, ammo или физическую скорость отдельного уровня.

#### Scenario: Полный профиль
- **WHEN** загружается новая shipped revision
- **THEN** все AI/refill numeric leaves покрыты metadata, а validation проверяет диапазоны и cross-field отношения

#### Scenario: Повреждённое пополнение
- **WHEN** refill равен нулю, отрицательный либо дробный
- **THEN** профиль отклоняется до матча со стабильным path

#### Scenario: Изменение поведения
- **WHEN** меняется AI numeric parameter
- **THEN** изменяется profile identity/hash и старый несовместимый replay отклоняется явно
