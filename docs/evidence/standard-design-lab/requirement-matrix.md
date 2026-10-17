# Lab: матрица требований 0.2.0

Проверяемый pin game-spec-templates60fe3d2787dfb860722372791ee67dc8453f6f66 (local и origin HEAD проверены17.10.2026). Полная цель включает ещё четыре пакета. Эта матрица не закрывает parent/change и не подменяет native/human acceptance. Source до текущих test-only дополнений: codex/game-standard-alignment@f38103cc. Ранее проверенные контракты учитываются по неизменённым областям и более новым affected QA, а не автоматически по наличию отчёта.

Evidence paths ниже относительно этого каталога. «Техническое основание» включает inspected source + перечисленные executed assertions, не означает проверку всех устройств/всех runtime веток. Общие MENU-ACTIONS-01/02/03 ждут Q5, поэтому общий UX пока не завершён. Native Development Player -labProject остаётся отдельным непроверенным gate без команды на сборку.

| ID | Техническое основание и текущая граница |
| --- | --- |
| LAB-SCOPE-01 | Main Lab route/OpenDesignLab, отдельные settings/setup; runtime/ и approved-ui/ — actual screen and back. |
| LAB-SCOPE-02 | LabBundle.IsVisible/IsEditable,638 visible/14974 excluded, catalogue/actual-catalogue.json, consumers/ и workspace hidden-path/save guards. |
| LAB-GROUPS-01 | ResetLabDraft/RefreshLabWorkspace единый registry→groups/search/diff; approved-ui actual rendered tree. |
| LAB-CATALOG-01 | catalogue/: все actual visible metadata валидны, numeric155integer/481decimal/2boolean, defaults source/NextGame checked. В editable catalogue нет text/enum/resource selectors, их domain widgets не требуются. |
| LAB-CATALOG-02 | consumers/:638 source bindings, actual selected→frozen lifecycle; hud-lifecycle/ устранил найденные startup-only UI caches. Это source+targeted lifecycle proof, не638 executed effects. |
| LAB-FIND-01 | DesignLabWorkspaceTests search hidden paths/global changes; LabDiscardWarningTests hidden changes. Reset via clear search; new short copy Q5 pending. |
| LAB-EDIT-01 | Workspace partial raw input/focus/step/save; typed-values/ boolean guards; approved-ui18. Новый single-toggle вид Q5 pending. |
| LAB-DRAFT-01 | Workspace draft vs saved/live, per-field diff, exact baseline reset; discard/ and approved-ui/ checked. |
| LAB-DRAFT-02 | discard/: hidden/raw/long diff, cancel focus, guard leave/profile/revision/reset/create/delete/quit; approved06 UI. |
| LAB-VALIDATE-01 | LabParameterTypeTests trusted metadata/0or1/NaN/Infinity, History range/step/relations, hud-lifecycle kill bounds; invalid draft not published. |
| LAB-PROFILES-01 | History.Create новый ID/one unmarked base; LabReleaseCatalogTests readonly source→independent copy/restart; package-authority retains archive. |
| LAB-PROFILES-02 | authoring-model/runtime default rename/protected delete; discard delete guard; source deletion reset actual authority. |
| LAB-VERSIONS-01 | public-order/default-source exact source number/hash/date, Save max(public/physical)+1, full independent save and reload; no date invention. |
| LAB-VERSIONS-02 | public-order source/current ordering; package-authority actual UI own→Default ignores legacy9, explicit old selection remains available. |
| LAB-DEV-01 | authoring-model/runtime writable author Default/all profiles, rename/copy/new immutable saves, protected Default deletion. |
| LAB-DEV-02 | unity/DesignLab/authoring-history-v1.json tracked in Git; LabProjectStorage author path + runtime project-vs-player writes/status; Development Player wiring not built/tested. |
| LAB-PROD-01 | LabReleaseCatalogTests whole-profile guard all data APIs, copies survive updates; package-authority explicit old copy/readonly/current catalogue selection. |
| LAB-RELEASE-01 | authoring/staging all-and-only marks, project missing-Default editable/error; default-source old compatible source resolves without freezing flag. Incompatible-only author error fallback open below. |
| LAB-RELEASE-02 | default-source startup/reset original identity; public-order highest public; package-authority no factory-tail substitution/current catalogue wins. |
| LAB-RELEASE-03 | flag-only data/API and actual UI tests, no activation/new version, explicit Save only; package projection detached, no commit/build/publish hidden. |
| LAB-ACTIVE-01 | runtime/workspace selected/saved→active, pending async selection guards, error previous active intact; current displayed base vs active in approved19. |
| LAB-MATCH-01 | ProvingGround start freezes owners/reference; Workspace next-match HUD test; phase routes do not open Lab from match/pause. No live editor extension. |
| LAB-STORAGE-01 | Atomic temp/flush/publish, write-failure/draft intact, bad input source intact; production baseline/back verified. **Author historyLoadError blocks ordinary start indefinitely: Q7/mockup28 pending.** Concurrent author processes not advertised/supported; in-process writes serialized, multiple-reader fixture only. |
| LAB-UPGRADE-01 | saved-values exact compatible own values incl old default/0/false; repeated-upgrade current Default fields; default-source identity/full Save; structural-policy three maps×three roles. |
| LAB-UPGRADE-02 | structural-policy known rename/split; Diagnostics/Type strict unknown/removed/type/range/resource rejection, original preserved; mixed-history valid selection isolated and old-only inspect/unmark. **Author inaccessible-file fallback Q7 still open.** Automatic history repair optional by STORAGE-01, not implemented/claimed. |
| LAB-TEST-01 | Lab selection and ordinary setup/start freeze exact config/reference. Approved26–27 implemented: frozen profile/source version/known date visible in seat/operator pause;4 actual PlayMode checks and7 rendered PNGs in pause-identity/,1/2/4 viewport1080p/720p/long names. Physical/device/human acceptance separate. Deterministic test instrument not required/claimed. |
| LAB-UX-01 | approved-ui/ typed fields, focus/footer,720p/short viewport; other menu-base source audit and new20–25 pendingQ5; pause/author error pendingQ6/Q7. Physical device/human gates not replaced. |
| LAB-EXTEND-01 | No user preview/import/export/series editor added. Explicit build catalog tooling validates originals/prospective hashes and stages under ownership; not automatic publication. Legacy QA/review modes are project test tooling, no cross-release deterministic promise. |

Следующий обязательный UI шаг после человеческого ответа:20–25(Q5),26–27(Q6),28(Q7), с affected state/focus/input/rendered verification и сохранением originals. До ответа можно продолжать независимую полную five-package applicability matrix/source audit. Полный набор тестов/Player/build/deploy не разрешён этим отчётом.
