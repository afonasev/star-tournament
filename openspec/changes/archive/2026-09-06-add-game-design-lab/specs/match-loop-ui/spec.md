## ADDED Requirements

### Requirement: Вход в лабораторию и выбранный профиль нового матча
Главное меню SHALL предоставлять действие открытия Game Design Lab и SHALL отображать identity выбранной сохранённой game-design revision до запуска матча. Предматчевый Start MUST использовать именно эту revision либо понятным образом блокироваться, если revision недоступна/невалидна; settings и graphics controls сохраняют presentation-only contract.

#### Scenario: Возврат из лаборатории
- **WHEN** пользователь выбирает valid revision и возвращается из лаборатории в главное меню
- **THEN** меню показывает выбранные profile id/revision/content hash, а последующий Start создаёт матч с этой exact identity

#### Scenario: Недоступный профиль
- **WHEN** выбранная revision больше недоступна или невалидна
- **THEN** меню не запускает матч, объясняет проблему и не подменяет revision без явного выбора пользователя
