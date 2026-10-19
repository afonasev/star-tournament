# Аудит границ совместимости Lab

Стандарт: design-lab0.2.0, pin0c2d4562683252d54736f294dc1af463050eac17. Это inventory правил и незакрытых проверок, не сертификат полного LAB-UPGRADE.

| Случай | Реальный механизм | Доказательство / граница |
|---|---|---|
| Новые поля | CompatibilityDefaults + UpgradeSnapshot | Последний package/project Default,51 affected cases (upgrade-defaults/);0/false и исходные снимки проверены. |
| Повторные обновления своих non-package/non-Default версий | Source refs + Schema3 envelope | repeated-upgrade/:75 unique EditMode +2 PlayMode, complete save vs derived view и latest saved selection; exact160MB input. |
| Пакетный Default | SelectLatestDefault | Максимальный объявленный Revision, не Sequence; исходные saved number/date/hash сохранены проекцией и UI. Исторические интерпретации и их labels/права требуют полного05 audit. |
| Авторский Default | AuthorDefault/ReleaseError | Максимальная отметка проекта; Current-совместимость проверяется, отсутствие/несовместимость блокирует release, Lab остаётся редактируемой в проверенных состояниях. Снятие исторической отметки в UI ещё не закрыто. |
| Неизвестная схема / смена schema epoch | StorageRegistry / ReadStorage | Только доверенные code-generated исторические schemas. Unknown version999 отвергается с ID@version; это не исчерпывающая проверка всех типов/units. |
| Новое более узкое ограничение | ValidateFile trusted.Validate | Сохранённое значение за новым пределом отвергается с profile/revision/path/reason, без clamp или записи в проверенном сценарии. |
| Неизвестное/удалённое поле | HistoricalPredecessors exact registry | Только явные известные predecessor shapes; неизвестное поле отклоняется, имя указано в причине. |
| Недоступное определение authored ресурса | ResolveStorageAuthored | ID/version/definition hash сверяются с trusted code. Причина называет ресурс; его geometry не принимается из файла. |
| Невалидная история в runtime | LoadDesignLabHistory / SetLabHistoryWaiting | Published Default остаётся доступным; Lab запись блокируется, Back доступен. Это не устройство/Player приёмка. |

Существующие явные legacy mappings, которые этот срез не меняет: SafeSwitchUpgrade (equipment fill/default gesture/safe bounds); старый Default bot movement; v9/v10 footstep interval; blood intensity v1; UpgradeArenaVisualValue и UpgradeWellDetailValue; office-to-stairs lighting v3; torso-to-unified damage; shared-to-two-axis gamepad look. Исходные значения и hash сохраняются в исторических строках; current counterparts используют эти правила. Полную проверку соответствия каждого преобразования LAB-UPGRADE-01/02, включая смену типов/ограничений и недоступность отдельных saved versions, нельзя заменить фактом существования функций.

Остаётся: complete Default/package policy audit; typed catalog/editors (boolean сейчас numeric, корректность type/step требует отдельной проверки); исторические release flags и доступная причина/действие в списке версий; source/execution identity и05 viewport/navigation. Результаты новой diagnostic QA сохраняются в verification.json рядом.
