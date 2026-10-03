# Проверка ориентирования в тоннелях

Scope: renderer-only UI/presentation. IndustrialTunnelsCatalog, topology, collision/nav и точки предметов не изменены. Baseline 7910ab19aef94ffcabe0df6f70a80e5b64f5b7b1.

Выбранный gate: make check-ui, IndustrialTunnelsTests + DesignLabHistoryTests + ProvingProfileTests, IndustrialTunnelsPhysicsTests, native macOS build и muted Player visual smoke. Результаты будут добавлены после выполнения.

Кадры room-01…04 сняты с одинакового относительного положения внутри входа; approach-01…04 — из внешнего тоннеля; exits-01…04 — из зала к его выходам. room-05-centre показывает центр. four-rooms — четыре viewport. Камеры scripted, симуляция удерживается: это visual QA, не human/device/60 FPS acceptance.

- Profile/history focused gate: PASS 49/49, 0 skipped, Unity exit 0; `automated/profile-history/`. Включает сохранение custom light и immutable history hash после presentation v1 → v2.
- UI gate: PASS 3 EditMode + 11 PlayMode, без skipped. Physics: PASS 1/1, 96 motor traversals (16 ramps × 2 directions × 3 lanes), anchor connectivity, no decorative colliders.
- Первый native visual smoke завершился exit 0 без script errors: номера/направления читаются во всех залах. После просмотра круг пола заменён плоской геометрией краски; окончательные кадры и build следуют ниже.

## Окончательный кандидат

- Source `6c26c27b86e586accc5499cfc91d65fe9c53a7b6`, build `20261003T060512Z`; точный путь и checksum — `candidate.json`, полный manifest — `candidate-files.sha256`.
- Native build exit 0 (`build.log.gz`), exact retained Player exit 0, `player/complete.json`: PASS, muted. Проверены все 5 залов, 4 внешних подхода, выходы из 4 углов, южная ракетница и 4 viewport. No exceptions/script errors; ранее известное предупреждение URP о shadow atlas осталось.
- 64 автоматические проверки: 49 profile/history + 3 UI EditMode + 11 UI PlayMode + 1 tunnel physics, 0 skipped/failed. Они выполнены на implementation tree до финальной замены объёмного круга пола на плоскую декоративную разметку; последняя renderer-only правка проверена повторной сборкой и exact Player smoke. Gameplay/catalog/input не изменены; повтор полного bot suite не требуется выбранным UI scope.
- Скриншоты финального кандидата: seed 20261002 (2 bots), 20261003 (4 bots/views), scripted cameras, paused simulation. FPS в кадрах включает захват и короткое окно после старта; это не performance benchmark. Human play/visual, physical gamepads, TV/4K и устойчивые 60 FPS не подтверждены.
- Проверены читаемость 01–04 с подходов, соответствие назначений выходов, сохранение тёплого освещения и различимость предметов. Центральное кольцо пола плоское, подвесное кольцо не закрывает проходы.
- Во время build Unity регенерирует сцену и технические assets/settings. В commit оставлен только блок TunnelsPresentation v2; посторонние generated rewrites восстановлены в owned worktree. Hash фактической build-сцены сохранён отдельно.
