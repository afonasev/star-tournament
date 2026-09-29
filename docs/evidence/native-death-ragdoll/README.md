# Native death ragdoll

Изменение: `add-weapon-driven-death-ragdoll`. Ниже записан **исходный** кандидат `262518ea2ec8ac6e81f76e40ae1bc82d0e535810`. После просмотра пользователь попросил усилить все четыре импульса ×1.5; новый source `2262f6d` с профилем `unity-death-presentation-v1@2` прошёл affected EditMode 27/27 (XML `editmode-1.5x.xml`) и PlayMode 20/20 (XML `playmode-1.5x.xml`), но ещё ожидает новой сборки. Не приписывать старые кадры новым числам.

Passive ragdoll создаётся из evaluated позы и скорости, получает однократный weapon-driven импульс, контактирует с копией authored окружения в отдельной physics scene и следует session clock. Оружие corpse скрыто; двуручный grip и animation writers выключены. Настройки исходного кандидата: `unity-death-presentation-v1@1`.

## Проверки

- Исходный focused EditMode: 23/23, exit 0 — fatal attribution, сила/спад импульса и сохранение старых Lab ревизий.
- Full EditMode: 203/203, exit 0, 313.23 s.
- Full PlayMode: 138/138, exit 0, 680.9135928 s.
- macOS build: exit 0, `STAR_PROVING_BUILD_PASS 471102531`.
- Исходный immutable native Player: natural exit 0, 52.4 s; `player/complete.json`. Все четыре real fatal paths, воздушная смерть, support contacts, pause, respawn, Repeat и четыре viewport прошли scripted guards.
- Просмотрены последовательности оружия и финальные позы на t=3 s: руки свободны, оружие скрыто, импульсы различимы. Для ракеты это промежуточная поза.
- Диагностический Tick с четырьмя corpse: mean 0.391 ms, max 1.700 ms; это не общий FPS и не reference-hardware acceptance.
- Ракурсы ступеней/края перекрыты окружением; camera fixture исправлен, требуется финальная пересъёмка и affected checks объединённого source. Исходные кадры сохранены без ретуши.
- [Последовательности оружия](player/weapons.gif), [позы на t=3 s](player/final-poses.png), [четыре killcam](player/four-views.png).

QA state: arena `combat-bowl-v1@16`, frozen design profile `38aeaf3191e89e7d71c5822ea637f82a6c5ad1ce3eb3efedf3541c7bfa67ec75`; muted synthetic-input diagnostic. Fresh empty QA history. Кадры в GIF идут с интервалом 0.1 s по session clock; последний кадр удерживается для просмотра.

## Приёмка

Automated/native evidence не устанавливает human acceptance. Падение и свободные руки на исходной записи пользователь одобрил с требованием увеличить все силы ×1.5. Изменённый Player ещё не просмотрен; техническая проверка и приёмка точного нового результата остаются открытыми. Четыре физических устройства, TV и performance reference hardware не проверены этим automated review.
