# Релизные ревизии Lab

Реализация: `35c7191`; объединение с проверенными изменениями меню: `4549ee5`. Автоматическая проверка не является human/device acceptance.

Полный `UNITY_RUN_MODE=exclusive make check`: EditMode 351/351, PlayMode 211/211, 0 failed/skipped; development build exit 0. Естественные матчи восьми ботов FFA и Teams дошли до terminal. XML и hashes сохранены рядом. Focused history/catalogue + тяжёлые миграционные сценарии: 10/10, без изменения 180-секундного лимита.

Native development smoke (`-labReview`, muted, isolated history): сохранение, ветка от старой ревизии, локальная релизная отметка, ошибки, dirty guard, удаление локального профиля, защита встроенного, следующий матч, повторный запуск. После перезапуска выбран Default с исходным hash `c1c6487e1915ed13a021756f6ca3f35f728aeb9c89fa97fe63a4472701938fd4`; локальный QA профиль остался в истории.

В исходном Git-каталоге содержится только штатный Default v1. Персональная история и числовые правки пользователя не включены. Все будущие явно внесённые релизные ревизии сохраняют свои profile IDs/names; startup выбирает максимальный release sequence только среди Default.

Порядок выпуска: сохранить изменения в Lab → отметить сохранённые ревизии → явно перенести отмеченные snapshots командой `unity/tools.sh lab-stage-releases /absolute/history.json` → проверить diff каталога → commit → game gates → сборка и разрешённая публикация клиента. Локальная отметка сама ничего не публикует.

Release Player и окончательная проверка UI после merge записываются в `verification.json`. Публикация новой версии на GitHub/VPS в этой задаче не выполнялась. Физическая Windows/gamepad приёмка не заявляется.

QA catalogue smoke: `597f5ef`, four entries / three profiles, first run + restart passed. A separate regular release Player (`4549ee5`, BuildOptions.None) loaded main menu with Lab and startup/setup Default hash. CUA fullscreen interaction returned `noWindowsAvailable`; direct Lab interactions are proven by the isolated development Player using the same production Lab implementation. Physical/manual acceptance remains pending. The extra UI retry was cancelled; the runner had just started its owned Player, which exited with signal 15. It supplies no additional QA proof.

Retained release candidate: `/Users/eaafonasev/Projects/star-tournament-candidates/ship-lab-release-revisions/Star Tournament Unity Proving Ground.app`. The QA fixture Player will be removed after its screenshots and logs are preserved. The historical rename candidate (binary removed) was at `/Users/eaafonasev/Projects/star-tournament-candidates/rename-lab-default-profile/StarTournamentProvingGround.app` for its pending acceptance.

User accepted all Lab changes on 2026-10-07 in thread 01a115da-442a-79a0-964c-44a54f009b34. Earlier acceptance-pending notes describe the original QA state. The standalone rename binary is superseded and removed; its exact manifest/identity remains in `../rename-lab-default-profile/removed-candidate*.json*`. Retained accepted builds: the regular release candidate above and `../protect-shipped-lab-profiles/identity.json` (latest development Player). Public publication and physical platform tests are separate.
