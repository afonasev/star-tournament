## 1. Snapshot profile compatibility

- [x] 1.1 Передать exact validated profile context через playable snapshot parse/serialize/hash и step boundaries; проверить, что current release `prototype-v1 v7` создаёт и разбирает initial snapshot.
- [x] 1.2 Сохранить deterministic rejection другой profile identity и добавить regression tests для current-release bootstrap/serialization paths.

## 2. Проверка и handoff

- [x] 2.1 Запустить focused tests, `npm test`, `npm run build`, `openspec validate repair-snapshot-profile-bootstrap --strict` и `git diff --check`; зафиксировать результаты.
- [x] 2.2 Запустить muted dev-стенд из worktree, подтвердить HTTP URL и провести in-app Browser smoke с реально запущенным матчем; сохранить и проверить PNG.
- [x] 2.3 Зафиксировать только данный change отдельным commit и обновить handoff с фактическим URL, revision, тестами и screenshot evidence.
