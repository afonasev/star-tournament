## ADDED Requirements

### Requirement: Независимые immutable profile histories и release catalog
Profile subsystem SHALL хранить локально созданные полные валидные `GameDesignProfile` revisions в отдельной append-only history для каждого stable profile id. Номер revision MUST инкрементироваться только внутри собственного profile id; отдельную сохранённую revision MUST NOT изменять или удалять. Checked-in `balance/releases.json` SHALL хранить опубликованные exact snapshots и единственный `releaseRef`; releaseRef MUST указывать на snapshot из этого каталога.

#### Scenario: Версии двух профилей независимы
- **WHEN** дизайнер создаёт revision для профиля A после сохранения профиля B
- **THEN** следующая revision получает номер относительно истории A, а history B и её revision numbers не меняются

#### Scenario: Продолжение поставляемого профиля
- **WHEN** дизайнер сохраняет valid draft, созданный от поставляемой revision `prototype-v1 v6`
- **THEN** исходный snapshot остаётся неизменным, а новая local revision получает тот же profile id `prototype-v1` и следующий номер `v7`

#### Scenario: Удаление нерелизного профиля
- **WHEN** дизайнер удаляет local profile, не содержащий current release revision
- **THEN** удаляется вся local history этого profile id, отдельные revisions не получают самостоятельного delete action, а selected profile возвращается к current release при необходимости

#### Scenario: Публикация release revision
- **WHEN** дизайнер на dev-стенде помечает valid saved revision как релизную
- **THEN** endpoint атомарно сохраняет exact immutable snapshot в `balance/releases.json` и переключает единственный `releaseRef` на эту identity

#### Scenario: Production publication недоступна
- **WHEN** лаборатория открыта в production build
- **THEN** она отображает repository release catalog read-only и не предлагает записать release в файловую систему

### Requirement: Startup selection exact revision
Profile subsystem SHALL выдавать только exact complete snapshot по выбранной identity/hash. Выбор local revision MUST быть проверен до нового match startup; при отсутствии явного local selection новый матч MUST использовать repository `releaseRef`. Отсутствие, повреждение либо hash mismatch selected/release revision MUST отклонять старт без fallback на другой профиль.

#### Scenario: Выбор сохранённой revision
- **WHEN** меню передаёт сохранённую local profile identity для нового матча
- **THEN** loader возвращает exact immutable snapshot с совпадающим content hash и runtime использует его во всех profile consumers

#### Scenario: Повреждённая revision
- **WHEN** выбранная local либо repository release revision отсутствует, не проходит validation либо не совпадает с ожидаемым hash
- **THEN** новый матч не стартует, UI получает actionable validation error, а shipped profile не подставляется молча
