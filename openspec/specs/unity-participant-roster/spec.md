# unity-participant-roster Specification

## Purpose
Разделить участников native Unity матча и локальные места так, чтобы состав до восьми тел использовал только реальные локальные виды и назначения ввода.

## Requirements

### Requirement: Независимые participant и local-seat identities
Native runtime SHALL принимать immutable состав из 2–8 участников и уникальное отображение 1–4 local seats на существующих участников. Participant identity MUST оставаться стабильной в течение матча, независимо от номера камеры или устройства. Невалидные ссылки, дубликаты и количества MUST отклоняться до создания частично активного матча.

#### Scenario: Непоследовательное отображение
- **WHEN** два локальных места отображены на участников 6 и 2 состава из восьми
- **THEN** их действия, HUD, оружие и камеры принадлежат именно 6 и 2, а остальные участники не получают local input

#### Scenario: Один локальный вид
- **WHEN** у валидного матча есть один local seat
- **THEN** он занимает весь экран; общий состав по-прежнему содержит минимум двух участников

#### Scenario: Invalid mapping
- **WHEN** mapping содержит повтор участника или неизвестный participant id
- **THEN** запуск отклонён без утечки session/camera/input state

### Requirement: Participant lifecycle не зависит от camera lifecycle
Все участники SHALL иметь одинаковые физические motor/capsule, здоровье, оружие, scoring, death/corpse и respawn правила. Камеры, first-person руки и HUD SHALL существовать только для local seats. Смерть нелокального участника MUST NOT менять камеру другого участника.

#### Scenario: Нелокальный убийца
- **WHEN** local participant погибает от нелокального участника с индексом выше числа экранов
- **THEN** killcam отслеживает именно жизнь убийцы; его смерть/respawn не переключает камеру на новую жизнь

#### Scenario: Восемь тел
- **WHEN** матч содержит восемь участников и один local seat
- **THEN** все восемь занимают distinct physically valid initial slots, участвуют в общих collision/combat/standings, а на экране только одна камера и один HUD

### Requirement: Полный состав виден в standings
Live/results SHALL показывать всех участников до восьми, сохраняя выравнивание колонок, authoritative цвета и team totals. Local humans SHALL отличаться от явно помеченных diagnostic fixtures; stationary fixture MUST NOT называться готовым ботом.

#### Scenario: Командный состав из восьми
- **WHEN** открыта командная таблица восьми участников
- **THEN** видны обе команды, оба totals и все восемь строк без обрезки; local highlights следуют mapping

### Requirement: Repeat и pause сохраняют ownership
Pause/focus loss/disconnect назначенного устройства SHALL останавливать общий gameplay и очищать held actions. Repeat SHALL создавать чистую сессию с exact frozen roster, mapping, profiles и назначениями существующих устройств. Изменение local seats SHALL оставаться доступным только в setup; выход в setup MUST освобождать прежнюю сессию.

#### Scenario: Repeat после смерти
- **WHEN** пользователь повторяет mixed diagnostic матч после смерти и редактирования setup drafts в тесте
- **THEN** новый матч использует frozen composition/mapping и чистые lives/score/corpses, без добавления виртуальных устройств для нелокальных участников

#### Scenario: Disconnect
- **WHEN** отключён контроллер local seat
- **THEN** матч всех участников приостановлен до восстановления того же устройства либо явного rebind в setup; нелокальные участники не требуют контроллеров

### Requirement: Честная граница поставки prerequisite
Development journey SHALL проверять solo viewport и состав до восьми через явно обозначенные fixtures и ordinary participant actions. Обычный human-only setup SHALL сохранять работоспособность; UI MUST NOT предлагать stationary fixtures как playable AI. Shipping bot planner/setup остаётся отдельным обязательным срезом.

#### Scenario: Обычный запуск
- **WHEN** Player запущен без diagnostic flags
- **THEN** human setup и его 2–4 seat flows работают без скрытых участников и без navigation fixture goals
