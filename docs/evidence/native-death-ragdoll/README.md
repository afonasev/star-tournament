# Native death ragdoll

Изменение: `add-weapon-driven-death-ragdoll`. Runtime revision: `262518ea2ec8ac6e81f76e40ae1bc82d0e535810`.

Passive ragdoll создаётся из evaluated позы и скорости, получает однократный weapon-driven импульс, контактирует с копией authored окружения в отдельной physics scene и следует session clock. Оружие corpse скрыто; двуручный grip и animation writers выключены. Настройки: `unity-death-presentation-v1@1`.

## Проверки

- Focused EditMode: 23/23, exit 0 — fatal attribution, сила/спад импульса и сохранение старых Lab ревизий.
- Full EditMode: 203/203, exit 0, 313.23 s.
- Full PlayMode: 138/138, exit 0, 680.9135928 s.
- macOS build: exit 0, `STAR_PROVING_BUILD_PASS 471102531`.
- Native Player review: ждёт окна на общем QA-хосте; результат будет записан после просмотра кадров.

## Приёмка

Automated/native evidence не устанавливает human acceptance. Оценка плавности, свободной позы рук и читаемости силы оружия остаётся за пользователем на точном локальном Player candidate. Четыре физических устройства, TV и performance reference hardware не проверены этим automated review.
