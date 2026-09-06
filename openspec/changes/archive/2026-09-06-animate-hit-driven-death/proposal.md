## Why

Текущая смерть показывает краткую wireframe-сферу и примитивное наклонённое состояние вместо читаемого погибшего робота. Это не соответствует утверждённой hard-surface анимации и не передаёт, откуда пришёл смертельный выстрел.

## What Changes

- Заменить placeholder `target-destroyed` effect на renderer-only анимацию: краткая потеря опоры, падение и сдвиг тела от origin смертельного выстрела, затем неподвижная поза на полу.
- Показывать тело как GLB-робота той же participant identity, а не как сферу; сохранить world weapon в руках и приглушить identity emissives.
- Вычислять направление падения из существующего shotgun event и позиции жертвы; при отсутствии связанного event использовать стабильный fallback по yaw, не меняющий gameplay state.
- Исправить presentation floor contact для corpse, чтобы позиция капсулы не превращалась в висящее тело.
- Добавить в immutable `presentation-balanced-v1` renderer-only параметр дальности сдвига тела: shipped 0,65 м, диапазон 0–1,5 м, шаг 0,05 м; он использует общий descriptor contract, но не расширяет `GameDesignProfile`.
- Расширить muted animation-review sandbox проверяемой последовательностью падения и финальной позой для обоих LOD.
- Обновить `docs/GAME_SPEC.md` (§3, §8 и журнал) утверждённым решением.

## Capabilities

### New Capabilities
- `hit-driven-death-presentation`: спортивная renderer-only анимация и поза тела, направленные от смертельного выстрела.

### Modified Capabilities

- Нет.

## Impact

Затрагиваются `src/render/robotAnimation.ts`, `src/render/firstPersonRenderer.ts`, `src/ui/RobotAnimationReview.tsx`, `src/presentation/presentationProfile.ts`, их тесты и каноническая спецификация. Симуляция, collision, input, камера killcam, snapshot/replay schema, `GameDesignProfile`, asset manifest и зависимости не меняются. Решение опирается на `docs/GAME_SPEC.md` §3, §7–8.
