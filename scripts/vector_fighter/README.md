# B01 — предплечья от первого лица, 2026-10-30

`python3 scripts/vector_fighter/port_b01_arms.py --promote` переносит точные
оптимизированные поверхности B01 на FPS-модель из pinned `70828f74`.
Редактируемый источник брони — `vector-body-b01.blend`; исходные FPS кисти,
крепления и оружие сохранены в pinned GLB. Скрипт не пересэмплирует их:
сохраняются topology/UV/normals/weights, skeleton и bind transforms. Защита
promotion отказывается перезаписывать более новые body/FPS assets.

До: 32524 skin triangles, 7 arm material slots. После: 36705, 9 slots,
в пределах существующих 45000. Переиспользуется выполненная для B01 bounded
оптимизация; второе упрощение не требуется. Неиспользуемые старые surfaces,
resource tables и buffers удаляются. PBR карты и материалы общие с body через
`TrooperPreparation`, отдельного нового атласа нет. Тени FPS остаются выключены;
для этой всегда близкой модели distance LOD неприменим. Это учёт ресурсов,
не измерение CPU/GPU/FPS.

После promotion: `TrooperPreparation.RefreshExistingScene`, затем affected
`TrooperImportTests` и native `WristDigitReviewTests` (output через
`STAR_WRIST_DIGIT_CAPTURE`), плюс проверка COBALT в `TrooperPresentationTests`.
Все Unity-запуски — через `unity-run --shared`, `-noaudio -muted`, с отдельной
QA history. Все пять кадров получают второй offscreen submission перед чтением
пикселей; `STAR_CAPTURE_FIRST_SUBMISSION=1` сохраняет и первый кадр для сравнения
при той же позе/свете. Внеэкранный capture не является Player/device приёмкой.

# B01 — реконструкция всего костюма по VECTOR REFERENCE STUDY

Новый исходник `vector-body-b01.blend` содержит заново построенные торс/спину,
плечи/руки, таз, ноги и обувь. `reconstruct_body.py` сохраняет только F02 head,
rig, кисти и equipment из `f23-reconstructed.blend`; прежние body meshes удаляются.
Новые ткани используют анатомические сечения; броня охватывает их замкнутыми
оболочками. Три новых материала имеют общий уникальный 2048² albedo/normal/ORM
атлас. Metallic ткани запекается как 0, независимо от металла/покрытия;
`audit_body_maps.py` проверяет фактический ORM по UV экспортированных тканевых
треугольников. Старые canonical материалы сохранены на реальных деталях для точного
FPS material aliasing. FPS GLB, клипы, gameplay/camera/collider не изменены.

```sh
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/reconstruct_body.py
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/texture_body.py
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/export_body.py
python3 scripts/vector_fighter/audit_body.py --output .local/vector-body-reconstruction/audit.json
python3 scripts/vector_fighter/catalog_game_bodies.py --output .local/vector-body-reconstruction/catalog.json --extract unity/Assets/B01Review
```

Экспорт создаёт high/candidate GLB в `.local/vector-body-reconstruction`, без
автоматического продвижения. Отдельная оптимизация сохраняет защищённые детали,
удаляет только полностью закрытые новой бронёй участки предплечной ткани на той
же кости и сокращает новые поверхности с bidirectional sampled error ≤0.7 mm.
Предел не является gameplay/acceptance числом. Исходник остаётся полным.
Финальный world: 176352 треугольника (исходный F23: 603575; новый high: 234566),
world-предплечья: 36705 ≤ существующих 38562; FPS ≤45000 без изменения ассета.
Материалы 19→22, embedded images 21→24 — стоимость нового общего body атласа.

Native review: `B01SurfaceReviewTests` проверяет отклик фактуры после assigned tint;
`B01HistoryReviewTests` использует manifest из `STAR_B01_GALLERY` и output из
`STAR_B01_HISTORY_EVIDENCE`. Исторические native тела имеют общие idle/run, цвет,
свет и камеры; два browser-era GLB и процедурная фигура имеют другой rig и явно
помеченную authored-rest позу. `STAR_B01_GALLERY_IDS` позволяет переснять только
указанные через `;` ревизии после изменения, сохраняя полный catalog gate.
Процедурная фигура восстановлена по точным размерам исходного renderer; tessellation
Unity отличается от Three.js. Это native сравнение ассетов, не запуск старого web runtime.

Доказательства: shared planning `docs/evidence/reconstruct-vector-body-from-reference/2026-10-29`.
Первый EditMode FAIL на forearm budget сохранён; тесты не ослаблялись. Полные тесты,
Player build/release/deploy и physical/human acceptance не выполнялись. Человеческая
оценка сходства с выбранным макетом остаётся открытой.

# F19 VECTOR — детализированный художественный исходник (F01)

Текущий этап: качество модели без лимита треугольников. Решение пользователя
2026-10-29: сначала форма, материалы и детализация, затем при необходимости
оптимизация для игры. Runtime GLB, префабы и first-person модель не обновлены.
Художественная приёмка остаётся открытой.

`author_reference.py` воспроизводит `f19-reference.blend` из сохранённого F18.
Броня состоит из отдельных замкнутых оболочек: плоские фрезерованные пластины
и параметрические облегающие секции с согласованными изнанкой и кромками.
Плечи и шлем используют прилегающие слои; исходник сохраняет rig/weights.
Изображения `textures/reference-*` подключены в материалы: покрытие и ткань
получают согласованные albedo/normal; покрытие также packed roughness/metallic.
Авторский исходник хранит отдельные объекты, фаски, крепёж и материалы.

```sh
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/author_reference.py
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/export_reference.py -- --reference
python3 scripts/vector_fighter/audit_reference_art.py --source .local/vector-reference/reference/candidate-body.glb --output .local/vector-reference/art-audit.json
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/review.py -- --source .local/vector-reference/reference/candidate-body.glb --views model-front,model-side,model-back,helmet,torso --output .local/vector-reference/art-review
```

`--reference` не сокращает сетку и не продвигает ассет в игру. Art-audit проверяет
точные binds, weapon/FPS/source/clips, finite geometry, skin weights и PBR;
это не тест визуального качества и не runtime-ready PASS. Игровой
`audit_reference.py` сохраняет прежний cap 32,989, отложенный на художественном
этапе. Не ослаблять его assertions ради исходника. `--promote` и native QA —
следующий этап после оценки модели. Full/Player/release/deploy не разрешены.

Выбранный референс и реальные рендеры: shared planning
`docs/evidence/refine-vector-heavy-agile-fighter/rework-f01`.
Ранние итерации содержат сохранённые FAIL, а не принятую реализацию.

# F18 ASSAULT V2 — heavy agile fighter candidate

Current implementation source: `f18-heavy-agile.blend`, derived from retained F17 by
`author_heavy_agile.py`. The model has a stronger torso/core/legs, continuous lower
garment, shaped abdominal/hip/lumbar/leg armor, seated boot plates and shared packed
PBR panel/textile maps. The approved concept remains in shared planning evidence:
`docs/evidence/refine-vector-heavy-agile-fighter/approved-assault-v2.png`.
Human visual acceptance is pending; generated concepts are not implementation evidence.

```sh
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/author_heavy_agile.py
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/export_heavy_agile.py
# Inspect the candidate before promotion into this worktree:
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/export_heavy_agile.py -- --promote
python3 scripts/vector_fighter/audit_heavy_agile.py --output .local/vector-heavy-agile/audit.json
```

`--reference` exports the same source without reduction for comparable render QA.
New panels retain authored bevel topology; cloth, sleeves and world gloves use the
existing resource strategy. The world cap remains 32,989 skin triangles, first-person
GLB bytes remain unchanged. Current weapon buffers are copied exactly from the task's
pinned `9d2e15bf` baseline because the F17 Blender source embeds an older shotgun.
The exporter prunes unused material/texture/accessor/buffer payloads after that copy.
Re-run `TrooperPreparation.RefreshExistingScene` through the shared Unity wrapper to
refresh persisted prefabs without changing existing animation clips.

Trooper profile @30 owns longer steps, coherent pelvis height, direction response,
pivot stepping and foot roll. The visual controller uses real root displacement,
geometric support reach and world-space stance orientation; gameplay/camera are not
moved. `BeforeHeavyAgileGait` preserves @29 history. Physical/gameplay/human acceptance
and full-suite/Player/release/deploy are separate gates.

Historical notes below describe the preserved sources and prior contracts.

# Current F17 assault model

Selected direction: concept 1, 2026-10-24; subsequent user refinement paints the complete pectoral chest plate in participant/team color. `f17-assault.blend` is the editable world source, reproducibly derived from immutable F16:

```sh
blender -b -noaudio --python scripts/vector_fighter/author_assault.py
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced --promote
python3 scripts/vector_fighter/audit_assault.py --output .local/vector-assault/audit.json
```

First-person geometry still starts from F16; only the neutral/identity material partition changes. Glove anatomy, named skeleton, weapon and image bytes are exact. Offline asset dimensions in author_assault.py are the selected sculpt proportions, not new gameplay/Balance Lab controls. The exporter writes the authored neutral glTF baseColorFactor explicitly because Blender drops the linked MixRGB constant; it shares the original texture bytes instead of baking new maps. Paint seams are cut again after mesh collapse so the final color boundaries follow continuous edges. This adds a small amount of topology within the original world/view caps.

The historical audit_runtime.py proves the old optimization-only contract at its original source revision. It deliberately retains material/source assertions; use audit_assault.py for F17, which permits only the two specified neutral material additions with exact maps/factors and retains strict compatibility/geometry budgets.

Historical source notes follow; early builders do not rebuild F17.

# VECTOR fighter — editable sources and runtime meshes

The accepted visual source is `f16-orbital-hands.blend`; current runtime assets are
derived by `optimize_runtime.py` as documented below. Retain this detailed source
and the established rig, gloves and grips. The original visual target is
`docs/evidence/vector-fighter/approved-vector-concept.png`.

The early builders and F1–F15 notes below are historical studies. Their rough or
unaccepted status does not describe the accepted F16 source. Do not replace the
shipping model by rerunning an early builder alone.

This local Blender pipeline preserves the existing compatible skeleton, authored
clips, weapon mount hierarchy and articulated glove derivative. The suit and closed
helmet are new geometry. Retained content is attributed in
`unity/Assets/StarTournament/Art/trooper-ATTRIBUTION.md`.

With Blender on PATH:

```sh
blender -b -noaudio --python scripts/vector_fighter/build.py -- --candidate
blender -b -noaudio --python scripts/vector_fighter/review.py -- --candidate
```

Candidate mode writes only `.local/vector-fighter/`. Without `--candidate`, the
builder replaces the two Unity GLBs and updates their manifest hashes. Do not run
that export while Unity is using this worktree.

The checked-in `vector-fighter.blend` is the earlier editable snapshot. The builder saves `.local/vector-fighter/vector-fighter.blend` with separate armor
objects, skin weights, materials and the compatible animation rig before batching
render meshes. `--source-only` saves this editable source without changing GLBs.
All original source inputs are pinned Git blobs from
`00fbd33209eb755dd5f86499aa8ea12867a2efd0`; rebuilding never imports its own output.

`review.py` imports the actual GLB and renders front/back/helmet views using a
display-only teal override. `render-source.json` records the exact mesh hash.
These studio renders do not validate Unity rendering or gameplay.

The new runtime airborne and landing poses are presentation-only additions; the
existing physics-driven death remains authoritative for corpses. Focused Unity
checks and their results are recorded in the evidence directory.

F1 (2026-10-21) uses slimmer authored arm dimensions, thinner ceramic shells,
a contoured breastplate, revised jaw/shoulder/knee/back panels and packed coating grain.
The cloth is nonmetallic, fully rough, with a reduced normal-map amplitude.
Unity Trooper profile @10 places its existing per-seat equipment fill behind the
camera (`view.fillBackMeters`) to avoid near-field sleeve overexposure. This does
not scale or move the camera, weapon grips or skeleton. The earlier @9 profile
can still be reconstructed by the history registry.

F2 broadens the shoulder cups, upper arms and upper torso while retaining the F1
forearm dimensions/material. Chest and belt hardware, intake slots, engraved
markings and ear grille are authored geometry. Trooper @11 adds
`grip.primaryWristYaw`: the right wrist fits inward, but the sampled weapon world
pose is preserved. Restore the previous additive correction before graph sampling
so clips without wrist/mount channels remain idempotent. @10 history remains
reconstructible through BeforePrimaryGripFit.


F4/F5 recovery and rejected visual route (2026-10-21): `--details tailored`
and `--details technical` are development studies, not accepted detail upgrades.
After provisionally choosing technical armor, the user rejected the primitive
construction itself. Do not ship these attached service details.

`--candidate --forms planar` reconstructs garment continuity and new shell
cross-sections; `primary-form-study.blend` is its editable, **unaccepted** study.
This is still too schematic and is NOT the shipping model. Its real GLB renders
are in `docs/evidence/vector-fighter/f5-surface-study/`. The production source
`vector-fighter.blend` and shipping GLBs remain unchanged by these studies.
The remaining model work is primary form/art quality against the approved
VECTOR reference, not adding more objects or subdividing existing parts.

`--candidate --forms authored-cloth` is a diagnostic of retained source clothing.
It still includes source vest/pad geometry and visibly intersects the new shells;
it is NOT a proposed finished model. Do not use it as production or acceptance
 evidence. A clean authored garment extraction and proper surface remodeling
remain necessary; automated overlaying is not a completed substitute.

F6 (2026-10-22): `--candidate --forms remodel` extracts pinned sewn sleeve/trouser
components and authors new torso/shell surfaces. `f6-remodel-study.blend` is an
unaccepted study, NOT the production source. Its full-body renders still show
cloth/armor intersections and insufficient concept fidelity; production export
remains blocked by the candidate assertion. Do not promote it automatically.

`fit_grip_surface.py CAPTURE_DIR OUTPUT.txt` fits per-digit overrides against
Unity-baked glove vertices and measured rectangular support-grip bounds using
NumPy/SciPy. It is an offline art aid, not a mesh collision proof or execution test.
Its outputs require actual Unity capture review. Actual weapon triangle OBJ and
skin/axes data come from the optional CaptureGrip evidence path.

F7 repairs the existing F6 costume with `blender -b -noaudio --python
scripts/vector_fighter/refine_pose.py`. It creates no additional costume objects:
rigid panels retain their designed surfaces, covered cloth follows those panels,
and existing glove seam islands receive cloth-interpolated skin weights.
`f7-pose-repair.blend` is the editable source; real exported-model renders and
native execution results are recorded in `docs/evidence/vector-fighter/f7/`.
The body gait uses actual root displacement and planted world-space support feet;
only the visual pelvis/legs move, never the simulation actor or camera.

F8 repairs the preserved F7 glove with `blender -b -noaudio --python
scripts/vector_fighter/refine_glove.py`. Finger cloth follows its own anatomical
chain, existing seam islands conform to that cloth, and existing finger/palm
plate layers are seated on the garment. Each dorsal rim/coating pair is fitted
as one existing assembly with a narrower footprint and preserved layer spacing.
Object, vertex, bone and animation
counts are preserved. Editable source: `f8-glove-repair.blend`; diagnostic
exports go to `.local/vector-f8/`. This script does not promote GLBs or factory
parameters automatically. Trooper @16 supplies separately authored closed
RocketLauncher/Cutter grips; `BeforeClosedVerticalGrips` preserves exact @15
factory defaults for saved history. Actual Unity evidence and remaining visual
gates are recorded in `docs/evidence/vector-fighter/f8/`.

## Runtime mesh optimization (2026-10-24)

The accepted F16 source is `f16-orbital-hands.blend`. Keep this editable high-poly
source intact. The original optimization derived independent world and first-person
meshes from it; the original glove vertices are retained in first-person. This
avoids using the expensive first-person sleeves/gloves on every bot. Materials,
embedded textures, bind skeleton, weapon meshes and Unity's persisted clips remain
unchanged. No distance-based LOD or runtime camera/quality setting is introduced.

```sh
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant conservative
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced
# Only after reviewing the generated candidates:
blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced --promote
```

Candidates and per-object surface-error/count reports go to
`.local/vector-runtime/<variant>/`. `--promote` updates shipping GLBs and their
manifest hashes; it does not publish a release. After promotion run
`TrooperPreparation.RefreshExistingScene` through the Unity wrapper to regenerate
only the two persisted prefabs while retaining the accepted animation clips.
World and view meshes deliberately have distinct mesh names and resolutions;
sharing unequal same-name skins is forbidden.

For the optional `VectorMeshOptimizationReviewTests` graphical comparison, copy
each generated candidate pair to `Assets/StarTournament/Art/RuntimeMeshReview/`
as `<variant>-body.glb` and `<variant>-arms.glb`. Use an owned worktree, set an
absolute `STAR_TOURNAMENT_MESH_EVIDENCE`, run the explicit review through unity-run
with graphics and `-noaudio -muted`, and choose exclusive for timings. Remove
these temporary review imports after preserving the report/images. The fixture
remaps per-slot identity property blocks with the material roles; merely swapping
meshes can otherwise produce misleading colors. Frozen timing is diagnostic;
Windows Player/human acceptance remains separate.

`python3 scripts/vector_fighter/audit_runtime.py --output .local/vector-runtime/audit.json`
checks the actual exports without Unity: budgets, manifest hashes, named bind
matrices, normalized weights, material/texture identity, untouched weapon and
unchanged first-person glove triangles with UVs/normals/named skin weights. The
reference defaults to the last pre-optimization source commit. It also verifies
that the editable high-poly source remains byte-identical to that revision.

Set `STAR_TOURNAMENT_MESH_SHIPPING_ONLY=1` for a graphical capture of the actual
current prefabs without candidate imports or timing loops. That mode supports
unity-run shared; measured multi-variant comparison uses exclusive.

## Restored shotgun surface maps (2026-10-25)
After exporting the final runtime GLBs, run `python3 tools/restore-shotgun-surfaces.py`. This append-only pass reuses the original 128px shotgun metal map from double-barrel-shotgun-lod0.glb on vector-chassis/vector-titanium. It preserves all existing geometry, UV, skin, clip and image bytes, shares one new texture between the two materials per file, and updates asset-manifest hashes. Existing ceramic/glove maps remain intact.

## F23 color coverage and native pose review

`refine_f23.py` derives `f23-color-coverage.blend` from pinned F22 by assigning the
existing identity material to 16 major armor panels. Geometry, skin, gloves and
weapon are unchanged. Export with `export_reference.py --reference --source
scripts/vector_fighter/f23-color-coverage.blend --output-root .local/vector-f23`.
This is an unreduced art candidate, never runtime promotion.

The GLB's embedded clips do not contain the game's finger/wrist IK. For an
honest grip preview, run Unity Editor `VectorArtPoseExport.Run` with
`STAR_TOURNAMENT_ART_POSES` set to an absolute JSON output path, through the shared
`unity-run` wrapper and `-batchmode -noaudio -muted -nographics -quit`. It executes
actual `TrooperVisual`, `OrbitalGripPose` and grounded gait with factory tuning,
persisted clips and measured actor displacement. It writes evidence only.

`blender -b -noaudio --python-exit-code 1 --python
scripts/vector_fighter/review_native.py -- --poses PATH --output OUTPUT --details`
uses that native skin/weapon palette on the actual art GLB. Add `--fps` for the
preserved shipping first-person arms, factory camera offset and vertical FOV.
Images are Blender studio renders with native pose data, not Unity screenshots
or Player/device acceptance. The FPS source is deliberately separate from the
world body and retains its existing broad identity forearm panels.

## F23 adaptive world sampling (2026-10-29)

`optimize_f23_world.py` derives a candidate from the unchanged
`f23-color-coverage.blend`. It reuses `reconstruct_f22.py`'s manufacturing cages
at 12/24 interior samples and retains the original panel whenever the sampled
surface distance exceeds 1 mm in either direction. This is an offline export
choice, not an acceptance threshold. Bevel resolution, material slots, all
nonarmor geometry, gloves, rig and equipment remain preserved. The source blend
is never overwritten; FPS geometry is not exported or modified.

```sh
blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/optimize_f23_world.py
```

The candidate, derived editable sources and per-panel error/fallback report go
to `.local/f23-adaptive-runtime/`. The script never promotes automatically.
Current runtime candidate: 678,893 skinned triangles (source export: 1,070,223).
The existing user-authorized test cap remains unchanged. Real before/after
rendering, native import/grip/gait/contact checks and Player timing are required
before integration; this triangle count alone is not a performance or visual PASS.

`NativeModelPerformanceReview.cs` and `ModelPerformanceBuild.cs` originate from
`codex/vector-reference-rework@6951a008794f53407dd7cfa713eaf73bfb55e97c`.
The F23 follow-up adds contact CPU markers, a fixed 24-shot surface diagnostic,
post-timing native front/side/back/motion images, and an original-F23 resource
in the same explicitly requested diagnostic build. The original-body mode
restores the former duplicate source/proxy shadow routing for comparison.
Private generated baseline assets must be removed from Assets after building;
the exact diagnostic app and durable evidence remain retained for review.

The confirmed surface hot path uses an opt-in weak cache of immutable imported
mesh topology/bind attributes in `HitFeedbackPresentation`. Dynamic/general
`SkinnedContactPatch` callers still read fresh source data by default. Baked
vertices, world transforms and block bounds are rebuilt per impact. The existing
exhaustive reference test exercises the product cache across changed transforms;
`-f23ContactReview <absolute output>` runs 24 uncached/cached CPU probes per body
in an isolated diagnostic Player (exclusive, muted), including the initial fill.

Dense F23 faces exposed `Vector3.normalized`'s small-vector cutoff in contact
geometry. Surface normals now normalize every nondegenerate face explicitly;
the exhaustive reference keeps its historical default and opts into precise
normals for the current exact comparison. An independent 2 mm skinned-face
regression test verifies a unit normal and a complete visible patch.

Supporting `F23SurfaceAllocationReview.Run` records one optimized asset in ABBA
order in Editor. It is not Player FPS. A zero allocation counter delta is marked
unavailable (the local Mono counter returned zero even in allocating phases),
never reported as zero allocations. Native Player pairs and raw tails remain
the primary runtime evidence.

## Rounded F23 — helmet 2 / color 3 (2026-10-29)

`f23-round-color.blend` is the editable successor for the selected world helmet and color panels; `f23-color-coverage.blend` remains intact. Reproduce with `blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/round_f23_helmet.py`, then `blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/optimize_f23_world.py -- --source scripts/vector_fighter/f23-round-color.blend --output .local/f23-round/optimized`. The latter is candidate-only. New helmet surfaces use an analytic rounded shell with existing material/Head skin roles; surviving F22 body cages retain the adaptive export pass. Do not run the old blanket collapse exporter.

`audit_round_f23.py` checks actual GLB compatibility against a supplied baseline plus the unreduced export. Only world GLB and its hash records are promoted; independent FPS mesh, all grips, camera, clips and gameplay stay unchanged. Review actual imported renders and Unity views separately from structural audits. Canonical decision/evidence: shared planning change `round-f23-helmet-color-panels`.

## F23 F01 military helmet (approved 2026-10-29)

`military_f23_helmet.py` derives `f23-military-color.blend` from the preserved `f23-round-color.blend`. Only rigid Head objects are rebuilt, retaining the comms modules. The generator asserts exact geometry, weights, transforms, UVs and material assignments for all 558 retained objects. Color scheme 3 remains intact.

Reproduce with `blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/military_f23_helmet.py`, then the candidate-only `optimize_f23_world.py -- --source scripts/vector_fighter/f23-military-color.blend --output .local/f23-military/optimized`. The helmet uses a low crown, distinct visor/brow and cheek/chin volumes, real vent apertures and recessed grille ribs; existing materials/textures and Head skin are reused. Body optimization retains the prior adaptive cage policy. Keep both prior sources for recovery. Verify exported GLB, prefab material order and native frames before promotion.
