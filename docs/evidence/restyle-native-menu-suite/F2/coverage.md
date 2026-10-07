# F2: требования и доказательства

Exact source и окончательные результаты — `verification.json`; изображения — `gallery.html`. Скриншоты native Player и синтетические устройства не являются человеческой приёмкой.

| Требование | Автоматическая проверка / кадр |
|---|---|
| Список арен слева, превью и сведения справа | menu/05-map.png, 05b-tunnels.png, 05c-lunar.png |
| Нет боковой сводки и дублирования перед запуском | menu/05-map.png, 06-rules.png, 07-players.png |
| Карта и режим во вкладках | menu/06b-rules-teams.png, 07-players.png |
| Один активный режим, отдельные значения и −/+ | menu/06-rules.png, 06b-rules-teams.png |
| Обязательные цели 2000/3500 и наследование сложности | NativeMenuPresentationTests.SetupUsesMandatoryModeTargetsAndInheritedBotDifficulty |
| Y присоединяет/меняет собственную команду; X добавляет бота | RosterCardsTests.RosterYJoinsThenSwitchesOwnTeamAndXAddsInheritedBot |
| Фокус новой карточки, запуск/пауза геймпадом | FourPlayerMenuTests.FourGamepadsCanJoinStartPauseAndResumeWithoutMouse |
| Стрелки/Enter после возврата из настроек | NativeMenuPresentationTests.KeyboardNavigatesMainMenuAndSubmitsAfterReturningFromSettings |
| Галочка собственного экрана и inline сложность | RosterCardsTests.BotViewRoundtripPreservesRosterDifficultyTeamAndFocus; menu/10-bot-editor.png, 11-difficulty.png |
| Профили без параметров, компактные действия | menu/03-profile-detail.png |
| Крупнее кнопки главного меню | NativeMenuPresentationTests.ApprovedMainMenuHasReadableArtworkAndSixPreservedActions; menu/01-main.png |
| Растровая справка, целое изображение в панели | menu/15-help-keyboard.png, 16-help-gamepad.png; FeedbackUsesTopBackSquareChecksAndFullResolutionList |
| Три строки Lab и сохранённая защита shipped-профилей | menu/20-lab.png; DesignLabWorkspaceTests; lab/01-editor-base.png |
| Одинаковая пауза оператора и участника | pause1/13-match-back-1.png, 14-operator-pause.png, 17-operator-return-pause.png |
| Один FPS на весь экран | pause4/01-running-4.png и соответствующий JSON visibleFps=1 |
| Настройки, rollback разрешения, музыка/звук | setup/complete.txt; setup/05-resolution-picker.png, 07a-display-timeout-reverted.png |
| Матч и AI не сломаны | NativeBotBehaviorTests.NaturalDefaultEightFfaReachesTerminalWithoutInvalidRuntimeState; NaturalDefaultEightTeamsReachesTerminalWithoutInvalidRuntimeState |

## История итераций

- Первый full PlayMode: 210/216, журнал и XML сохранены отдельно. Ошибки устранены без отключения тестов.
- Focused 19 тестов: 17 passed, выявлены прежнее ожидание фокуса карточки и старый текст P2 в заголовке. После исправления отдельный повтор FourPlayerMenuTests/NativeSeatLayoutTests прошёл.
- Native ввод проверяется только при реальном фокусе окна. Последний локальный кандидат имеет отдельный QA bundle ID, чтобы macOS не выбирала одну из сохранённых прежних сборок.
- Финальные XML содержат полный повтор на интегрированном source; точная привязка EditMode/PlayMode/UI/native хранится в verification.json. Неуспешные прогоны сохраняются отдельно и не используются вместо финального gate.
