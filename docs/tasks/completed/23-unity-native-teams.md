# 23 — Unity: нативный командный матч

Статус: реализован и проверен в отдельном коммите change `add-unity-native-teams`. Ветка `codex/unity-native-teams`, worktree 83e9; база `4d0334f` поверх native trooper `9b21406`.

## Решение и реализация

Пользователь подтвердил: союзник блокирует дробь без урона. GAME_SPEC §3 и журнал обновлены до реализации. Дробина не проходит к противнику за союзником; damage, kill и assist не начисляются.

Native setup переключает FFA/Team A/B для 2–4 людей, назначает команды и ориентацию существующих blue/magenta цветов. Требуются две непустые команды; неравные составы разрешены. Ошибка состава или профиля оставляет setup доступным для исправления. Session замораживает roster, цвета и профили; Repeat сохраняет их и устройства, очищая состояние матча.

Initial spawn резервирует свободные места с capsule clearance и расстоянием между противниками. Новый профиль `unity-native-team-v1@1` хранит `spawn.initialOpponentSeparation`: стартовое значение 5 м, диапазон 1–8 м, шаг 0,5 м, группа, подпись и описание управляют Inspector и валидацией. Respawn оценивает угрозу только от противников; физическая занятость учитывает всех. Числа остаются в конфигурации, а не narrative GAME_SPEC.

HUD, сгруппированные live/results, постоянная таблица при трёх местах и победитель используют team identity. Цвета body, first-person arms и corpse сохраняются; возврат FFA очищает командные строки и восстанавливает личную палитру. Shipping trooper assets не изменялись.

## Проверка

- EditMode **41/41**, PlayMode **32/32**, Mac Development build PASS (459154141 reported bytes). Четыре новых PlayMode tests проверяют все 22 непустых состава для 2–4 мест, initial spawn, фактический shotgun blocker, direct damage, respawn/occupancy, одновременный respawn союзников, invalid setup/profile recovery, цвета, results/Repeat и FFA.
- Native muted journeys: **31 PNG/JSON в FHD и 31 в 4K**, все focused. Synthetic gamepads проходят реальные UI/session paths; resolved diagnostic damage и ускоренные ticks используются только для достижения результатов.
- Обычный Player отдельно проверен через CUA: join, режим, пустая команда, восстановление состава, цвета, diagnostic start, pause, Repeat, setup и Exit. Это не физическая проверка четырёх контроллеров.
- Astra read-only review выполнен до и после реализации. Исправлены recovery при ArgumentException профиля, недоказательный blocker diagnostic и отсутствие regression командного respawn. Strict OpenSpec и проверка diff проходят.

Короткие stationary foreground samples: повтор FHD p95 9,208 мс, 4K p95 9,164 мс. Первый FHD содержит worst 1008,856 мс; исходный отчёт сохранён, причина не установлена. Повтор не доказывает устранение выброса. Это не длительная приёмка 60 FPS и не строгий A/B предыдущего коммита.

[Evidence: screenshots, XML, build hash, performance и ограничения](../../evidence/unity-native-teams-2026-09-20/README.md). Полный архив и логи сохранены с проверкой SHA256 в `/Users/eaafonasev/Documents/Codex/qa-vault/star-tournament/native-teams-2026-09-20`.

## Следующий срез и открытая приёмка

Bots, playable solo и восемь scene participants остаются отдельными срезами этапа 2. Этот handoff завершает human teams integration; этап миграции целиком не завершён. Реальные gamepads/TV, художественная приёмка trooper, reference hardware/quality/internal scale и длительный foreground 60 FPS остаются открытыми. Main, archive и deploy не изменялись.
