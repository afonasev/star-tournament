## Context

См. proposal.md. Оригинал имеет 6 meshes и не имеет skins/animations; прошлый compatible-rig содержит разрезанную жёсткую геометрию, поэтому не используется как source. Blender установлен локально.

## Goals / Non-Goals

**Goals:** воспроизводимый offline derivative, редактируемый skeleton/weights, семь именованных клипов, независимый readback и PNG.

**Non-Goals:** замена light-sport-robot, новые игровые параметры, viewmodel/оружие, Unity import/avatar certification, retopology без оценки необходимости, производство LOD и deploy.

## Decisions

- Оригинал и provenance копируются без изменения в игнорируемую библиотеку этого worktree; Git хранит scripts, recipe, audit и evidence.
- Используется anatomical humanoid hierarchy, feet origin, метры, явная rest pose и mapping; skinning сохраняет непрерывную поверхность вместо геометрического разрезания по суставам.
- Сначала проверяем topology и автоматические веса; ткань допускает сглаженные веса, rigid accessories требуют привязки целиком. Итог оценивается через крайние позы.
- Клипы создаются локально как собственная анимационная адаптация CC BY исходника. Внешние клипы не нужны, если минимальный набор работоспособен; при использовании внешних обязательны license snapshot, bone mapping и readback retarget.
- Offline recipe values не участвуют в симуляции, runtime или Balance Lab. При переносе в игру параметры читаемости и движения проходят отдельную спецификацию и metadata.
- GLB и editable Blender сохраняются локально с исходным attribution и описанием изменений. Стандартные humanoid имена сами по себе не доказывают совместимость с Unity Avatar.

- Второй проход сохраняет v1 и создаёт v2: сглаживание весов по связности ткани, измеренные landmarks пальцев, отдельные digit bones и проверка closed-grip поз крупным планом. QA grip proxy — только измерительная форма для руки, не новый игровой ассет или weapon design.

## Risks / Trade-offs

- Плотная броня → визуальная проверка shoulder/hip/knee, ограничения явно блокируют quality acceptance.
- Неизвестные finger topology и weapon grip → проверяем, не заявляем first-person пригодность без отдельного pass.
- A-pose отличается от Unity T-pose → mapping и калибровка отдельно, runtime не трогаем.
- Offline PNG не доказывают игровой performance → delivery остаётся candidate-only.

## Migration Plan

Запуск локального pipeline; проверка экспортированного GLB повторным импортом. Нет миграции игрового состояния или публикации. Source и approved assets остаются доступны независимо от derivative.
