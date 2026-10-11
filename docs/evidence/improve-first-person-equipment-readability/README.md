# First-person equipment readability — Editor evidence

Согласованный срез №1: читаемость тёмных частей оружия и рук без новой геометрии.
Baseline: `60870153f4476297ec04aa8304bfbd9b8d3682be`.
Implementation/source: `6d67f576b8174e875fa52a666f7545ea26d9f2be`.

## Result and tuning

Использован существующий `equipment-fill` point light, без добавления источников. Встроенный `unity-trooper-presentation-v1@8` меняет `view.fillIntensity` с 0.05 на 0.35. Control: Balance Lab → Оформление → Модель игрока → «Подсветка оборудования»; диапазон 0–4, шаг 0.05. Дальность, материалы, meshes, поза, оттенки, gameplay и world lighting не меняются.

На одинаковом кадре Industrial Tunnels сравнивались 0.05, 0.35, 0.65 и 0.95. Выбран 0.35: проявляет поверхности тёмной перчатки/корпуса, а более сильные варианты заметнее выбеливают цветные панели. Это инженерная визуальная оценка Editor, не human acceptance.

Untouched старый default обновляется; explicit off (0) и другие custom значения сохраняются. Старые Lab snapshots, revision numbers и hashes остаются неизменными. Новая trusted registry projection сохраняет точный predecessor v7/v6; повторное открытие не создаёт дубликатов.

## Comparisons

- `fill-variants.png`: один и тот же верхний левый viewport Industrial Tunnels / Rifle, четыре уровня.
- `comparison-overview.png`: четыре оружия в Industrial Tunnels, слева 0.05, справа выбранный 0.35.
- `captures/*-before.png` / `*-after.png`: полные 1600×900 изображения четырёх viewport для трёх арен × четырёх оружий. Каждый viewport — 800×450. Две пары альтернативного fill для Rifle сохранены отдельно.

Capture использует Unity Editor 6000.3.23f1, URP 17.3.0, GPU Metal (Apple M2 Pro), shared admission, без -nographics. Clock заморожен; camera matrices и число mesh/collider объектов проверяются между before/after. Остальные visual effects остаются 50%, SMAA Medium; единственная переменная внутри пары — intensity существующего equipment-fill. ScreenSpaceOverlay HUD не входит в camera RenderTexture; HUD/UI не изменяются.

## Focused checks

Команды проходят через `~/.local/bin/unity-run --shared --project <owned-worktree>/unity -- <Unity6000.3.23f1> -batchmode -projectPath <owned-worktree>/unity -runTests`:

- EditMode **10/10 Passed**, exit 0, `-testFilter FirstPersonWalkTests`: profile/default/serialized scene upgrade, explicit off/custom history, старые revisions/hashes и соседние walk/history contracts.
- PlayMode **2/2 Passed**, exit 0, `-testFilter VisualPolishTests.EquipmentFillComparesAllWeaponsWithoutLightingTheWorld;TrooperPresentationTests.SeatsShareAssetsButNeverRenderAnotherFirstPersonView`: actual GPU captures и camera/layer isolation для 1–4 viewport. Скрытое собственное оборудование даёт **byte-identical world/participant pixels** при 0.05 и 0.35 на всех четырёх камерах всех трёх арен.

XML и полезный render log сохранены в `test-results/`. В первоначальном расширении culling fixture один локальный игрок не имел обязательного второго участника матча; сценарий исправлен через существующий ensureOpponent и возврат в Menu перед редактированием roster, сохранив camera/layer assertions и явный подсчёт local views + world participants. Промежуточный запуск застрял в ошибочном fixture (RemoveBot во время матча); завершён только owned Unity PID, этот запуск — NotRun, не Passed.

Полный набор тестов, Player/release build, deploy, FPS/device и человеческая приёмка не выполнялись. Сборка не требовалась для затронутого Editor/API/render поведения.
