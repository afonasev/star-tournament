# Интеграция alignment в main

**Source QA завершена:**305 различных EditMode и86 PlayMode cases покрыты исходным прогоном и направленными повторами; текущий результат каждого — PASS. Python147, JS11, tooling11,18 режимов launcher contracts и компиляция maintenance прошли. Первичные FAIL/ERROR сохранены отдельно. Это не full game suite и не native/device/human acceptance.

Baseline локального `main`: `f8a54b7e`; исходная ветка: `codex/game-standard-alignment@d9cd23ed`; обновлённый release snapshot: `github/main@5f1b2909`. Ветки разработки и release snapshots имеют отдельную ancestry. `git cherry main alignment`: все 67 уникальных патчей помечены `+`, эквивалентных патчей нет. 14 более новых коммитов main относятся к Well и рабочим правилам. Ранние настройки/history/update, уже входящие в общую историю, повторно не применяются.

| Область | Исходные коммиты | Переносимое поведение |
|---|---|---|
| Startup/installer recovery | 2b582de6..01ca6df0 | StartupRecovery, PayloadRecovery, StartupHealthMonitor, NativeStartupHealth, WindowsInstallerTransaction |
| Windows rollback/broker | 2fe7a308..d2358a50 | WindowsRecoveryStore, WindowsFullPayload, WindowsBrokerUpgradeTransaction, BrokerUpgradeCandidate |
| macOS update/DMG/discovery | 7b5fd153, f68bdb63, 4aa5ff40 | MacRecoverySupervisor, GitHubDiscovery, macos_dmg.py; более поздние исправления discovery/PKG 7a6b2527 и 5f1b2909 |
| Lab history/migrations/typed controls | e5c82b1b..fa13ca57 | LabAuthoring, LabCompatibilityViews, VersionedUserData, ProvingGroundDesignLab; новые Well migration hooks сохранены |
| Выбор качества графики | 20b2f124 | NativeVisualPreferences и ProvingGroundGraphicsSettings |
| Профиль баланса в паузе | 21fe689d | FrozenLabIdentity отображается в каждом viewport паузы |
| Подсказки устройств и Back | c6eebc62..670e7f38 | MenuGamepadHints, MenuActionHint, ProvingGroundBackRouting |

Объединённые Runtime/Editor/Resources, тесты и distribution первоначально точно совпали с актуальным release source. Новые assets и правила main сохранены. Дополнительные исправления fixtures относительно release: обязательный PKG в test_release_changelog.py; schema-aware чтение compact истории в FirstPersonWalkTests; явно несовместимая будущая сетка в LabAuthoringTests. Дополнительная runtime-правка — начальные bounds скрытой подсказки (разбор ниже).

## Разрешение конфликтов
GAME_SPEC сохраняет утверждённые решения Well и alignment. WellArenaTests сохраняет revision4/corridor и более новые тесты Well. Пользовательские .8/.42/.5 сохраняются точно, без замены текущим factory default. Новый шаг well.fill=.01 допускает .8; диагностический fixture отказа явно использует несовместимый шаг .05. Исторические JSON/hash/байтовые assertions сохранены. Собственные значения и factory Default проверяются отдельно. Это применение уже согласованного правила Lab, новая продуктовая развилка отсутствует.

## Границы результата
Только исходники. Full suite, Player, сборка установщиков, установка, публикация/deploy, physical Windows/UAC/ACL/Finder и человеческая приёмка не выполнены. Исторические evidence сохранены как исторические. Текущие команды и результаты находятся в qa-scope.md, reconciliation.json и логах этого каталога.

## Текущие регрессии fixtures
Первичный EditMode:301/305,4 FAIL. Два FirstPersonWalk читали compact history напрямую как старый LabHistoryFile вместо существующего schema-aware ReadFixture. LabAuthoring ожидал отказ для .8, хотя текущий Well grid уже допускает это значение; fixture должен явно задавать несовместимую будущую сетку, как диагностика отказа. ExactLargeLegacyHistory требует внешний явный fixture; найден исходный160126874-byte файл с SHA2567241dbd2..., создана точная owned копия. Первичные XML/лог сохраняются; повтор выбирает только эти4 случая, assertions/hash/byte/timeouts не ослабляются.

## Прогон PlayMode и минимальный ремонт
Первичный PlayMode:81/86,5 FAIL. Один SetUp сообщил No space left on device; после выхода Editor на том же томе свободно14GiB, собственные временные данные ограничены197MiB. Чужие ресурсы не удалялись. Повтор ограничен упавшим setup.

Реальный UI дефект: скрытый gamepad hint имел стандартный offsetMin.y=-50, попадал в bounds строки и ломал reveal последнего элемента diff. Начальный offsetMin теперь задаётся явно, геометрия подсказки остаётся внутри кнопки даже когда Text выключен. Исходный тест сохраняет точные пороги видимости.

Другие fixtures: набор карт точно включает Well; проверка несовместимости задаёт будущую грубую сетку через существующий loader seam, поскольку текущая сетка уже допускает .8. Profile-switch fixture явно сохраняет полный снимок (фиксирует все поля), затем удаляет только disposable views перед reload: вновь построенные cache keys оказываются выше сохранённой версии. Все прежние assertions выбора, значений, hash и байтов сохраняются. Это применение согласованных source/view и Save контрактов, не изменение игрового поведения.
