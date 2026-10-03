# Проверка ориентирования в тоннелях

Scope: renderer-only UI/presentation. IndustrialTunnelsCatalog, topology, collision/nav и точки предметов не изменены. Baseline 7910ab19aef94ffcabe0df6f70a80e5b64f5b7b1.

Выбранный gate: make check-ui, IndustrialTunnelsTests + DesignLabHistoryTests + ProvingProfileTests, IndustrialTunnelsPhysicsTests, native macOS build и muted Player visual smoke. Результаты будут добавлены после выполнения.

Кадры room-01…04 сняты с одинакового относительного положения внутри входа; approach-01…04 — из внешнего тоннеля; exits-01…04 — из зала к его выходам. room-05-centre показывает центр. four-rooms — четыре viewport. Камеры scripted, симуляция удерживается: это visual QA, не human/device/60 FPS acceptance.

- Profile/history focused gate: PASS 49/49, 0 skipped, Unity exit 0; `automated/profile-history/`. Включает сохранение custom light и immutable history hash после presentation v1 → v2.
- UI gate: PASS 3 EditMode + 11 PlayMode, без skipped. Physics: PASS 1/1, 96 motor traversals (16 ramps × 2 directions × 3 lanes), anchor connectivity, no decorative colliders.
- Первый native visual smoke завершился exit 0 без script errors: номера/направления читаются во всех залах. После просмотра круг пола заменён плоской геометрией краски; окончательные кадры и build следуют ниже.
