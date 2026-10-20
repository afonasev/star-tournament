# Постоянные круги точек подбора

Аудит: Combat Bowl 12/12; Технические тоннели 6/6; Лунная лаборатория 11/11; Колодец 11/11. Исправлены тоннели и Луна; принятые Bowl/Well renderer sources не менялись. Секторное кольцо тоннелей и подписи Луны сохранены.

Финальный focused QA: 25 EditMode + 5 PlayMode PASS, 0 FAIL/skip, каждый финальный процесс exit 0. Исходные FAIL fixture и crash Editor после 5/5 PASS сохранены отдельно; причины и исправления в JSON. Полного прогона, сборки и публикации не было.

Реальные PNG каждой точки в views/<map>/<pickup>.png; четыре contact sheets для обзора. well-regression-views содержит шесть независимых принятых ракурсов Well. Это source/Editor evidence, не human/device acceptance. verification.json фиксирует tested source tree, фильтры, результаты и SHA256 файлов.
