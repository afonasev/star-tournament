# F2 — центральный зал «Ветеран лиги»

Change: `enhance-combat-bowl-veteran-slice`, canonical store `star-tournament-planning`.
Owner: `01a1031f-9fe0-7d12-aa5a-79b38ab2138f`.
Branch: `codex/veteran-central-hall`; baseline `06877122409c9535cfdf0eae43ffe6f6420862bb`.

Запрос: низкий проём; переработать весь центральный зал для новой визуальной оценки. Старый северный образец не принят и сохранён.

Результат реализации: проёмы N/S высотой 3.5 m, экран на западной стене, единая облицовка зала/колонн, резиновое покрытие с разметкой, потолочные коммуникации и разные сервисные зоны. Маршруты, спавны, бонусы и существующие физические укрытия сохранены. Карта `combat-bowl-v1@18`, authoring `@12`, Orbital ring `@10`.

Проверки: full `make check`, физика высоких проёмов и укрытий, muted Player с Repeat и PNG всего зала. Результаты и точный Player записаны в `docs/evidence/combat-bowl-veteran-hall-f2/verification.json`.

Human visual/gameplay acceptance открыта. Внешняя публикация не разрешена. Предыдущий Player `combat-bowl-veteran-8a17241` не удалять.

Проверено: 324/324 EditMode, 199/199 итоговый PlayMode, build и muted Player/Repeat. PNG просмотрены. Exact candidate 89595b8 сохранён вне worktree, human acceptance ожидается. Lifecycle, интеграция и cleanup закрепляются в canonical delivery.json.
