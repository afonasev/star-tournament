## ADDED Requirements

### Requirement: Keyboard UI actions match loop
Single-seat keyboard adapter SHALL нормализовать `Tab` как held scoreboard action и `Escape` как edge-triggered pause request отдельно от movement/look/jump/fire. UI actions MUST NOT входить в gameplay scoring либо movement state; потеря pointer lock, blur, visibility loss, pause, results и dispose MUST синхронно очищать held actions.

#### Scenario: Tab не меняет gameplay
- **WHEN** locked пользователь удерживает и отпускает `Tab`
- **THEN** UI получает held state, а movement, weapon, match timer cadence и state hash эквивалентного gameplay stream не меняются

#### Scenario: Escape передан приложению
- **WHEN** browser доставляет новый `Escape` press во время locked match
- **THEN** adapter создаёт один pause request, очищает held gameplay/UI input и не ожидает повторения клавиши

#### Scenario: Escape не передан
- **WHEN** browser обрабатывает Escape только снятием pointer lock/fullscreen
- **THEN** существующий focus lifecycle ставит матч на паузу без обязательного pause action

#### Scenario: Gamepad вне scope
- **WHEN** gamepad подключается, отключается или посылает buttons
- **THEN** scoreboard, pause, roster и единственный keyboard/mouse seat не меняются

