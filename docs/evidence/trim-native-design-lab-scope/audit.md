# Balance Lab: завершённая очистка аудита · 2026-09-29

По решению пользователя «давай по аудиту лишнее все уберем» из UI, поиска, дерева и diff дополнительно исключены все 25 оставшихся полей (пункты 1–9 исходного аудита).

| Пункт | Исключённые stable paths |
| --- | --- |
| 1. Fixture navigation | `navigation.sampleDistance` |
| 2–3. World bounds / authoring validation | `world.maximumTransitionSegment`, `world.minimumSupportHeight` |
| 4. Engine tick rate | `simulation.fixedTickHz` |
| 5. Shadow engineering | `light.shadowResolution`, `light.shadowBias`, `light.shadowNormalBias`, `light.shadowCasters` |
| 6. Technical bot navigation | `bots.navigation.heightTolerance`, `bots.navigation.sampleDistance` |
| 7. Old weapon mounts | `presentation.viewWeaponX`, `presentation.viewWeaponY`, `presentation.viewWeaponZ`, `presentation.worldWeaponMountY` |
| 8. Unused stair camera fields | `camera.stairSmoothingSeconds`, `camera.stairMaximumOffsetMeters` |
| 9. Fixed model placement | `view.x`, `view.y`, `view.z`, `cutter.modelX`, `cutter.modelY`, `cutter.modelZ`, `cutter.modelPitch`, `cutter.modelYaw`, `cutter.modelRoll` |

Геометрия уровней, спавны, маршруты, точки бонусов, координаты/повороты оформления и размеры деталей станции уже исключены предыдущим этапом и остаются скрытыми.

## Сохранённые контракты
Runtime значения, descriptor metadata, defaults и schema/profile revisions не меняются. Прежние snapshots и hashes остаются неизменными, включая ранее сохранённые custom overrides скрытых полей. Новое сохранение может переносить старые значения, но не может редактировать скрытые поля. Пустые группы автоматически исчезают через общий filtered registry.

Урон/боезапас/cooldown, здоровье/броня/бонусы, движение и hit zones, чувствительность/камера/killcam, правила/очки, игровые реакции/прицел/поведение AI остаются. Сохраняются читаемость света и палитра, прозрачность стекла, эффекты оружия, скорость декоративного движения, dynamic shoulder/elbow weapon switch animation. User concept approval не означает human acceptance Player.

Точный source/candidate, native screenshots и checks: `trim-native-design-lab-scope/delivery.json` в shared planning home; обновлённые evidence — `docs/evidence/trim-native-design-lab-scope/audit-cleanup/`.
