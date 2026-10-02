# Исправление цветов оружия — 2026-10-02

Исправлен поиск владельца оборудования при окраске выключенного персонажа: `GetComponentInParent<WeaponModelPresentation>(true)`. Мировые модели оружия сохраняют authored material references вместо замены на нейтрализующий шейдер тела. Модели, палитры, баланс, simulation и звук не менялись.

Исправление: `2f19d2b107f07313d23f630b714bb4800517cf20`. Итоговый Player после интеграции: `550ec9cbbc3f2d4f8d452386d435144cb007d5cf`. Диагностика до исправления: [weapon-color-diagnostic](../weapon-color-diagnostic/README.md).

## Проверки

- До интеграции, source `2f19d2b`: `make check`: exit 0; EditMode 289/289, PlayMode 184/184, skipped/failed 0; успешная native macOS сборка.
- После интеграции, source `550ec9c`: EditMode 43/43, PlayMode 9/9, skipped/failed 0; успешная повторная сборка. Проверены history profile upgrades, world materials, participant identity и Rocket presentation.
- Новый regression падал до исправления на замене authored материала у inactive actor и прошёл после исправления. Проверяет также boost и восстановление исходных материалов.
- Существующие identity tests дополнены проверкой equipment shaders на start, boost, respawn и Repeat. Natural eight-player FFA и teams достигли terminal state.
- Итоговый сохранённый Player после интеграции (`550ec9c`): exit 0; 24 снимка, session snapshots и material/property-block dumps. Все 24 сочетания оружия/состояния/world-own прошли проверку shader routing и красных override.
- Initial shared Player не начал capture и был остановлен только его владельцем; exclusive повтор завершился. Чужие процессы не останавливались.

Это muted диагностическая сцена с фиксированным размещением, восемью участниками, local participant 7, наблюдаемым participant 0 и часами 05:00. Damage timer задаётся snapshot как 5 секунд и затем сбрасывается в 0. Это подтверждает rendering и transitions; не заменяет human/device/performance acceptance или естественный pickup/expiry.

## Сборка для визуальной приёмки

[local-macos-player-candidate/550ec9cbbc3f2d4f8d452386d435144cb007d5cf/fix-world-weapon-colors](/Users/eaafonasev/Projects/star-tournament/.local/releases/fix-world-weapon-colors/550ec9cbbc3f2d4f8d452386d435144cb007d5cf/StarTournamentProvingGround.app)

Сборка сохранена вне worktree; точный SHA-256 исполняемого файла — в [candidate.json](candidate.json). Автоматические проверки и снимки не означают human acceptance.

## Мировое оружие

| Оружие | Обычно | Бонус | После снятия |
| --- | --- | --- | --- |
| Автомат | ![Автомат, normal](post-integration-player/Rifle-normal-world.png) | ![Автомат, boost](post-integration-player/Rifle-boost-world.png) | ![Автомат, restored](post-integration-player/Rifle-restored-world.png) |
| Дробовик | ![Дробовик, normal](post-integration-player/Shotgun-normal-world.png) | ![Дробовик, boost](post-integration-player/Shotgun-boost-world.png) | ![Дробовик, restored](post-integration-player/Shotgun-restored-world.png) |
| Ракетница | ![Ракетница, normal](post-integration-player/RocketLauncher-normal-world.png) | ![Ракетница, boost](post-integration-player/RocketLauncher-boost-world.png) | ![Ракетница, restored](post-integration-player/RocketLauncher-restored-world.png) |
| Резак | ![Резак, normal](post-integration-player/Cutter-normal-world.png) | ![Резак, boost](post-integration-player/Cutter-boost-world.png) | ![Резак, restored](post-integration-player/Cutter-restored-world.png) |

## Вид от первого лица

| Оружие | Обычно | Бонус | После снятия |
| --- | --- | --- | --- |
| Автомат | ![Автомат, normal](post-integration-player/Rifle-normal-own.png) | ![Автомат, boost](post-integration-player/Rifle-boost-own.png) | ![Автомат, restored](post-integration-player/Rifle-restored-own.png) |
| Дробовик | ![Дробовик, normal](post-integration-player/Shotgun-normal-own.png) | ![Дробовик, boost](post-integration-player/Shotgun-boost-own.png) | ![Дробовик, restored](post-integration-player/Shotgun-restored-own.png) |
| Ракетница | ![Ракетница, normal](post-integration-player/RocketLauncher-normal-own.png) | ![Ракетница, boost](post-integration-player/RocketLauncher-boost-own.png) | ![Ракетница, restored](post-integration-player/RocketLauncher-restored-own.png) |
| Резак | ![Резак, normal](post-integration-player/Cutter-normal-own.png) | ![Резак, boost](post-integration-player/Cutter-boost-own.png) | ![Резак, restored](post-integration-player/Cutter-restored-own.png) |
