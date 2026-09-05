## Why

Текущие позы робота переключаются мгновенно, поэтому в animation-review заметен механический скачок между idle, locomotion, боевыми реакциями и destruction. Нужно сохранить выразительные ключевые позы, но сделать их переходы и циклы приятнее для просмотра со стороны.

## What Changes

- Ввести renderer-only сглаживание переходов между поддерживаемыми animation states.
- Сделать idle и locomotion непрерывными циклическими движениями с мягким входом и выходом, сохранив текущий набор состояний и нейтральную позу.
- Обновить muted animation-review, чтобы полный прогон давал достаточно времени увидеть плавный переход каждого состояния.
- Не менять deterministic simulation, collision, combat, input, камеры, first-person viewmodel или authored GLB geometry.

## Capabilities

### New Capabilities

- Нет.

### Modified Capabilities

- `participant-weapon-presentation`: non-local robot animation теперь обязана плавно смешивать renderer-only позы без изменения authoritative state.
- `robot-animation-review`: viewer должен позволять визуально оценить плавные переходы полного прогона.

## Impact

- `src/render/robotAnimation.ts` и его unit tests.
- `src/ui/RobotAnimationReview.tsx` и visual QA artifacts.
- `docs/GAME_SPEC.md` и две delta-спеки OpenSpec.
