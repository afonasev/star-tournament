## Context

См. [proposal.md](proposal.md). Renderer уже создаёт `PointLight` у attachment `lamp`, но WebGL shadow map не включена, а lamp fixtures выбираются в порядке обхода surfaces. `PresentationProfile` владеет числовым budget света; graphics preset уже определяет доступный presentation detail.

## Goals / Non-Goals

**Goals:**

- Делать освещённые architectural детали объёмными через небольшой, предсказуемый budget мягких теней.
- Сохранять яркое заполнение и contrast participants во всех presets.
- Делать shadow cost, sampling quality и ресурсы проверяемыми отдельно от simulation.

**Non-Goals:**

- Не менять semantic attachments, GLB manifest, arena generation или collision shell.
- Не добавлять меню, сетевой contract, baked lightmaps, dynamic day/night cycle или тени от каждого робота.

## Decisions

### Равномерный renderer-only rhythm ламп

Renderer выводит lamp fixture placement из existing canonical wall surface slots: corridors, rooms и combat zones получают повторяемый, но не blocking rhythm без новой geometry в `ArenaDefinition`. Fixture включает GLB housing, emissive lens и local point light; Low может убрать light contribution, но не сам читаемый lens. Альтернатива — хранить больше attachments в generator payload: она меняет arena identity ради presentation detail.

### Контрастный global fill

Profile-owned studio fill управляет hemisphere/key contribution отдельно от lamp intensity. Default снижает background и fill до medium-dark уровня, но rim light роботов сохраняется. Альтернатива — затемнять authored albedo: она смешивает lighting policy с texture contract и хуже масштабируется по quality tier.

### Отбор shadow caster по близости к камере

Renderer создаёт все разрешённые локальные lamp lights как раньше, но после complete scene setup выбирает shadow casters из них по расстоянию до active first-person camera. Это делает наиболее заметные тени при ограниченном GPU budget и не привязывает результат к порядку generation. При равной дистанции используется stable lamp id. Альтернатива — тени у первых attachment в arena order; она даёт неочевидный результат и может оставить видимую зону без теней.

### Ограниченный point-light shadow budget

`localLightShadowCount` задаёт верхний предел caster для `Low`/`Balanced`/`High`/`Ultra` как 0/1/2/4. Каждому выбранному `PointLight` включается кубическая shadow map с profile-owned resolution и bias; остальным — только освещение. Альтернатива — directional shadows от всего scene: это не передаёт локальный характер светильников и делает стоимость зависимой от площади арены.

### Мягкость, но без затемнения матча

Renderer включает `PCFSoftShadowMap`; local light использует малый radius для чёткого contact-depth на полу, а ambient hemisphere/key/rim lights не становятся shadow casters. Wall shell и wall presentation не получают point-light shadows: их широкие panel surfaces не являются надёжным temporal receiver. Альтернатива — получать тени на стенах: она даёт мерцание при смене camera-relative caster и потому исключена.

### Presentation-only profile contract

Новые поля profile: `localLightShadowCount`, `localLightShadowMapSize`, `localLightShadowRadius`. Они получают descriptor metadata и range validation; shipped profile поднимает revision/content hash. Профиль по-прежнему не является simulation input. Альтернатива — hardcode: он нарушает действующее правило Balance Lab для чисел, влияющих на читаемость.

### Lifecycle и verification

Shadow selection выполняется только при setup/resizing camera invalidation и освобождается общей disposal веткой renderer. Renderer unit tests используют `RendererPort`; источник должен проверять включение shadow map через inspection scene graph. Browser gate запускается на all quality presets, а visual QA фиксирует updated scene PNG с `?muted=1`.

### Geometry stability

`wall-bay` — тонкий renderer-only фасад перед canonical wall shell. Его scaled bounds целиком лежат снаружи shell и отделены от него положительным clearance; пересекающиеся и совпадающие faces запрещены, потому что они дают depth competition независимо от light/shadow settings. Этот gap не меняет collision proxy, navigation или identity.

`wall-architecture-gap-seal-*` является обратным случаем: это canonical collision-only overlay, который намеренно перекрывает adjacent solids для capsule safety. Renderer не создаёт ему mesh и сохраняет shell geometry соседних primary walls.

## Risks / Trade-offs

- Несколько point-light cube maps могут снизить FPS на слабом GPU → Low отключает shadows, Balanced ограничен одним caster, а High/Ultra используют жёсткий ceiling 2/4.
- Светильник за камерой может сохранить shadow map до следующей presentation invalidation → camera-relative selection вызывается перед render при изменённой camera transform, но не делает дополнительный WebGL submission при idle snapshot.
- Мягкие contact shadows могут быть незаметны среди ярких fixtures → проверка включает fixture с portal/wall relief и зону пола рядом с lamp, не повышая shadow caster budget.

## Migration Plan

Новая profile revision применяется вместе с renderer; локальные пользовательские profile snapshots с прежней схемой безопасно отклоняются и runtime использует shipped `Balanced`. Откат — удаление новых renderer-only полей и возврат старого profile revision, без migration simulation/replay data.
