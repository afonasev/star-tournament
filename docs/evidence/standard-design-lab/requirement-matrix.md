# Lab: матрица требований 0.2.0

Проверяемый pin game-spec-templates60fe3d2787dfb860722372791ee67dc8453f6f66 (local и origin HEAD проверены17.10.2026). Полная цель включает ещё четыре пакета. Эта матрица не закрывает parent/change и не подменяет native/human acceptance. Source до текущих test-only дополнений: codex/game-standard-alignment@f38103cc. Ранее проверенные контракты учитываются по неизменённым областям и более новым affected QA, а не автоматически по наличию отчёта.

Evidence paths ниже относительно этого каталога. «Техническое основание» включает inspected source + перечисленные executed assertions, не означает проверку всех устройств/всех runtime веток. Общие MENU-ACTIONS-01/02/03 реализованы по Q5/20–25 с9 unique affected Editor cases в ../standard-menu-actions/implementation; native/human gates отдельно открыты, поэтому общий UX пока не завершён. Native Development Player -labProject остаётся отдельным непроверенным gate без команды на сборку.

| ID | Техническое основание и текущая граница |
| --- | --- |
| LAB-SCOPE-01 | Main Lab route/OpenDesignLab, отдельные settings/setup; runtime/ и approved-ui/ — actual screen and back. |
| LAB-SCOPE-02 | LabBundle.IsVisible/IsEditable,638 visible/14974 excluded, catalogue/actual-catalogue.json, consumers/ и workspace hidden-path/save guards. |
| LAB-GROUPS-01 | ResetLabDraft/RefreshLabWorkspace единый registry→groups/search/diff; approved-ui actual rendered tree. |
| LAB-CATALOG-01 | catalogue/: все actual visible metadata валидны, numeric155integer/481decimal/2boolean, defaults source/NextGame checked. В editable catalogue нет text/enum/resource selectors, их domain widgets не требуются. |
| LAB-CATALOG-02 | consumers/:638 source bindings, actual selected→frozen lifecycle; hud-lifecycle/ устранил найденные startup-only UI caches. Это source+targeted lifecycle proof, не638 executed effects. |
| LAB-FIND-01 | DesignLabWorkspaceTests search hidden paths/global changes; LabDiscardWarningTests hidden changes. Reset via clear search; short actions/source copy20–25 implemented and checked in ../standard-menu-actions/implementation. |
| LAB-EDIT-01 | Workspace partial raw input/focus/step/save; typed-values/ boolean guards; approved-ui18. Approved20 single checkbox button/state/0or1 implemented; A toggle twice, readonly/save/focus/viewport assertions in ../standard-menu-actions/implementation. |
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
| LAB-RELEASE-01 | authoring/staging all-and-only marks, project missing-Default editable/error; default-source old compatible source resolves without freezing flag. Approved28 author fallback implemented; exact source/write/reload guards in author-fallback/, native process gate still open. |
| LAB-RELEASE-02 | default-source startup/reset original identity; public-order highest public; package-authority no factory-tail substitution/current catalogue wins. |
| LAB-RELEASE-03 | flag-only data/API and actual UI tests, no activation/new version, explicit Save only; package projection detached, no commit/build/publish hidden. |
| LAB-ACTIVE-01 | runtime/workspace selected/saved→active, pending async selection guards, error previous active intact; current displayed base vs active in approved19. |
| LAB-MATCH-01 | ProvingGround start freezes owners/reference; Workspace next-match HUD test; phase routes do not open Lab from match/pause. No live editor extension. |
| LAB-STORAGE-01 | Atomic temp/flush/publish, write-failure/draft intact, bad input source intact; production baseline/back verified. Approved28 implemented: author load-error card with plain reason and safe Back; explicit session-only shipped Default permits ordinary setup, Save/Create remain blocked, exact original/package bytes retained. author-fallback/:4 affected PlayMode passed,3 new rendered states and2 neighbors; scene reload requires new choice, standalone process/device acceptance separate. Concurrent author processes not advertised/supported; in-process writes serialized, multiple-reader fixture only. |
| LAB-UPGRADE-01 | saved-values exact compatible own values incl old default/0/false; repeated-upgrade current Default fields; default-source identity/full Save; structural-policy three maps×three roles. |
| LAB-UPGRADE-02 | structural-policy known rename/split; Diagnostics/Type strict unknown/removed/type/range/resource rejection, original preserved; mixed-history valid selection isolated and old-only inspect/unmark. Approved Q7 author read-error fallback implemented and Editor-tested in author-fallback/; source is never repaired or overwritten. Automatic history repair optional by STORAGE-01, not implemented/claimed. |
| LAB-TEST-01 | Lab selection and ordinary setup/start freeze exact config/reference. Approved26–27 implemented: frozen profile/source version/known date visible in seat/operator pause;4 actual PlayMode checks and7 rendered PNGs in pause-identity/,1/2/4 viewport1080p/720p/long names. Physical/device/human acceptance separate. Deterministic test instrument not required/claimed. |
| LAB-UX-01 | approved-ui/ typed fields, focus/footer,720p/short viewport; menu-base action source audit and approved20–25 implemented with9 unique affected cases; pause26–27 and author error28 implemented with scoped Editor evidence. Physical device/human gates not replaced. |
| LAB-EXTEND-01 | No user preview/import/export/series editor added. Explicit build catalog tooling validates originals/prospective hashes and stages under ownership; not automatic publication. Legacy QA/review modes are project test tooling, no cross-release deterministic promise. |

Q5–Q10 согласованы; повторное согласование не требуется. 20–28 реализованы с affected state/focus/input/rendered verification и сохранением originals. Следующий общий UI шаг: Q8/device-aware hints29.26–28 имеют Editor evidence, native/human gates остаются отдельно открытыми. Полный набор тестов/Player/build/deploy не разрешён этим отчётом.
