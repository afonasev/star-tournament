## Why

Принятый робот имеет детализированную, но статичную форму, из-за чего движение и боевые события не читаются как действия спортивного участника. Полная анимационная основа нужна сейчас, пока authoring pipeline может сохранить уже утверждённые силуэт, экипировку и first-person представление.

## What Changes

- Добавляется `light-sport-robot-animation-v1`: жёсткая semantic joint hierarchy без skin deformation для обоих LOD робота и weapon/hand mount.
- Добавляются renderer-only состояния idle, walk/run/strafe, jump, aiming, firing recoil, hit reaction и destruction, derived из presentation snapshot/event данных.
- Добавляется отдельный muted animation-review sandbox для управляемого просмотра состояний робота со стороны; sandbox не является матчем, источником simulation state или проверкой physical input.
- Existing first-person руки, camera и их accepted recoil сохраняются; collision, hit volumes, combat, replay и state hash не меняются.

## Capabilities

### New Capabilities
- `robot-animation-review`: Управляемый browser sandbox для просмотра полного набора presentation-анимаций робота со стороны.

### Modified Capabilities
- `participant-weapon-presentation`: Жёсткая суставная иерархия, анимация non-local робота и привязка оружия к рукам без изменения simulation contract.

## Impact

Изменяются generator локальных participant GLB, renderer presentation controller, его typed snapshot/event projection и renderer tests. Добавляются source/animation audits, browser sandbox и visual evidence; затрагиваются решения `docs/GAME_SPEC.md` разделов 2 и 3. Новых зависимостей, сетевого протокола, input-механики, collision или persisted simulation data нет.
