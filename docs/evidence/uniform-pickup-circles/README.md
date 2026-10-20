# F2 — единый радиус белых кругов

Стандартные профили четырёх карт используют radius1.35 m. Well v6 остаётся прежним; Bowl @11, тоннели @4, Луна @6. Жёлтый центр тоннелей сохраняет radius2.2 m. Карты/anchors/spawns/свет/gameplay не менялись. Historical snapshots/hashes и named custom values сохранены; untuned Default получает общий радиус.

Реальное сравнение: four-map-radius-comparison.png — одинаковые QA camera FOV70, RT960x720, eye offset (0,2.7,-3.1), lookAt anchor+(0,.15,0). Raw PNG каждой из40 точек в views/. Контактные листы и6 отдельных Well regression views сохранены. На Луне H/V круг вплотную к основанию перегородки, на W6 часть линии перекрывает существующее перило; геометрия и anchors сохранены.

Итог:25 existing EditMode PASS +4 uniform-profile EditMode PASS (29 уникальных); 5 PlayMode PASS, финальные процессы exit0. Исходный EditMode имел27 PASS/2 FAIL: новый fixture пробовал сохранить read-only radius Bowl через Lab.Save. Приложение корректно отказало. Fixture исправлен без ослабления guard/assertions; 4/4 повторного focused прогона PASS. Исходные FAIL сохранены.

Полный набор, Player/build/release/deploy и human/device acceptance не выполнялись.
