# F2 — переработка меню и поведения подготовки

Запрос: список арен и подробности без боковой сводки; выбранные карта/режим во вкладках; компактные профили без параметров; отдельные значения и степперы; обязательные цели 2000/3500 с шагом100; Y вход/команда, X бот с последней изменённой сложностью; inline списки и галочка экрана бота; растровая справка; три строки Lab; единая пауза и более крупные главные кнопки.

QA scope full: make check, natural bot matches, affected UI/input contracts, muted native screenshots. Проверки и человеческая приёмка пока не завершены.

Растровые ассеты созданы встроенным image_gen: `unity/Assets/StarTournament/Resources/UI/help-keyboard.png`, `help-gamepad.png`. Палитра navy/gold, реалистичные устройства, русские подписи и стрелки. Исходные generated PNG сохранены генератором; рабочие копии находятся в Resources. Текст основан на существующих привязках и новом Y/X поведении.

Prompt keyboard: infographic-diagram, premium Russian game control reference, landscape1536x1024, navy #142130/gold #e7bb72; realistic keyboard/mouse, gold Esc/Tab/WASD/1–4/Q/E/Space/LMB/wheel; labels movement, jump, pause/back, held scoreboard, weapon selection/cycling, fire/look; menu arrows/Enter/Esc.
Prompt gamepad: infographic-diagram, landscape1536x1024, realistic Xbox-layout controller, navy/gold, exact sticks/D-pad/ABXY/View/Menu/LB/RB/RT callouts for existing bindings; footer menu selection/confirm/back and roster Y join/team, X add bot.

Старые immutable Lab snapshots сохраняются. Новые параметры `match.ffaTargetDefault`/`match.teamTargetDefault` имеют metadata и совместимость с историческим реестром.
