# Аудит действий и предложения20–25

Baseline: codex/game-standard-alignment@7830e481. game-spec-templates@60fe3d2787dfb860722372791ee67dc8453f6f66, catalogue0.6.0/menu-base0.2.0, MENU-ACTIONS-01/02/03. Это source review и проверенные raster-макеты, не готовность runtime/focus/device.

| Экран / source | Вывод | Предлагаемое изменение / доказательство |
| --- | --- | --- |
| Main, ProvingGroundMenus.CreateModernMenuUi | Действия различаются: бой, сеть, Lab, профили, настройки, выход. Причина disabled сети указана | Нового визуального изменения для этих подписей не требуется по source review; фактическая полная acceptance отдельно |
| Lab boolean, BuildLabField | Две кнопки bool-off/bool-on задают один обратимый boolean |20: один checkbox со shape/check и текстом текущего состояния;0/1 и draft-only contract прежние |
| Lab release toolbar | Уже один toggle action, но длинная/меняющаяся подпись и состояние только в тексте |20: «В релиз» и checkbox; while dirty недоступен с видимой причиной; не переносить отметку на unsaved draft |
| Lab version inspector | Отдельный unmark-only button ограничивает нормальную симметрию признака |21–23: одна «В релиз» отметка. Compatible позволяет mark/unmark, incompatible marked позволяет только снять, после снятия — unchecked disabled с причиной. Inspected profile отличается от selected, draft/active guard прежний |
| Lab per-field reset | «Сброс» возвращает selected baseline, а focus hint преимущественно показывает release Default; объект неоднозначен |20: «Вернуть»; объяснить значение выбранной сохранённой версии при фокусе. Не подменять baseline релизным Default |
| Lab entire draft reset | Отдельная операция для всех правок, отличная от поиска/базы |20: «Сбросить правки», прежний06 guard. Не объединять с очисткой поиска |
| Lab reset-to-release | Не дубль profile chooser: chooser берёт latest saved, включая unmarked; reset выбирает greatest marked Default |20: «Версия по умолчанию», рядом actual Default display name / latest release role. Сохранить действие и discard guard, не выбирать по совпадению имени |
| Lab all changes / jump | Навигация к global diff или полю, а не boolean gameplay flag | «Все изменения» сохранить. «К полю» уточнить как «Открыть поле» при реализации20, прежний route без смены данных |
| Lab create | Same operation clones selected saved snapshot; подпись отличается для local/package, title технический и длинный |20/25: всегда «Создать копию»; «Новый профиль» и источник сохранённой версии в body, submit «Создать». Unsaved draft не копируется; прежний guard06 сохраняется |
| Settings A, StandardSettings.ChooseAutoResolution | AUTO — не дубль списка пиксельных разрешений: сохраняет Auto-mode, список задаёт manual-mode |24: «Автоматически» и пояснение «Автоподбор по текущему экрану». Не удалять route и не менять подтверждение/откат |
| Settings boolean controls | BooleanSetting уже один Toggle с check shape/name | Сохраняется,24 показывает существующий checkbox VSync. Новых пар Вкл/Выкл не добавлять |
| Settings reset / display confirmation / help/device | Объекты/действия различимы контекстом; «Сбросить все настройки» не дубль per-field или Lab reset | Source review, прежние confirmation и navigation. Новые макеты этих неизменяемых процессов не требуются |
| Release history / updater | Одна «Что нового» открывает историю, другая update-action скачивает/отменяет/перезапускает по состоянию | ProvingGroundReleaseHistory/NativeUpdateBridge; разные операции, не удалять update/cancel/history как duplicates |
| Profiles/setup/pause | Create/rename/delete/personal settings, Next/Back, Continue/Restart/Exit to menu — разные действия | Не сокращать «Выйти в главное меню» до неоднозначного «Выход»; стандарт не требует лимита слов |
| Text entry | RU/EN и А/а — по одной кнопке каждого состояния; apply/cancel/delete/caret разные действия; язык/регистр видны на клавишах | ProvingGroundTextEntry.BuildTextKeys; новых visual changes не требуется по source review |
| Windows repair07 | Контекст same-version переключает primary label на «Восстановить», newer — «Обновить»; explaining text рядом | distribution/windows/installer.nsi lines179–196. Source review, native Windows acceptance остаётся отдельно |

## Сохранённые предложения

20 workspace,21 compatible release toggle,22 incompatible marked,23 incompatible unmarked,24 screen AUTO copy,25 copy-profile dialog. SVG+PNG и gallery docs/design/template-standard/index.html. Watermark «МАКЕТ · НЕ UNITY» и подпись «Предложение» — аннотации проекта, не строки будущего runtime UI. Числа/версии демонстрационные; баланс/схемы/сохранения/Unity source не менялись.

Проверено: rsvg-convert всех6 SVG; каждый PNG просмотрен; узкий отступ «Вернуть» скорректирован до согласования. XML/SVG и Python AST parsed, повторная генерация byte-identical, ссылки gallery проверяются отдельно; старые approved01–19 PNG/SVG byte-equal с baseline. Runtime/game/full/Player/build/deploy не запускались: изменения только design/doc artifacts. Approval20–25 pending; реализация изменённого visual UI до ответа запрещена прямым требованием пользователя «где меняешь дизайн предлагай сначала макеты».

## Согласование и реализация17.10.2026
Q5/20–25 согласован прямым «согласовано», planning main95efe1e. Прежнее pending выше — исторический статус предложения. Реализованы единые checkbox buttons boolean/release, compatible inspected mark/unmark и incompatible unmark-only, понятные reset/copy/Default actions, saved source в диалоге копии и Auto copy.9 unique affected PlayMode passed (8 initial +4 after local typography/copy-name/rename corrections,3 repeats). XML/log/source hashes/actual PNGs/cleanup: implementation/. Current state/Off/On не только цвет, A toggle twice/right-to-reset checked; snapshots/hash/date/base/draft/active retained, dirty discard and saved-only independent unmarked copy verified. Main/profiles/pause/updates/Windows repair/text entry source-reviewed unchanged actions remain distinct as table above; actual native/device/human acceptance not replaced. Fixed B/Верх glyphs belong to approved Q8 and remain open. No full/Player/installer/build/deploy.
