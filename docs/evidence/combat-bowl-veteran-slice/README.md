# Combat Bowl — образец «ветеран лиги»

Северный выход центрального зала. Новые материалы, ремонтные панели, коммуникации, сервисный шкаф, вентилятор/пар, тёплый свет и два кейса. Кейсы и выступающий шкаф имеют authored коллизии; проход, спавны, бонусы и переходы сохранены. Map combat-bowl-v1@17.

Full gate: 321/321 EditMode, 192/192 PlayMode; macOS Player build и muted visual smoke завершились успешно, Repeat=true. В полном PlayMode проверены физическое укрытие, проходимость портала по трём линиям в обе стороны и Repeat, а также прежние natural bot match gates.

EditMode компилировался на be6ee4a; единственная следующая правка 8a17241 меняет координату диагностической камеры, чтобы она не находилась внутри колонны. Gameplay/profile/test sources идентичны. PlayMode и Player собираются из 8a17241.

Human artistic/gameplay acceptance открыта. Постановочные кадры — реальные native Player renders с приостановленной симуляцией; они не заменяют пользовательскую игру возле кейсов. Seed 20260923.

Кандидат: `/Users/eaafonasev/Projects/star-tournament/.local/player-candidates/combat-bowl-veteran-8a17241/StarTournamentProvingGround.app`. Сохранён переносом исходного bundle; все 282 файла сверены по SHA-256 с bundle-manifest.json.

На стартовом кадре четырёх камер HUD показывает 9 FPS. Это ранний кадр после загрузки, а не прогретый benchmark; целевые 60 FPS на reference hardware не подтверждены.
