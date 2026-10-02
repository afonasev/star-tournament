# Приёмка окраски игрока

Кандидат: `local-macos-player-candidate/e5cface32dfb246d86665bd9612361f505ebc6e5/player-color-zones`.

Приложение: `/Users/eaafonasev/Projects/star-tournament-builds/player-color-zones-e5cface/StarTournamentProvingGround.app`.

Tree SHA-256: `1352bfd2bd79485a896b30b6482e100d543ce0ec02c69c0ed170bb39475fd6b5`.

Исходники: `e5cface32dfb246d86665bd9612361f505ebc6e5`. Palette `unity-participant-presentation-v1@2`; исходные RGB сохранены. Арена — текущий authored Combat Bowl. Screenshot fixtures фиксируют восемь цветов, фронт/бок/спину, шлем крупно и четыре viewport; камеры и позиции диагностические, участники-fixtures не playable AI. QA без звука. FPS цифры этих кадров не являются performance acceptance.

Evidence в code repository: `docs/evidence/improve-player-color-zones/player/01-eight-front.png`, `02-eight-side.png`, `03-eight-rear.png`, `04-helmet-front.png`, `05-helmet-side.png`, `06-helmet-rear.png`, `07-four-views.png`; точный manifest и результаты — рядом в release.json / verification.json.

Проверить на этих кадрах и/или в указанном Player: большие зоны костюма и шлема читаются цветом участника со всех сторон; постоянного бордового оттенка нет, швы и фактура сохраняются. Красный участник остаётся красным. В Teams те же зоны используют цвет команды; boost сохраняет цвет тела и отдельную индикацию оружия.

Human acceptance пока pending. Явное решение привязывается helper accept к этому exact candidate; согласование варианта 1 было решением о scope, а не приёмкой Player.
