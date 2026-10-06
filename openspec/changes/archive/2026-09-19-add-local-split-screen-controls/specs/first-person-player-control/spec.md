## MODIFIED Requirements

### Requirement: Keyboard/mouse focus и pointer lock
Browser match SHALL допускать не более одного local keyboard/mouse seat среди 1–4 local seats. Его movement, look, jump и fire MUST поступать только при активном pointer lock; потеря lock, blur или скрытие страницы SHALL очистить held actions всех local seats и остановить gameplay ticks до явного resume. Назначенные gamepad seats MUST сохранять собственные action sources и не получать keyboard/mouse actions.

#### Scenario: Pointer lock получен
- **WHEN** пользователь активирует start/resume overlay и браузер подтверждает pointer lock
- **THEN** keyboard/mouse actions направляются только назначенному seat, а остальные seats используют только свои device bindings

#### Scenario: Pointer lock потерян
- **WHEN** pointer lock пропадает из-за Escape, browser UI или смены фокуса
- **THEN** все held gameplay actions очищаются, runtime останавливает gameplay ticks и показывает общий overlay продолжения

#### Scenario: Начало игры
- **WHEN** совместимый browser runtime готов, но keyboard/mouse pointer lock ещё не получен
- **THEN** сцена и compact HUD видимы, DOM overlay предлагает начать матч, а gameplay ticks и fire не выполняются

#### Scenario: Gamepad не участвует в slice
- **WHEN** gamepad подключается, отключается или посылает buttons без binding к local seat
- **THEN** keyboard/mouse seat, viewport и simulation не меняются

### Requirement: Keyboard UI actions match loop
Keyboard/mouse adapter SHALL нормализовать `Tab` как held scoreboard action и `Escape` как edge-triggered pause request отдельно от movement/look/jump/fire. UI actions MUST NOT входить в gameplay scoring либо movement state; потеря pointer lock, blur, visibility loss, pause, results и dispose MUST синхронно очищать held actions всех local inputs.

#### Scenario: Tab не меняет gameplay
- **WHEN** keyboard/mouse seat удерживает и отпускает `Tab`
- **THEN** UI получает held state, а movement, weapon, match timer cadence и state hash эквивалентного gameplay stream не меняются

#### Scenario: Escape передан приложению
- **WHEN** browser доставляет новый `Escape` press во время local match
- **THEN** runtime создаёт одну pause request, очищает held gameplay/UI input и не ожидает повторения клавиши

#### Scenario: Escape не передан
- **WHEN** browser снимает pointer lock/fullscreen без key event
- **THEN** общий focus lifecycle ставит матч на паузу и очищает input всех seats

#### Scenario: Gamepad вне scope
- **WHEN** подключается либо посылает buttons gamepad, не назначенный local seat
- **THEN** scoreboard, pause, roster и action frames не меняются
