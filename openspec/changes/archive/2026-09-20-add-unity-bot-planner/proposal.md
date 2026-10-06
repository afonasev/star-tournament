## Why

Native perception, navigation и participant composition проверены отдельно, но пока нет соперника, который сам ищет бой и стреляет. Следующий срез соединяет их в настоящий AI согласно GAME_SPEC §2,3,5,7 без привилегий над человеком.

## What Changes

- Data-only planner трёх сложностей: поиск, honest target/memory, ограниченное наведение, обычный fire edge, strafe, ситуативный jump, краткий отход и временная поддержка союзников.
- Native driver собирает один filtered pre-tick observation и действия всех ботов; единственный Session.Tick исполняет общие motor/weapon правила.
- Явное владение движением: transitions/recovery не перетираются combat steering; static tactical endpoints проверяются физически.
- Frozen profile и сериализуемые timers/RNG/planner state; death/pause/Repeat/menu lifecycle.
- Opt-in development journey с настоящими Bot-kind участниками,1–4 local views и total8; никаких scripted damage/решений вместо AI.
- Shipping add/remove/difficulty setup — следующий change; evaluation/holdout, generated arenas, полный recorded replay, physical/TV/art/long performance этим срезом не закрываются. Новых продуктовых развилок в bounded scope нет; replay schema/target hardware остаются открытыми GAME_SPEC §10.

## Capabilities

### New Capabilities
- `unity-bot-planner`: исполнение утверждённого AI через native observation/action/session контракты.

### Modified Capabilities
Нет.

## Impact

Runtime Core planner/profile, native driver/tactical adapter, navigation arbitration, ProvingGround lifecycle, EditMode/PlayMode и native review harness. База9f207d2; отдельный worktree/commit. Дополнительных пакетов и сервисов нет. Игрок увидит реальный бой с AI в development review; обычное shipping setup пока остаётся human-only.
