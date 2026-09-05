## Purpose

Задаёт ускоренные воспроизводимые проверки ботов на реальной игровой симуляции, с измеримой активностью, сравнением сложности и отдельной браузерной приёмкой.

## ADDED Requirements

### Requirement: Реальные ускоренные матчи
Headless runner MUST использовать те же AI, fixed timestep, collision, оружие, ammo, scoring и respawn, что browser, без DOM/WebGL и ожиданий wall-clock. Он SHALL принимать полные roster/profile/arena identities, seeds и предел ticks, включая bot-only FFA и teams. Отчёт SHALL сохранять фактические результаты, elapsed ticks, wall time и измеренное ускорение.

#### Scenario: Ускоренный запуск
- **WHEN** запускается серия заданных seeds
- **THEN** матч выполняется с исходным fixed delta, а ускорение получается за счёт отсутствия render/wait, не увеличением delta

#### Scenario: Ограничение прогона
- **WHEN** достигнут harness tick budget до победы
- **THEN** отчёт содержит timeout и реальное состояние матча без подмены overtime или выбора фиктивного победителя

### Requirement: Метрики и воспроизводимые ошибки
Отчёт SHALL включать K/D/A, score, dealt/received damage, shots/hits, время до контакта, movement/idle/stuck intervals, grounded/airborne/jump cadence, долю поддержки и close-group duration. Ошибки SHALL сохранять seed, arena/profile/AI identities, scenario, initial state, внешние actions, checkpoint hashes и snapshot для воспроизведения; ожидание/скрытие браузера MUST NOT влиять на результат.

#### Scenario: Регрессия маршрута
- **WHEN** на seed обнаружен длительный stuck interval
- **THEN** сохранённый артефакт позволяет воспроизвести тот же интервал и state hashes

### Requirement: Оценка силы и поведения
Версионный evaluation suite SHALL включать paired seeds со сменой spawn/сторон, FFA/teams, все размеры арен и уровни, составы до восьми участников. Сила SHALL статистически возрастать от Салаги к Бойцу и Ветерану на независимом наборе seeds. Отчёт MUST показывать размер выборки, неопределённость оценки, timeout/stall долю и исходы, а не объявлять успех по одному матчу.

#### Scenario: Сравнение соседних уровней
- **WHEN** оба направления paired matchup завершены на evaluation seeds
- **THEN** отчёт показывает результаты обеих сторон, агрегат и доверительный интервал; недостаточные данные не считаются PASS

### Requirement: Финальная браузерная приёмка
AI change MUST пройти muted in-app Browser матч с реальным keyboard/mouse управлением, FFA/teams, смешанной сложностью, lobby edit, live/results, смертью/respawn и pause/resume. Скриншоты SHALL показывать актуальные изменённые состояния из dev worktree, а simulation/collision/performance gates MUST проверяться отдельно.

#### Scenario: Приёмка игрового поведения
- **WHEN** headless проверки прошли
- **THEN** приёмка остаётся открытой до browser playtest и подтверждения управляемости; renderer-only preview или автоматические команды не заменяют физическое управление
