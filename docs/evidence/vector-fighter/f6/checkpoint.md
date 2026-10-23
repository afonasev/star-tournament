# VECTOR F6 — незавершённая переработка, 2026-10-22

Ветка `codex/vector-fighter`, baseline `239f657f728f7d20e2843332218979387885be0e`.
Рабочая копия: `/Users/eaafonasev/.codex/worktrees/vector-grip-refinement/star-tournament`.
Владелец продолжения: `01a1601c-bc6a-7c90-ac69-a383d5f881fb`.
Canonical change: `replace-player-with-vector-fighter`, implementing. Интеграция и публикация не выполнялись.

## Фактический результат

F4/F5 НЕ завершены. Не переносить кандидаты автоматически в production.

- Добавлен независимый `grip.<weapon>.supportPitch` в профиль @14 с default 0. Остальные defaults, все прежние assertions и production body/arms GLB сохранены.
- `BeforeVerticalSupportGrips` реконструирует @13 перед цепочкой старых профилей. Новая проверка ненулевого pitch сохраняет положение оружия, palm pivot, длины руки и frozen pose.
- CaptureGrip опционально выгружает реальные baked vertices перчатки, skin weights, кости и треугольники оружия. Это измерение, не автоматическая приёмка хвата.
- `fit_grip_surface.py` — offline помощник NumPy/SciPy, сопоставляющий skin vertices с размерами прямоугольной рукояти. Его приближение не доказывает отсутствие пересечений всех треугольников или контакта с остальным корпусом.
- Ориентация palm-longitudinal/knuckle-row дала более естественную линию запястья для вертикальных рукоятей. Оба последующих fitted digit кандидата визуально провалены: указательный палец у корпуса, неестественное расстояние между фалангами, дефекты skin/seams, неполный охват.
- Rifle/Shotgun и правая рука оставлены прежними. Реальные свежие кадры есть для каждого оружия, но новой визуальной приёмки нет.
- `remodel.py` извлекает выбранные sewn components pinned source, формирует новый торс и поверхности брони. Результат всё ещё схематичен и пересекается в posed review. Ray-clearance правка не исправила posed intersections. Это **не shipping source**; `f6-remodel-study.blend` — сохранённая неудачная проба.

## Evidence

- `grip-review.png`: последние реальные Unity кадры `grip-candidate2/`, явные overrides `surface-candidate2.txt`, НЕ factory defaults.
- `model-review.png`: настоящий экспортированный mesh, целиком и крупно. Visual FAIL.
- `model-candidate/`, `model-clearance/`: история фактических GLB renders и SHA.
- `diagnostics/`: предыдущие позы, измерения, actual OBJ/skin в gzip, XML и журналы.
- `verification.json`: окончательные результаты технических проверок; они не закрывают visual acceptance.
- `recovery.md`: повторное внешнее archive-cleanup в 01:24 UTC+5. Исходники восстановлены из `c7c1ac16a4e333a38df4c0a653f2860b0d5b90c2` в тот же путь/ветку. Warm Library и временные результаты были удалены внешним механизмом. Новый Library восстановлен импортом.

## Воспроизведение

`blender -b -noaudio --python scripts/vector_fighter/build.py -- --candidate --forms remodel`

`blender -b -noaudio --python scripts/vector_fighter/review.py -- --candidate --output <owned-output>`

Unity запускается через `UNITY_RUN_MODE=shared unity/tools.sh editor -batchmode -noaudio -muted -runTests -testPlatform PlayMode -testFilter FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene -testResults <output>/results.xml -logFile <output>/editor.log`.
Использовать отдельный `STAR_TOURNAMENT_QA_LAB_HISTORY`, `STAR_TOURNAMENT_GRIP_FACTORY=1`, `STAR_TOURNAMENT_GRIP_MESH=1`, `STAR_TOURNAMENT_WALK_EVIDENCE=<output>` и неизменяемый `STAR_TOURNAMENT_GRIP_OVERRIDES=<candidate.txt>`.

## Следующий шаг

Сменить способ изготовления: редактировать реальную posed геометрию костюма и skinning перчатки, проверяя силуэт и поверхности непосредственно по утверждённому VECTOR. Не повторять добавление деталей, сглаживание манекена или подбор одной абстрактной контактной точки. Ни один fitted candidate здесь не принят.

Рабочую копию, ветку, Library и inputs/candidate GLB в `.local/vector-f6`/`.local/vector-fighter` сохранить для разрешённого продолжения. Привязка worktree к текущему чату через app tool отклонена: `Cannot verify that this ownerless worktree belongs to this task.` Никакого архивирования из этой сессии не выполнялось. Чужие main/planning изменения сохранены.

Full suite, Player build, integration/release/deploy, human/device/performance acceptance: NotRun.

Первый compaction пережит после сохранения этого checkpoint; после него только завершение начатой focused QA и фиксация незавершённого результата. Новые visual studies не запускались.

Финальная focused EditMode QA: 30/30 PASS (765.44 s), включая оба ±30° pitch случая и все 11 исторических вариантов. Последний graphical capture: 1/1 PASS, visual FAIL. XML/log и source hashes сохранены.
