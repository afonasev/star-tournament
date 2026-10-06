## Why

Исходный Sci-Fi Soldier ART_LOLL не содержит skin rig и клипов. Пользователь поручил отдельный проверяемый animation pipeline, чтобы оценить кандидата до решения о замене игрока.

## What Changes

- Аудит нетронутого исходника, сохранение SHA-256, provenance и CC BY 4.0.
- Отдельный воспроизводимый Blender pipeline: humanoid skeleton, skinning, клипы idle/walk/run/aim/fire/hit/death или подтверждённый совместимый свободный набор.
- Локальные PNG поз и фаз, независимый export/readback audit и честный список ограничений.

## Capabilities

### New Capabilities

- `trooper-candidate-animation-pipeline`: offline подготовка кандидата с доказательствами и запретом автоматической поставки.

### Modified Capabilities

Нет.

## Impact

GAME_SPEC разделы 3, 7, 8: approved light-sport-robot сохраняется. Меняются только offline scripts, документы и игнорируемые локальные derivatives; runtime, manifest, simulation, input, камеры, сеть и public assets не меняются. Зависимости: локальный Blender и исходный GLB. Открытый продуктовый вопрос — принятие кандидата для shipping — вынесен за рамки этого поручения. Качество скелета и деформаций проверяется по фактическим результатам, а не наличию bone names.
