# 16 — Sci-Fi Soldier: отдельный animation pipeline

Статус: **v2 технически проверен; художественная приёмка и проверка с игровым оружием остаются открытыми**.
Ветка: `codex/trooper-humanoid-animation-pipeline`.
Worktree: `/Users/eaafonasev/.codex/worktrees/5b65/star-tournament`.
Change: `openspec/changes/prepare-trooper-animation-pipeline/`.

Поручение: подготовить отдельного ART_LOLL Sci-Fi Soldier, сохранив источники/CC BY provenance; не заменять approved light-sport-robot, runtime и manifest.

## Передано

- [Воспроизводимый pipeline и инструкция](../../scripts/trooper/README.md).
- Editable `.blend`, skin GLB, **57 bones** и семь собственных baked clips: idle/walk/run/aim/fire/hit/death.
- v2 добавляет 30 костей пальцев и 4 forearm twist bones, сглаживает skinning таза/коленей/перчаток, подбирает локальный хват и анимирует правый указательный палец.
- Исходный `trooper-2k.glb` и v1 сохранены. Разрезанный `trooper-compatible-rig.glb` не используется.
- [Geometry audit](../evidence/trooper-animation-pipeline/source-audit.json), [export audit](../evidence/trooper-animation-pipeline/export-audit.json), [grip audit](../evidence/trooper-animation-pipeline/grip-audit.json), [grip recipe](../evidence/trooper-animation-pipeline/grip-recipe.json), [provenance](../evidence/trooper-animation-pipeline/provenance.json), [attribution](../evidence/trooper-animation-pipeline/ATTRIBUTION.md).
- [Все клипы](../evidence/trooper-animation-pipeline/clip-overview.png), [фазы движения](../evidence/trooper-animation-pipeline/motion-phases.png), [исходник и combat side](../evidence/trooper-animation-pipeline/source-and-combat.png), [rest skeleton](../evidence/trooper-animation-pipeline/rig-skeleton.png), [кисти и хват](../evidence/trooper-animation-pipeline/hands-and-grip.png).

Локальные binary assets: `.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2/`. Git их не содержит. `SHA256SUMS.json` сопровождает источники, derivatives и scripts; evidence-index связывает PNG с GLB hash. Предыдущий `animation-pipeline-v1/` сохранён со своей QA.

## Проверено

Полный `scripts/trooper/run.sh` прошёл с Blender 5.2.1 LTS / Pillow 12.3.0. После исправления заслонённых ракурсов отдельно повторены hand readback/render, contact sheets и finalize; персонаж и клипы не менялись. Просмотрены итоговые локальные PNG тела, фаз движения, skeleton и изолированных кистей. Изоляция кисти применяется только к QA-картинкам; расчёт контакта использует всю исходную экспортированную поверхность перчатки.

GLB: 30 757 triangles, 6 meshes/materials, 1 skin, 57 joints; 20 101 экспортированная вершина с weights, 0 непокрытых вершин, максимум 4 influences. Все 30 digit bones и 4 twist bones реально имеют веса. Ошибок joint indices, нормализации и loop endpoints нет. Разница с 16 691 welded Blender vertices обусловлена GLB splits для normals/UV.

Проверено 119 поз (17 на каждый из 7 клипов). `PASS_TECHNICAL`, deformation gate `PASS`, errors/warnings пустые. Максимальное растяжение run снижено с прежних ~2,30x до **1,790x**; aim/fire — **1,977x**. Диагностический порог 2x не менялся и не является художественным стандартом. Минимальный floor Z в run — около -0,000873 м.

Хват: 14 поз обеих рук в aim/fire, `PASS_GRIP_FIXTURE`. Минимальный зазор поверхности до контрольного цилиндра — **0,854 мм**, зазоры несущих пальцев — **0,908–1,106 мм**. Радиус фиксирован; проверяется поверхность треугольников, а не только вершины. Пять геометрических fixtures проверяют расчёт проникновения и отсечение по конечной длине цилиндра.

Strict OpenSpec validation, syntax checks, `git diff --check`, checksum/evidence-index readback выполнены. Source checksum совпадает с основной локальной библиотекой.

## Осталось для принятия персонажа

- Художественная оценка и доработка движения: процедурные walk/run/death не выдаются за mocap или финальную анимацию. В PNG нет прежних диагностических разрывов ткани, но это не проверка анимации в реальном времени.
- Два контрольных цилиндра проверяют каждую кисть отдельно. Они не доказывают двухручный контакт с единым оружием: утверждённая модель оружия, взаимное положение рук, first-person и Animator transitions проверяются отдельно.
- Unity Humanoid Avatar, T-pose retarget и performance не проверены. 24 текстуры требуют оптимизации: raw RGBA8 level 0 — 360 MiB, GLB около 84 MiB.
- При первоначальном аудите Sketchfab live page ответила 403; CC BY provenance сохранён из исходного SOURCE.md и embedded GLB metadata.

Изменения simulation/runtime/input/public/manifest отсутствуют. Browser/playtest/deploy не выполнялись: это offline DCC кандидат. Change остаётся активным с открытым пунктом 2.3, до финальной художественной приёмки не архивируется.
