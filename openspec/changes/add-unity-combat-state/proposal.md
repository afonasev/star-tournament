## Why

Этап 2 требует отделённого от Unity scene состояния боя. Сейчас полигон хранит только движение и diagnostic ray hit; подготовим проверяемую основу damage/death/respawn до подключения weapon queries и HUD.

## What Changes

- Сериализуемое состояние жизни, здоровья, боезапаса, cooldown и ожидания respawn; профиль `unity-combat-state-v1` с общим registry metadata.
- Одна транзакция выстрела по front-edge, continuous-combat refill, применение разрешённого урона к конкретной жизни, однократная смерть, готовность к возрождению и явное возрождение после выбора spawn адаптером.
- EditMode сценарии и roundtrip snapshot; изоляция возвращаемого состояния от владельца.
- Требования GAME_SPEC §3–4, §7. Новых продуктовых решений нет.

## Capabilities

### New Capabilities
- `unity-combat-state`: независимый C# lifecycle боя для будущего local match.

### Modified Capabilities
Нет.

## Impact

Только новый domain runtime и тесты, factory профиля и handoff. Существующий native полигон пока продолжает diagnostic shots. Non-goals: pellet geometry/occlusion, hit-zone queries, collision toggle, killcam/corpse renderer, spawn selection, scoring, teams, bots, results, replay format и сеть. Это завершённая внутренняя основа этапа 2, не готовая игровая петля. Physical/performance gates Stage 0 остаются открытыми; reference hardware и quality не выбираются этим change. Зависимость: существующие profile descriptors и Unity serialization.
