## 1. Canonical family contract

- [x] 1.1 Добавить canonical profile selector и registry пяти stable family IDs; EditMode проверяет mapping, одинаковые/разные inputs и отказ неизвестного ordinal.
- [x] 1.2 Реализовать пять pure family definitions с distinct supports/solids/routes/spawns и минимум двумя transitions; EditMode проверяет structural matrix и отсутствие seed-driven выбора.

## 2. Native projection

- [x] 2.1 Сохранить единую `ProvingArena` projection для всех family definitions и explicit default selection в native setup; PlayMode проверяет build, support identity и representative NavMesh routes.
- [x] 2.2 Добавить representative diagnostic selection/identity в native Player evidence path без нового UX; PlayMode/JSON подтверждают фактически построенные families.

## 3. Verification and handoff

- [x] 3.1 Запустить targeted и полный Unity EditMode/PlayMode, strict OpenSpec validation и Mac Player build; сохранить команды и результаты в evidence JSON.
- [x] 3.2 Провести muted native Player diagnostic на representative families, осмотреть PNG/JSON, оформить handoff и явно оставить physical, artistic и performance gates открытыми.
