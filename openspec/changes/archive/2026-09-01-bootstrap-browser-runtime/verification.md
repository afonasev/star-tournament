# Verification evidence

Дата: 2026-09-01

## Automated checks

- `npm run typecheck`: passed.
- `npm test -- --run`: 10 files, 61 tests passed.
- `npm run build`: passed; production bundle создан, остаётся informational warning Vite о чанке больше 500 kB.
- `git diff --check`: passed.
- `openspec validate bootstrap-browser-runtime --strict`: change valid.

## Runtime smoke

- Dev server запущен из worktree change; Vite выбрал фактический URL `http://127.0.0.1:5179/` после занятых портов 5173–5178.
- `curl -I http://127.0.0.1:5179/`: `HTTP/1.1 200 OK`.
- In-app Browser: отображены одна WebGL diagnostic arena и отдельная DOM-диагностика со статусом `Симуляция активна`.
- Canvas, renderer dataset и simulation/UI используют `diagnostic-arena` с hash `fnv1a64-v1:47d7940ff2fc8aae`; активный профиль — `prototype-v1`.
- Tick продвинулся с `4 859` до `4 871` за 220 ms; console warnings/errors отсутствуют.
- Resize override с `1280x720` на `800x600`: CSS canvas, drawing buffer и playfield стали `800x600`, статус runtime остался активным; после проверки viewport reset.
- Screenshot diagnostic scene сохранён в Browser verification output текущей задачи.

## Не проверено

- Физический TV, отдельные GPU/драйверы и целевая browser matrix.
- Long-run GPU memory/performance и production bundle budget.
- Физические клавиатура/мышь и геймпады: соответствующий input/gameplay ещё вне scope этого foundation change.
