# F7: протокол диагностики

Основной кандидат v2: SHA256 дерева 820563539c1e87d58bb5f1f2cf0b74d37efff5582060d3836acb8b05d768b5f0. Точный путь и source revision — candidate.json. Development macOS Player, Unity 6000.3.23f1, Metal, ProvingURP; exclusive unity-run. Audio muted.

Один матч FFA, seed 20261003, 8 hard bots. Simulation заморожена, один viewport, yaw/pitch 0, FOV 90, quality 5. Камеры: двор (0,.02,-25), зал (0,.02,-9), верх (0,4.5,-9). Порядок двор→зал→верх→зал→двор, затем пары зал/двор для no-shadows, no-local-lights, no-upper-lights (y>4.5) и half-render-scale, затем повтор baseline. 5 секунд warmup + 8 секунд samples. Фокус записывается, PNG вне sample. Каждый режим восстанавливает свет и renderScale. Это опт-ин диагностический процесс, не настройка обычной игры.

Серии v2: performance-vsync — 1920×1080 windowed, штатный VSync1; performance-saved-display — 3456×2160 fullscreen, штатный VSync1, display120Hz. Они используют одни и те же байты Player. Saved display взят read-only из предпочтений; подтверждение пользователем исходных условий остаётся открытым.

Ранняя supplemental серия performance использует v1 (точные исходник/identity сохранены), 1080p uncapped, 3+6 секунд. Её GPU timing недостоверен; результаты не подменяют основную серию v2. В CSV v1 могли попадать повторные timing records. В v2 timestamp/fresh и пустые поля исключают трактовку stale/missing как новых нулевых измерений. Под high-resolution нагрузкой остаётся отдельная проблема неполных/противоречивых GPU данных; см. report.

CSV/JSON: wall-clock frame mean/p50/p95/p99, FrameTiming CPU/GPU/main/render/present wait, draw/SetPass/triangles/vertices, light inventory, mesh bounds. CPU total включает ожидание и не доказывает CPU-bound. Отдельные 600 bot/session ticks измеряются без отрисовки; они не заменяют natural match.

[FrameTiming API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/FrameTiming.html), [Rendering counters](https://docs.unity3d.com/Manual/ProfilerRendering.html).

Визуальные снимки обеих стен f7-west/f7-east: scripted frozen camera, seed20261003, 1920×1080. Проверены агентом; приёмка пользователя остаётся открытой. Physics и natural match — отдельные проверки, см. checks и visible/complete.json.
