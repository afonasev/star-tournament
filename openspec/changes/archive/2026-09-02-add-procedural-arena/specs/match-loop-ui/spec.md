## ADDED Requirements

### Requirement: Выбор arena size и seed до матча
Предматчевый DOM setup SHALL предоставлять выбор `small`, `medium` либо `large`, где `medium` выбран по умолчанию, и необязательный ручной unsigned seed. Пустое seed field SHALL означать auto generation; UI MUST показать resolved seed и arena identity после успешного старта.

#### Scenario: Auto seed
- **WHEN** пользователь оставляет seed пустым и запускает валидную configuration
- **THEN** shell разрешает новый concrete seed, генерирует выбранный size и сохраняет exact identity сессии

#### Scenario: Ручной seed
- **WHEN** пользователь вводит допустимый seed и запускает матч
- **THEN** runtime использует именно этот seed и тот же size/version/profile reproduces arena content hash

#### Scenario: Неверный seed
- **WHEN** seed не является поддерживаемым unsigned integer
- **THEN** старт блокируется, а поле показывает локальную понятную ошибку

### Requirement: Actionable generation error
Если generation либо validation не завершается accepted arena, setup MUST остаться DOM-surface без gameplay ticks и показать seed, стабильную причину и действия retry, new seed и explicit fallback.

#### Scenario: Generation failure
- **WHEN** все bounded attempts отклонены
- **THEN** UI не создаёт частичный collision/renderer runtime и позволяет пользователю выбрать следующее действие

#### Scenario: Fallback подтверждён
- **WHEN** пользователь явно выбирает fallback
- **THEN** UI запускает shipped fallback и показывает её фактическую identity
