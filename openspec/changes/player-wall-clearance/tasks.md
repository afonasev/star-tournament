## 1. Контракт и профиль

- [x] 1.1 Применить approved 0,55 m / 0,35 m capsule в immutable `prototype-v1`, descriptors и cross-field validation; verify profile/unit tests покрывают defaults, ranges и incompatible revision.
- [x] 1.2 Версионировать collision compatibility и backend-neutral wall-clearance query contracts; verify parser/checkpoint tests явно отвергают прежнюю identity.
- [x] 1.3 Обновить `docs/GAME_SPEC.md`, journal и `docs/tasks/14-player-wall-clearance.md`; verify спецификация отражает static-only body clearance и visual-only weapon retraction.

## 2. Детерминированная collision и арена

- [x] 2.1 Реализовать static-first wall-clearance solve и body/participant solve в stable order; verify прямую стену, оба типа углов, barrier, ramp, slab, jump и отсутствие participant penetration unit tests.
- [x] 2.2 Применить effective clearance к overlap, spawn safety, procedural validation и traversal без изменения participant separation; verify targeted generator/physical/spawn corpus tests.
- [x] 2.3 Обновить collision workload и reconstruction evidence; verify two independent 10 000-tick runs, restore at tick 5 000 и observer cadence 0/1/2/4 имеют одинаковые hashes и проходят budget.

## 3. Weapon presentation

- [x] 3.1 Реализовать renderer-only static/decor envelope query и всегда видимую сложенную позу third-person weapon; verify renderer test доказывает отсутствие simulation mutation и безопасную world-weapon transform у wall и декора.
- [x] 3.2 Реализовать first-person viewmodel/arms retract в всегда видимую сложенную позу с сохранением pointer-lock look; verify renderer test покрывает wall, декор, recoil, pitch bounds, resize и unchanged shot/snapshot data.
- [x] 3.3 Добавить shipped bounds/audit coverage тела, оружия и presentation envelopes; verify LOD0/LOD1 и supported presentation states не выходят за contracts.

## 4. Интеграционная проверка

- [x] 4.1 Выполнить targeted tests, полный test suite, build, `openspec validate player-wall-clearance --strict` и `git diff --check`; verify все команды завершаются успешно.
- [x] 4.2 Запустить muted dev-стенд из этого worktree и проверить HTTP URL; выполнить in-app Browser playtest keyboard/mouse pointer lock: ходьба, strafe, jump, свободный look/fire у wall, угла, portal, ramp и barrier.
- [ ] 4.3 Сохранить и визуально проверить актуальные PNG первого и третьего лица из dev-стенда; verify screenshots показывают body и weapon снаружи wall и не содержат console/WebGL diagnostics.
