# Исправление текстур и прохождения сквозь архитектуру

2026-09-06, `codex/expand-procedural-arena-variety`. Этот отчёт заменяет прежнее визуальное доказательство после пользовательского сообщения о дефектах.

## Причины и исправления

- `wall-bay` устанавливался с исходным размером даже на коротких участках стены. GLB выходил в свободное пространство без соответствующего solid. Теперь каждый экземпляр вписан по измеренным bounds в половину ширины, высоту и половину толщины canonical стены; геометрия шаблона не меняется.
- `portal-facade` существовал только в renderer. Теперь authored box parts хранятся в общем `portalKit.json`, используемом generator и GLB authoring script. Canonical `portal-solid:*` имеют те же размеры, положения и повороты, блокируют капсулу и лучи. Uniform scale привязан к реальной ширине двери. Арки ставятся только между сегментами одной стеновой цепочки, без ложных «порталов» между gap-seal деталями.
- Shared-corner GLB получали только XY projection: боковые/горизонтальные грани имели вырожденные UV. Теперь грани разделены, UV вычисляются по dominant normal с учётом физического размера; метрический ритм применяется также к полу, потолку и деталям. Нормали пересчитываются, материалы клонируются и используют полный texture kit.
- Roughness повторно умножалась на material factor, усиливая зеркальные пятна на полу. Теперь ORM texture задаёт фактическую шероховатость и металличность без повторного ослабления; floor texture различима.
- Navigation исключает части дуг выше высоты стоящей капсулы и удаляет дублирующие planar bounds, полностью покрытые другими препятствиями. Rapier сохраняет все solids, включая верх арки для прыжков/выстрелов.

Generator identity: `arena-recipes-v4`; canonical schema 4 и profile revision 7. Старые полные definitions/replays не переписываются. Для проверки нужно начать новый матч, а не продолжать сохранённую арену предыдущей версии.

## Проверки

- Регрессии: каждый authored portal box совпадает с обоими GLB LOD; капсула блокируется стойкой и лучи встречают дугу во всех размерах; все UV-треугольники имеют ненулевую площадь; фактический wall-bay помещается в узкий host solid без изменения исходной геометрии.
- Матрица 96 definitions, физические spawn↔center маршруты 24 recipe/size/mirror комбинаций, рампы в обе стороны, legacy/replay/AI и renderer lifecycle входят в полный suite.
- Полный suite: **59 файлов / 345 тестов PASS**. Production build PASS, strict OpenSpec PASS, `git diff --check` PASS. Все проверки без звука.

## Performance после исправления

Репозиторный gate, production build, Chromium, DPR 2. Все восемь фаз каждого отчёта прошли. Пауза, меню и результаты — zero-work. Это cadence/budget evidence; новый длительный thermal soak не заявляется.

| Графика | Размер / seed | CSS viewport | Результат | Arena hash |
| --- | --- | --- | --- | --- |
| [low](perf-low.json) | medium / 1 | 1280×720 | PASS | `fnv1a64-v1:f16d504caeb3500d` |
| [balanced](perf-balanced.json) | medium / 1 | 1280×720 | PASS | `fnv1a64-v1:f16d504caeb3500d` |
| [high](perf-high.json) | medium / 1 | 1280×720 | PASS | `fnv1a64-v1:f16d504caeb3500d` |
| [ultra](perf-ultra.json) | medium / 1 | 1280×720 | PASS | `fnv1a64-v1:f16d504caeb3500d` |
| [ultra-4k-large](perf-ultra-4k-large.json) | large / 3 | 3840×2160 | PASS | `fnv1a64-v1:0cf61cb5d89504e0` |

## Browser и screenshots

Проверен dev-стенд этой ветки: `http://127.0.0.1:5180/?muted=1`, HTTP 200. Визуальный fixture использует shipping renderer, но не заменяет физический ввод. Просмотрены атриум medium/seed 1 и courtyard large/seed 3; общий вид, портал, окно, два барьера, ниша и рампа.

PNG-каталог: `/Users/eaafonasev/.codex/visualizations/2026/09/05/01a071b7-e49d-7442-8741-35812a2e83a8/`.

- `repair-overview.png`, `repair-portal.png`, `repair-window.png`.
- `repair-low-barrier.png`, `repair-high-barrier.png`, `repair-niche.png`, `repair-ramp.png`.
- `repair-courtyard.png` (large/seed 3).
- `repair-input-blocker.png` (обычный матч medium/seed 1, hash suffix `aeb3500d`).

Новый обычный матч успешно инициализирован. Кнопка «Начать игру» через in-app Browser по-прежнему сообщает об отказе захвата мыши; таймер остаётся 05:00. Это ограничение embedded automation, а не gameplay defect.

## Physical Chrome acceptance (2026-09-06)

По явному решению пользователя проверка продолжена в muted Google Chrome на dev-стенде этого worktree: `http://127.0.0.1:5187/?muted=1` (HTTP 200). Chrome успешно получил pointer lock и показал native banner о `Esc`; mouse-look подтвердил смену camera view. Просмотрены два отдельных recipe: `medium`/seed `1` (`split-atrium`, hash suffix `aeb3500d`) и `large`/seed `3` (`flanked-courtyard`, hash suffix `d89504e0`).

Пользователь выполнил и подтвердил physical keyboard/mouse acceptance в Chrome: проход рамп, столкновение с обоими profile-defined barrier heights, выстрелы через barriers и wall-relief niches, а также блокировку shots окнами на обоих recipe. Одиночные UI-automation click events не являются доказательством shots, поскольку завершаются между browser fixed ticks; это не было использовано как gameplay verdict. Текущие Chrome frames просмотрены в сессии для обоих recipes; screenshots остаются частью live browser evidence, а не подменяются synthetic fixture.

Main, архивирование и деплой не затрагивались.
