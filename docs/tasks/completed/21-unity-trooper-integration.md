# 21 — Unity trooper v2 integration

Статус: реализация, automated и muted native Player проверки завершены; отдельный commit. Change `integrate-unity-trooper`, ветка `codex/unity-trooper-integration`, worktree 9fd4.

База fc85267: текущий main26ce3ff является предком, native цепочка подключена без cherry-pick дубликатов. Main и старые worktree не редактировались. Pipeline files/evidence из a47acfe перенесены точечно без перезаписи GAME_SPEC/native секций. Старый handoff16 → `docs/evidence/trooper-animation-pipeline/CANDIDATE_HANDOFF.md`.

Новое поручение пользователя разрешает интеграцию существующего trooper v2/рук/анимаций и имеет приоритет над прежним candidate-only запретом. Художественная приёмка, physical устройства/TV, reference hardware/quality и long foreground60FPS остаются открытыми. Teams/bots/solo/восемь участников, archive/main/deploy в scope не входят.

## Восстановление

Исходный GLB SHA256 `53add01552d98244b75039c33319293c53e2eecc046b11890d6fa247fd13ef53` подтверждён. Blender5.2.1LTS/Pillow12.3.0, прежний scripts/trooper/run.sh выполнился: technical/deformation119 и grip fixture14 PASS. Исходник не изменён; rigid compatible-rig не использован.

Устойчивая копия source/SOURCE.md/полного animation-pipeline-v2: `/Users/eaafonasev/Documents/Codex/asset-vault/star-tournament/trooper-v2`, с RECOVERY.md. Shipping derivative и их proof будут сохранены отдельно. Локальный grip fixture не доказывает хват real shotgun.

## Архитектура

Read-only Astra memo: Generic Animator/manual Playables поверх editor-imported persisted clips; glTFast6.20.0 loop policy исправляется для one-shots. Animation clock = session clock, root motion только visual child, corpse не использует старый rigid90° fall. Body/arms shared immutable resources, независимые skeletons и viewport layers. Humanoid retarget/Avatar не требуется для собственных same-rig baked clips и не объявляется проверенным.

## Реализация

Native manifest заменяет robot на body/arms derivatives существующего v2. Семь editor-persisted clips управляются manual Playables; session clock замораживает pose/переходы, принятый shot запускает fire, уменьшение health — hit, отдельный corpse — death. Reset/respawn/Repeat очищают graph и timestamps. Исправлен найденный read-only review дефект: immediate reset сохраняет завершённые веса, поэтому повторный Render в тот же clock не возвращает bind pose. Отдельный regression сравнивает реальные кости body/arms.

Модель имеет общий набор материалов/текстур; per-seat идентичность оружия задаётся MaterialPropertyBlock. Первая персона использует same-rig производную с анатомическим срезом выше локтя; обрезка только по весам была отклонена после Player QA из-за видимых фрагментов брони. Положение рук и animation thresholds/blend принадлежат `unity-trooper-presentation-v1@1` с metadata. Gameplay/colliders/hit zones не меняются.

Shotgun — исходная world-модель, старая first-person robot geometry не включается. В offline bake сохранены исходные lower-body/death движения; верх тела следует measured grip/support points, пальцы подогнаны к реальным поверхностям. Нормализованный исходник и skin weights не редактировались. Для shoulder seam поворот разделён между LeftShoulder/UpperArm; правый thumb curl ограничен. Fitter и bake используют одинаковый source-frame baseline и вычисляют wrist после итогового hand rotation.

Независимый shipping readback: body/arms содержат 57 bones и семь clips; 44 full-hand/actual-shotgun surface samples дают 0 BVH overlaps; 119 deformation poses проходят прежний 2× threshold, максимум 1,945392×. Левый open-palm support и правый хват имеют измеренные contact distances в evidence; это не художественная оценка естественности пальцев.

## Проверки

EditMode 31/31, PlayMode 28/28, Mac Development build PASS (459131781 file bytes). Muted native Player: 36 состояний FHD и 36 в 4K, все focused, точные output размеры, все семь clips; pause сохраняет clock/bone poses, death/respawn/Repeat работают. Ordinary UI setup/keyboard join/diagnostic/pause/Repeat/menu/Exit проверены отдельно через CUA. После тестов исправлен только старт diagnostic warmup до focus; финальная сборка с этим guard прошла оба foreground probe.

При 2/3/4 seats общими остаются 7 skinned meshes, 16 materials и 27 textures; число skin instances 14/21/28. Короткий M2 Pro foreground sample: FHD frame p95 9,162 ms; 4K p95 9,292 ms, p99 16,718 ms, worst 17,789 ms. Это четыре stationary animated troopers, 3s warmup +10s measurement, без Editor/Blender/CPU jobs; не доказательство длительных 60FPS. Старого baseline Player нет, сравнение с rigid robot не заявляется.

Полные результаты, recipe/hash, независимый контакт/deformation audit, PNG и ограничения: [evidence](../../evidence/unity-trooper-2026-09-19/README.md). XML/logs, editable shipping blend, source/v2, финальные derivatives, scripts и proof сохранены в `/Users/eaafonasev/Documents/Codex/asset-vault/star-tournament/trooper-v2/unity-shipping`. Checksum readback подтверждён. Исходный source SHA повторно проверен в worktree и durable vault.

Strict OpenSpec validation PASS. Runtime read-only Astra review завершён, найденный immediate-weight blocker исправлен и покрыт тестом. Не приняты художественное качество походки/хвата/падения, physical input/TV, reference hardware/quality/internal scale и long foreground60FPS. Этап 2 roadmap не завершён. Main/archive/deploy не выполнялись; native commits остаются на отдельной ветке.
