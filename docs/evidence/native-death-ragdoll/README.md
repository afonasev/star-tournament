# Native death ragdoll

Изменение: `add-weapon-driven-death-ragdoll`. Текущий локальный macOS Player собран из `881dcacccd0c8179007441f38c4b6948d65e8d8f`, профиль `unity-death-presentation-v1@2`. После просмотра первого кандидата все четыре силы увеличены ×1.5: автомат 3.75, дробовик 9, ракета 13.5, резак 0.375 м/с. Каждая сила редактируется отдельно в Balance Lab → «Оформление → Отбрасывание тела». Исходный кандидат `262518ea2ec8ac6e81f76e40ae1bc82d0e535810` и его кадры сохранены ниже как историческое сравнение.

Passive ragdoll создаётся из текущей позы и скорости, получает однократный импульс по направлению попадания либо от центра взрыва, контактирует с копией окружения в отдельной physics scene и следует session clock. Оружие погибшего скрыто, двуручная поза и animation writers выключены. Смена старых Lab ревизий и пользовательские значения проверены отдельно.

## Текущий кандидат ×1.5

- [Манифест неизменяемой локальной сборки](candidate-1.5x.json): build exit 0, `STAR_PROVING_BUILD_PASS 471124036`; app tree SHA-256 `96c66504810b4083d4ef788284a123c972527e5849a44df08af71a4abbe64ec3`.
- Affected EditMode: 27/27, natural exit 0, 160.53 s ([XML](editmode-1.5x.xml)); affected PlayMode: 20/20, natural exit 0, 39.74 s ([XML](playmode-1.5x.xml)). Прежние полные EditMode 203/203 и PlayMode 138/138 относились к исходному кандидату; после усиления и оптимизации миграции повторялись затронутые тесты.
- Immutable native Player: natural exit 0, muted scripted review, arena `combat-bowl-v1@16`, frozen Lab hash `5fb48228e2cb941949a1c4d9d1dd50096d489cab66f908e812a86f1944b5f394`. Все четыре реальные смертельные пути, смерть в воздухе, ступени/стена/край, pause, respawn, Repeat и четыре viewport прошли scripted guards ([отчёт](player-1.5x/complete.json), [журнал](player-1.5x/native-player.log)). Полные последовательности сохранены в `.local/qa/native-death-ragdoll/881dcacc-20260929T172817Z/` основного checkout.
- [Динамика четырёх оружий](player-1.5x/weapons-1.5x.gif), [контакты с окружением](player-1.5x/contacts.gif), [позы через 6 s](player-1.5x/weapon-settled-6s.png), [четыре камеры](player-1.5x/four-views.png). Это немодифицированные кадры Player с подписями и компоновкой. При сильном ракетном импульсе тело может уйти из исходного статичного кадра и продолжать кувыркаться к 6 s; для конечного кадра камера следует за ним.
- Измеренное смещение по направлению удара к 6.5 s: автомат 1.92 м, дробовик 4.66 м, ракета 9.29 м, резак 0.65 м. Diagnostic Tick с четырьмя corpse: mean 0.390 ms, max 1.478 ms. Это не FPS рендера и не замер на эталонном устройстве.

## Исходный кандидат

- Focused EditMode 23/23; full EditMode 203/203; full PlayMode 138/138; macOS build marker `STAR_PROVING_BUILD_PASS 471102531`; исходный native Player natural exit 0 ([отчёт](player/complete.json)).
- [Динамика оружия](player/weapons.gif), [позы через 3 s](player/final-poses.png), [четыре камеры](player/four-views.png). Этим кадрам соответствуют прежние значения профиля `unity-death-presentation-v1@1`.

## Приёмка

Падение со свободными руками и скрытым оружием на исходной записи пользователь одобрил с требованием увеличить все силы ×1.5. Изменённый Player ожидает отдельного просмотра и явной human acceptance. Scripted QA, включая четыре виртуальных viewport, не заменяет проверки физических устройств, TV и производительности на эталонном железе.
