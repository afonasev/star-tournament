## Purpose

Подключить утверждённую боевую петлю к native Player четырёх локальных участников на фиксированной многоэтажной арене, сохраняя границы gameplay, input и presentation.

## ADDED Requirements

### Requirement: Connected shotgun combat
Player SHALL разрешать один двуствольный выстрел по новому нажатию, применять ближайшее попадание каждой дробины с world occlusion и аналитическими head/torso/limb зонами живой актуальной жизни. Сумма долей полного зонального урона SHALL применяться один раз к каждой цели; shooter, dead bodies и movement-only barriers не блокируют дробь. Cooldown и continuous refill SHALL сохраняться.

#### Scenario: Zones and occlusion
- **WHEN** дробины пересекают смешанные зоны, стену либо другого живого участника
- **THEN** каждая поражает только ближайшую доступную зону; стена блокирует урон за ней, полного попадания в голову достаточно для смерти, корпус получает высокий несмертельный урон, конечности пониженный

#### Scenario: Held fire
- **WHEN** игрок держит trigger дольше cooldown
- **THEN** происходит только один выстрел до отпускания и нового нажатия

### Requirement: Death presentation and seat HUD
Каждый seat SHALL показывать здоровье/боезапас своей жизни. Смерть SHALL отключать capsule, движение и огонь, скрывать first-person weapon/crosshair и запускать killcam у точки смерти, следящую за конкретной жизнью убийцы с центральным сообщением. При отсутствии убийцы SHALL применяться медленный orbit вокруг тела. Исчезновение жизни убийцы SHALL не переключать tracking на его новую жизнь. Non-colliding body SHALL иметь ограниченный profile-owned срок; held roster остаётся доступен.

#### Scenario: Killer respawns
- **WHEN** убийца погибает и возрождается во время killcam жертвы
- **THEN** камера жертвы сохраняет последнюю точку отслеживаемой жизни и не прыгает к новому spawn убийцы

### Requirement: Safe current-state respawn
По окончании killcam Player SHALL выбирать spawn по текущему состоянию: исключить статические пересечения, отсутствие опоры, неправильный этаж и живую occupancy; предпочесть отсутствие enemy LOS, затем максимальную минимальную навигационную дистанцию. При отсутствии идеального варианта SHALL немедленно использовать лучший валидный slot. Одновременные respawn SHALL обрабатываться в стабильном participant порядке без пересечения. Возрождение SHALL восстановить capsule, full health/ammo и first-person seat view.

#### Scenario: Occupied preferred slot
- **WHEN** лучший slot занят к моменту readiness
- **THEN** выбирается другой свободный валидный slot в том же tick, без teleport в занятую геометрию

### Requirement: Session and input lifecycle
Pause/focus loss/disconnect SHALL останавливать movement/combat/death/corpse время всех seats, очищать команды и требовать нового отпускания/нажатия trigger после resume. Reconnect SHALL не возобновлять игру автоматически. Runtime roots SHALL принадлежать сцене сессии и удаляться при выходе; повторная загрузка SHALL не оставлять EventSystem, AudioListener, cameras, bodies либо NavMesh прежней сессии.

#### Scenario: Additive reload
- **WHEN** полигон загружен и выгружен дважды при другой active scene
- **THEN** нет оставшихся runtime roots и каждая новая сессия содержит ровно один собственный EventSystem и AudioListener

### Requirement: Evidence and tuning
Новые числовые параметры SHALL принадлежать именованному профилю с metadata registry для editor UI и валидации. Срез SHALL иметь EditMode/PlayMode, Mac build и muted native screenshots боя/killcam/respawn/pause. Автоматизация SHALL маркироваться diagnostic и не закрывать четыре физических устройства/TV, full-match либо целевую performance acceptance.

#### Scenario: Delivery
- **WHEN** срез передаётся
- **THEN** evidence содержит точные profile/build identities, результаты проверок и открытые physical/performance gates
