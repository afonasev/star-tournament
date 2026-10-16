# Сверка с game-spec-templates

Target: `60fe3d2787dfb860722372791ee67dc8453f6f66`, catalogue0.6.0: menu-base0.2.0/settings0.1.4/design-lab0.2.0/updates0.2.1/installer0.1.0. Local/origin HEAD повторно проверены17.10.2026. Рабочая ветка `codex/game-standard-alignment`, baseline аудита `f6a7d3df`; последующий cursor patch связан source manifest в evidence/standard-menu-contracts. Это текущая полная матрица, **не заявление о завершении**.

Обозначения: **Код/QA** — inspected implementation и сохранённые affected проверки; **Разрыв** — подтверждённое невыполненное требование; **Проверить** — доказательств недостаточно; **Платформа** — source/portable fixtures есть, native installed/device acceptance не выполнена; **Макет** — ждёт решения до visual implementation. Наличие реализации/green fixture не выдаётся за установленную игру. Все pending пункты сохраняют исходный scope.

Пути evidence ниже относительно `docs/evidence/`. Исторические отчёты могут содержать уже исправленные pending пункты: используются самые поздние integration/transfer/refinement записи, а не их старые README-вступления. Подписи и новые controls20–28 не приняты автоматически по прежним макетам.

## Menu-base — 25 требований

| ID | Состояние и конкретное основание / незакрытая граница |
| --- | --- |
| MENU-STRUCTURE-01 | Код/QA: ProvingGroundMenus main six actions/brand/settings; NativeMenuPresentationTests, отдельный setup. |
| MENU-STRUCTURE-02 | Код/QA: Lab/profiles/history отдельными routes; network «скоро» недоступна, browser runtime не поставляется. |
| MENU-STRUCTURE-03 | Код/QA: network «скоро» disabled/no-op и исключена из focus; остальные guards в Settings/Lab/Update states. |
| MENU-ACTIONS-01 | МакетQ5: короткие Lab/reset/copy/AUTO20–25 ещё не реализованы. Полный audit standard-menu-actions/action-audit.md. |
| MENU-ACTIONS-02 | МакетQ5: duplicate/state operations20–25; особая greatest-marked Default route описана, не удаляется как обычный latest selector. |
| MENU-ACTIONS-03 | МакетQ5: boolean Off/On отдельные кнопки ещё нарушают single-toggle; release source flag уже исправлен без нового UI. |
| MENU-INPUT-01 | Код/QA: InputSystem/UI navigation, SettingsGamepadNavigationTests, NativeMenuPresentation keyboard/NativeTextEntry grid; physical devices pending. |
| MENU-INPUT-02 | Проверить/разрыв: text keyboard/reset умеют family glyphs, но generic Back содержит постоянный B. Нужна общая binding схема по позиции, не Xbox для всех. |
| MENU-INPUT-03 | Код/QA: top modal/field/seat ownership, input clear/frozen pause; NativeTextEntry/LabDiscard/IndependentPause tests. |
| MENU-INPUT-04 | Код/QA: opening frames/wasPressed/async Busy/UpdateSession operation guards; held submit and match drain проверены. |
| MENU-INPUT-05 | Код/QA: NativeTextEntry draft/grid/keyboard handover/reconnect/cancel, поля защищены от outer navigation; approved15. |
| MENU-FOCUS-01 | Код/QA: MenuPresentation distinct gold frame/initial safe focus; disabled rows excluded, modal cancel focus. |
| MENU-FOCUS-02 | Код/QA: Settings/Lab/history return targets/scroll and text modal; current cursor patch keeps focus. |
| MENU-FOCUS-03 | Код/QA: MenuScrollFocus reveal + history resize/long descriptions + settings split-view fixtures. |
| MENU-DEVICE-01 | Код/QA: real input/drift gating, shared focus, NativeTextEntry handover; standard-menu-contracts5 focused cases. |
| MENU-DEVICE-02 | Код/QA: новый window focus guard в menu и text-entry cursor owners;5 actual PlayMode cases, visible/unlocked background. Native focus event still device gate. |
| MENU-HINTS-01 | Разрыв: main «controls» и release-history-controls постоянно показывают keyboard/mouse/pad legend. Нужен approved conditional pad-only view. |
| MENU-HINTS-02 | Разрыв: general legend вместо явной привязки к действиям; focused bottom confirm и Back должны иметь device-aware glyphs. |
| MENU-HINTS-03 | Разрыв: fixed A/B/PgUp/Esc в history/Main; text-entry/reset partly correct. Правка нового визуального направления ждёт preview. |
| MENU-LAYOUT-01 | Код/QA частично: approved A/03–19, top Back и modal consistent внутри контекстов; Q5/Q6/Q7 меняют ещё не принятые состояния. |
| MENU-LAYOUT-02 | Код/QA: fixed screens + internal scrolls for fields/history/settings; headers/actions outside scroll. |
| MENU-LAYOUT-03 | Код/QA частично: saved actual1080p/720p/short/split captures; physicalTV/все устройства и новые20–28 pending. |
| MENU-STATE-01 | Код/QA частично: styled Settings/Update/Lab errors, file failures preserve original; author inaccessible-file routeQ7 ещё open. |
| MENU-STATE-02 | Код/QA: background Lab/discovery/history/download with Busy/drain/cancel; late completion does not steal focus. |
| MENU-PAUSE-01 | Код/QA: IndependentPauseMenuTests freeze one local match until last menu closes, per-seat settings ownership; online game unavailable. |

## Settings — 22 требования, включая controls-help.md

| ID | Состояние и основание / граница |
| --- | --- |
| SETTINGS-BASE-01 | Частично: наследует открытые MENU-HINTS/actions; focus/cursor/scroll QA сохранены. |
| SETTINGS-ENTRY-01 | Код/QA: main/seat/operator SettingsView, same options/current values, closing returns to pause; standard-settings/ и IndependentPause tests. |
| SETTINGS-CATALOG-01 | Проверить: полный разбор30 options ниже; quality applicability не объявлена N/A без проверки. |
| SETTINGS-GROUPS-01 | Код/QA: A screen/graphics/audio/input/interface; empty language/privacy groups не добавлены. |
| SETTINGS-SCOPE-01 | Код/QA: NativeUser/Audio/Visual/Gamepad prefs отдельно от Lab/match settings; artistic strength stays Lab. |
| SETTINGS-CONTROL-01 | Код/QA: ordinary sliders with numbers, Toggle controls/discrete lists; labels and single-state menu actionQ5 pending. |
| SETTINGS-CONTROL-02 | Код/QA: descriptors/keys/units/steps, release-default preference metadata; SaveStandard wrapper covers NumericSetting/BooleanSetting. |
| SETTINGS-CONTROL-03 | Код/QA: common display/audio/graphics; per-profile/seat look/FPS, shared views reread current values; scope hints inspected. |
| SETTINGS-PERSIST-01 | Код/QA: setters+PlayerPrefs.Save / atomic PlayerProfileCatalog; last values/reopen tests. Native registry/plist pending. |
| SETTINGS-PERSIST-02 | Код/QA: visual ordinary preferences immediate; risky display temporary, match/Lab separate. Не добавлены фиктивные restart-only options. |
| SETTINGS-PERSIST-03 | Код/QA: SaveStandard error area (active but not saved wording), validated legacy fallback/zero/false; actual storage failure native gate remains. |
| SETTINGS-DISPLAY-01 | Код/QA: temporary mode/resolution/Auto confirmation, timeout/cancel/focus-loss rollback; no immediate submit. Native fullscreen/monitor pending. |
| SETTINGS-AUDIO-01 | Код/QA: master×music/effects/voice, exact0 persistence; standard-audio-settings7 core+6 UI. Audible device test NotRun under -muted. |
| SETTINGS-AUDIO-02 | Код/QA: session -muted before audio creation, Unity -noaudio, launcher argv; docs commands. Installed native argv/no-output pending. |
| SETTINGS-RESET-01 | Код/QA: explicit allowlist+personal reset, identities/progress/history/Lab/consent retained; display confirmation separate, reset not section-local. |
| SETTINGS-RESET-02 | Код/QA: north shortcut scoped to settings/no modal/text/list/held-open bypass; family labels in same action. |
| SETTINGS-HELP-01 | Код/QA: approved13–14 separate gameplay-only art/aspect/device equivalents, real InputSystem mappings reviewed; physical legibility acceptance pending. |
| SETTINGS-HELP-02 | Код/QA: keyboard+mouse одна страница, gamepad отдельно, переключение/Back/внутренний scroll; NativeTextEntry help-page fixture. |
| SETTINGS-HELP-03 | Код/QA: saved generated PNG assets13–14, UI overlays provide exact captions; source assets retained. |
| SETTINGS-HELP-04 | Код/QA: gameplay actions only, corrected leader Q/E and device equivalents; no menu navigation inside art. |
| SETTINGS-HELP-05 | Код/QA: labels reviewed against SeatInputCoordinator, short/hold/combination semantics; no gameplay remapping feature. Physical controls pending. |
| SETTINGS-HELP-06 | Частично: actual ratio/NPOT/captions/split-view checked, generated art visually inspected; small viewport/TV/human legibility still pending. |

## Полный каталог Settings — 30 опций

Обычные глобальные предпочтения хранятся в PlayerPrefs; личные — в player-profiles-v2.json/guest/general settings по объявленной области SettingsView. Метаданные/границы — реальные descriptors в NativeUserPreferences/NativeAudioPreferences/NativeVisualPreferences/GamepadLookSettings/ProvingProfile, не произвольные числа из макетов.

| ID | Применимость и текущая реализация / причина |
| --- | --- |
| OPT-SCREEN-MODE | Применима: ProvingGroundGraphicsSettings temporary mode/confirm. |
| OPT-SCREEN-RESOLUTION | Применима: unique resolutions + Auto; native monitor fallback pending, AUTO copyQ5. |
| OPT-SCREEN-VSYNC | Применима: VSyncKey/ApplyVSyncPreference, one shared runtime value. |
| OPT-SCREEN-FPS-LIMIT | Применима: NativeUserPreferences.FrameCaps/targetFrameRate; VSync priority explained. |
| OPT-GRAPHICS-QUALITY | **Проверить**: шесть Unity quality slots существуют; global URP и runtime per-effect overrides. Нельзя объявить отсутствие meaningful tiers только из-за отсутствия кнопки. Нужно проверить реальные отличия и после этого добавить совместимый selector либо обосновать N/A. |
| OPT-GRAPHICS-AA | Применима: NativeVisualPreferences AntialiasingLevel, per-camera/runtime resolve. |
| OPT-GRAPHICS-SHADOWS | Применима: global toggle, owned Light originals retained, quality state restores. |
| OPT-GRAPHICS-EFFECTS | Применима: каждый используемый VisualEffect имеет Enabled; strength stays Lab, no fake channels. |
| OPT-AUDIO-MASTER | Применима: MasterKey,0–100%, master multiplier. |
| OPT-AUDIO-MUSIC | Применима: MusicPercent/master mix/menu+match music. |
| OPT-AUDIO-EFFECTS | Применима: EffectsPercent/master mix actual effects. |
| OPT-AUDIO-VOICE | Применима: VoiceKey/bot voice; legacy effects fallback only before explicit voice choice. |
| OPT-AUDIO-EXTRA | Неприменима: самостоятельного доп. пользовательского канала сверх music/effects/voice нет; spatial submix не объявлен новым каналом. |
| OPT-INPUT-MOUSE | Применима: MouseSensitivityPreference + per identity, selected Lab default when personal missing. |
| OPT-INPUT-STICK | Применима: horizontal/vertical GamepadLookSettings per identity/seat/general. |
| OPT-INPUT-INVERT | Применима: look X/Y, movement unaffected. |
| OPT-INPUT-DEADZONE | Применима: declared descriptor and personal look resolver. |
| OPT-INPUT-VIBRATION | Неприменима: runtime не реализует haptic feedback/rumble; добавление механики шаблон не требует. |
| OPT-INPUT-CURSOR | Неприменима: gamepad menu focus is discrete; gameplay uses camera look, отдельного аналогового игрового cursor нет. |
| OPT-INPUT-CAMERA | Частично применима: analog look covered mouse/stick; edge/keyboard independent camera pan отсутствует. |
| OPT-INPUT-ASSIST | Применима: существующее auto-level + return delay, без придуманного aim assist. |
| OPT-UI-SCALE | Применима: ScaleDescriptor75–150/5, NativeInterfacePreferences scaler. |
| OPT-UI-CONTRAST | Применима: HighContrast/outline preserving team colors. |
| OPT-UI-SUBTITLES | Применима к существующей речи: TextEnabled bot reaction lines display exact utterance; нет сюжетных диалогов. |
| OPT-UI-FPS | Применима: personal/general FPS display, shared global counter semantics. |
| OPT-UI-HEALTH | Неприменима как выбор режима: health HUD — обязательная combat readability, варианты скрытия/режима проектом не предусмотрены; не добавляем новую gameplay visibility policy. |
| OPT-UI-PING | Неприменима: сетевой режим недоступен/не реализован, измеряемого сетевого RTT нет. |
| OPT-LANGUAGE | Неприменима: UI локализован только по-русски; bilingual release body не означает две локализации меню. |
| OPT-DATA-STATISTICS | Неприменима: необязательной отправки игровой статистики нет; ProvingTelemetry только локальный explicit QA-file, GitHub update HTTP не telemetry. |
| OPT-DATA-CLEAR | Неприменима: user-facing очистки истории/данных нет; удаление отдельного профиля принадлежит profile workflow с подтверждением, reset не удаляет данные. |

## Updates — 18 требований

| ID | Состояние и основание / граница |
| --- | --- |
| UPDATES-SOURCE-01 | **Разрыв**: test Discover API, production Discover сразу ProductionBase/latest/download. Signed asset admission есть, но API discovery для production отсутствует. |
| UPDATES-SELECT-01 | Частично: signed identity/version/platform/channel/full package, semantic versions/test drafts filtering; production API shortcut и отсутствующее explicit minimum OS/updater/data compatibility admission требуют исправления/обоснованного contract. |
| UPDATES-CHECK-01 | Код/QA: UpdateSession.Start once/started guard, shared IPC result, no manual check/retry. |
| UPDATES-CHECK-02 | **Разрыв**: discovery ReadBounded без ETag/cache/backoff; ReleaseHistory cache отдельно не закрывает этот контракт. Offline check error unavailable and game remains playable verified. |
| UPDATES-CONSENT-01 | Код/QA: download only after update action, no forced timer/settings/channel control, stage waits normal exit/restart. |
| UPDATES-FLOW-01 | Код/QA: UpdateSession.Command match drains download/history before ack; NativeUpdateMenu QueueMatch/return/resume, ordinary quit/discard protection. |
| UPDATES-UI-01 | Код/QA: actual available/downloading/verifying/ready/error/current panels; active version separate from candidate/ready, history17 highlight. Generic glyph/legend gap inherited. |
| UPDATES-DOWNLOAD-01 | Код/QA: temp package, signed size/hash/free-space/admitted target, retry same candidate/cancel/no duplicate operation; source tests standard-update-download-flow. |
| UPDATES-HISTORY-01 | Код/QA: ReleaseHistory ten recent+exact installed tag, RU/EN bodies/current RU, separate cached HTTP/drain; installed permanent marker/scroll/resize verified. |
| UPDATES-CHANGELOG-01 | Код/QA: Git-derived paired reviewed notes/generator before packaging; standard-release-changelog20+16 targeted Python tests. Actual next release notes not generated without publication command. |
| UPDATES-TRUST-01 | Код/QA: RSA-PSS admission, pinned identity/channel/asset/version/size/hash, HTTPS bounded redirects, activation reverification; native chain pending. |
| UPDATES-INSTALL-01 | Платформа: payload snapshots/durable journal/root transaction/restore portable fixtures, Windows registry callbacks compiled; installed crash/UAC/ACL not executed. |
| UPDATES-INSTALL-02 | Платформа: normal quit/restart guards, Mac stable supervisor/Windows root entry; actual paired launcher/Player/shortcuts/rights native acceptance pending. |
| UPDATES-DATA-01 | Платформа: versioned user data preserves actual v0.1.2 namespaces and failed-write paths; installed previous→candidate→failed startup→previous not executed. |
| UPDATES-RECOVERY-01 | Платформа: payload/health/journal/observer/backup transfer source wired; exact Player startup/restore on native installations pending. |
| UPDATES-ARGS-01 | Платформа: source forwarding and -muted/-noaudio contracts, stable entry/supervisor arguments; real native restart acceptance pending. |
| UPDATES-RELEASE-01 | Код/QA + публикация отдельно: complete signed release metadata/assets/immutable source custody and exact body readback; no release generated/published in this work. |
| UPDATES-QA-01 | Политика соблюдена: affected/reused/native NotRun separate; no full/Player/build/deploy inferred from standard. |

## Installer — 17 требований

Current delivery: macOS Apple Silicon `.pkg/.app`, Windows x64 system installation/NSIS. **Разрыв с platforms.md: требуется DMG с переносом в Applications, а package.py/publisher требуют PKG.** Новый DMG workflow сначала требует saved preview; упаковка/публикация отдельно. Intel Mac/universal compilation не является заявлением acceptance для Intel. Browser installer не поставляется. Test unsigned delivery explicitly documented; signed update trust remains mandatory.

| ID | Состояние и основание / граница |
| --- | --- |
| INSTALLER-PACKAGE-01 | Платформа: packaging scripts/release filenames/dependency inclusion exist; current modified Player+installer not built, earlier releases not proof this source. |
| INSTALLER-IDENTITY-01 | Код/QA + платформа: shared icon inputs/app+installer+shortcut routing, windows-installer-icon evidence; current actual installed shell icon pending. |
| INSTALLER-FLOW-01 | **Разрыв macOS**: текущий Velopack PKG, не требуемый DMG drag-to-Applications. Windows approved07 normal NSIS wizard/standard locations/repair source; native rendering pending. |
| INSTALLER-FLOW-02 | **Разрыв OS constraints**: x64/admin/space есть, но .onInit не проверяет minimum Windows version; signed manifest не содержит minimum OS, фактический macOSTargetOSVersion12.0 не отражён в release policy. Нужны объявленные min OS + preflight/admission, затем native QA. |
| INSTALLER-RIGHTS-01 | Платформа: ordinary root supervisor/Explorer finish launch, only protected broker elevated; source guards/compiled tests, real tokens/ACL pending. |
| INSTALLER-TRUST-01 | Explicit dev/test unsigned exception documented; no signing/notarization acceptance claimed. Update envelopes remain authenticated. |
| INSTALLER-CONTENT-01 | Платформа: wrapper/stable entry→launcher→Player and shared runtime packaged; current content completeness requires separately authorized build/readback. |
| INSTALLER-CONTENT-02 | Код/QA: LauncherConfig/version/channel/platform/architecture/protocol+product registration/owned markers; current installed readback pending. |
| INSTALLER-LAUNCH-01 | Платформа: default shell shortcuts/wrapper/offline code/optional finish launch; physical fresh/repeat launch not executed for current source. |
| INSTALLER-DATA-01 | Код/QA + платформа: VersionedUserData paths/import/write failure preserve old namespaces, app payload separate; actual native data persistence pending. |
| INSTALLER-REINSTALL-01 | Платформа: fresh/repair/upgrade exact version inspection+staged whole root+registration snapshots; portable transaction/transfer passes, actual NSIS pending. |
| INSTALLER-REINSTALL-02 | Платформа: leases/busy processes/channel identity/downgrade refusal and interrupted custody; cross-user/legacy binary concurrency native pending. |
| INSTALLER-RECOVERY-01 | Платформа: stage rollback/durable states/retained fallback/bootstrap transfer, exact owned cleanup; native power-loss/cancel/registration not executed. |
| INSTALLER-REMOVE-01 | Платформа: fixed product uninstall root/paths/records, user data retained; actual Windows Settings/uninstall/UAC pending. |
| INSTALLER-ARGS-01 | Платформа: wrapper/stable argv and mute contract documented; actual newly installed chain not executed. |
| INSTALLER-QA-01 | Политика соблюдена: transfer173 targeted checks, latest unchanged source hashes reused; native gate explicitly NotRun. |
| INSTALLER-RELEASE-01 | Политика соблюдена: no full regression imposed, no Player/installer/release build or publication without separate command. |

## Lab — 28 требований

Все IDs отдельно разобраны в [матрице Lab](evidence/standard-design-lab/requirement-matrix.md). Effective latest source: f38103cc; source-preserving migrations/flags/package ordering/structural mappings verified. Q5 UI20–25, Q6 frozen match identity26–27, Q7 author error fallback28 остаются открытыми; native Development -labProject отдельный gate. Никакие old snapshots/user balance/real author JSON не переписаны этой сверкой.

## Дополняющие нормативные документы

- `settings/controls-help.md`: ещё5 SETTINGS-HELP IDs включены выше, не потеряны за root requirements.
- `settings/options.md`: все30 option IDs перечислены. Quality остаётся OPEN, не удобным N/A.
- `design-lab/parameter-catalog.md`:9 metadata columns, grouping/search/diff/consumer boundary сопоставлены с actual638 registry/inventory и Lab matrix; примеры новых полей не являются требованиями их добавить.
- `installer/platforms.md`: реальные PKG≠DMG и отсутствующая явная min OS policy открыты. Windows x64 check не подменяет OS-version check.
- `updates/release-format.md`: signature/identity/version/channel/asset/size/hash/body code есть. Metadata schema1 как updater gate недостаточно описана для minimum OS/component/data compatibility: это OPEN до source contract/negative fixtures; не объявлено несовместимое обновление безопасным по наличию одной подписи.

## Следующее действие

1. Исправить bounded GitHub API discovery обоих каналов с ETag/cache/rate-limit handling; one session check, signed authority, same-candidate download сохраняются. Требуется targeted HTTP/.NET fixtures, не Player.
2. Уточнить min OS/component/data compatibility, устранить Windows OS preflight и Mac PKG/DMG разрыв с отдельным preview. Завершить source audit effective quality slots before deciding OPT-GRAPHICS-QUALITY; не объявлять N/A из удобства и не придумывать пресеты молча.
3. После human decisions внедрить20–28 и общий device-aware hints direction; новые визуальные изменения сначала saved preview.
4. После source fixes — affected integration, canonical spec sync и итоговый отчёт. Native build/platform/device/human gates остаются отдельными и не выдаются за успешные.
