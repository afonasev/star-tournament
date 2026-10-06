## Context

См. мотивацию в `proposal.md`. Текущий controller рассчитывает целевую позу напрямую в каждом render tick и немедленно записывает её в joints. Это сохраняет simulation boundary, но делает дискретный выбор state визуально резким, особенно в review sandbox.

## Goals / Non-Goals

**Goals:**

- Сохранить один presentation-owned controller и существующие semantic joint groups.
- Получать целевую позу из того же state/time, но интерполировать её от фактически отображённой позы с time-based easing.
- Сохранить устойчивую плавность при переменной частоте render кадров и в throttled in-app Browser.

**Non-Goals:**

- Не добавлять skeletal clips, физику, новые simulation events или балансные параметры.
- Не менять native first-person viewmodel/recoil, camera, input или LOD assets.

## Decisions

### Target pose плюс экспоненциально-независимый от FPS blend

Controller будет строить целевые углы semantic joints как сейчас, а затем приближать отображаемые углы к ним через нормированное time-based сглаживание. Это устраняет зависимость от частоты RAF и позволяет одной короткой функции применять позу и во время обычного рендера, и при ручном выборе state в viewer.

Линейный фиксированный `lerp` на кадр отклонён: он заметно меняет скорость перехода на 60 Hz, high-refresh и throttled вкладках. Смешивание целых transforms отклонено, потому что контракт требует вращать только semantic joints.

### Непрерывные циклы и переходы

Idle и locomotion сохранят periodic phase, но используют сглаженную целевую позу; новый state не сбрасывает phase. Discrete jump, hit и destruction также входят через тот же blend, поэтому первые кадры не телепортируют суставы.

### Review timing

Полный review увеличит dwell между состояниями настолько, чтобы blend был наблюдаемым; ручной выбор сразу задаёт target, но не обходит interpolation. Sandbox остаётся renderer-only и muted.

## Risks / Trade-offs

- [Слишком долгий blend размоет fire/hit feedback] → применить короткие state-class durations и проверить key frames в viewer.
- [Throttled RAF сделает state кажется замершим] → выбор state запускает immediate presentation update, а следующий реальный render продолжает time-based blend.
- [Накопление rotation drift] → target pose каждый tick создаётся из neutral values, а controller хранит только отображённую presentation pose.

## Migration Plan

1. Обновить canonical GAME_SPEC и delta specs.
2. Добавить pose blending и regression tests.
3. Проверить viewer, сохранить PNG/GIF, затем выполнить обычные checks и strict validation.
4. При регрессии rollback ограничен renderer-only controller и не требует миграции state или assets.
