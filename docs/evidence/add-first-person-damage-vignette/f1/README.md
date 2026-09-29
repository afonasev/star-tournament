# First-person damage vignette · F1

Поведение: синий импульс при полном поглощении бронёй; красный при любой потере здоровья, включая пробитие брони. Мягкий градиент по краям собственного viewport, чистый центр, HUD поверх эффекта. Параметры находятся в Balance Lab → Оформление → Получение урона (`unity-proving-ground-v1@8`).

Native scripted QA использует synthetic gamepads и управляемые combat fixtures. Это проверка рендера и контрактов Player, отдельная от human visual acceptance. Звуковой вывод выключен. State JSON рядом с каждым PNG фиксирует combat state, profile hash и arena identity.

Команды автоматических gates: `unity/tools.sh test-edit`, `unity/tools.sh test-play`, `unity/tools.sh build`.

Player diagnostic: `-damageVignetteReview ABSOLUTE_EVIDENCE_DIRECTORY`, из immutable macOS candidate с отдельными QA profile/history файлами. Сценарии: чистый экран, броня, пробитие брони, серия, затухание, 2/3/4 viewport, pause по session clock, restore, killcam/respawn, Repeat и Menu. Ни один сценарий не подтверждает физическое управление геймпадом.

Идентичность итогового кандидата и результаты gates фиксируются в `verification.json` после завершения проверки. Human acceptance записывается в shared planning change по точному кандидату.

F1 завершён: EditMode 203/203, PlayMode 137/137, build PASS, native muted Player exit 0; 31 сценарная проверка, 8 PNG 1920×1080. Просмотрены реальные кадры: гладкие синие/красные края, чистый центр и читаемый HUD; независимость 1–4 viewport. Кандидат dfa39ef, идентичность и хеш проверены в verification.json. Human acceptance остаётся pending.
