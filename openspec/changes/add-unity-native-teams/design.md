## Context

NativeMatchRoster/State уже поддерживают команды; runtime всё ещё FFA. Read-only Astra review подтвердил границы adapters и необходимость initial spawn, а не только respawn. User выбрал ally blocking без damage.

## Goals / Non-Goals

**Goals:** законченный human-only командный матч2–4 с текущими motor/trooper/layouts.
**Non-Goals:** bots/solo/8 bodies, procedural fairness, replay/network, новые ассеты.

## Decisions

- Setup хранит draft mode/assignments и orientation существующей browser team-color пары; перед запуском immutable roster и presentation choice замораживаются. Оба состава непустые, invalid draft явно блокирует normal/diagnostic start. Repeat использует frozen state. Device joins и viewport ownership прежние.
- Shot resolver видит все живые hit volumes; allied nearest hit поглощается без damage. ApplyDamage отдельно защищает от allied direct calls. World/self semantics прежние.
- Respawn передаёт только enemies для LOS/path scoring; physical occupancy проверяет все capsules. Initial teams используют те же physically valid slots, stable participant order и минимальную opponent separation. Без валидного batch старт выдаёт явную ошибку, не unsafe fallback.
- Новый `unity-native-team-v1@1` содержит initialOpponentSeparation: перенос medium baseline5m, metadata1–8m шаг0.5. Старые combat/trooper профили не меняются. Цвета — существующая vetted identity пара, не новый lighting/tuning.
- Таблица расширяется двумя team-heading rows с теми же numeric columns и сортировкой внутри команд; FFA сохраняет нынешние размеры. Три seats по-прежнему держат permanent table в четвёртой четверти.
- Identity применяется через существующие MaterialPropertyBlock и HUD, общие meshes/materials/textures сохраняются; death/respawn/Repeat проверяют отсутствие stale цвета.

## Risks / Trade-offs

- Initial spawn не равен respawn → отдельный atomic reservation batch до включения gameplay.
- Дополнительные controls/table rows → native FHD/4K screenshots2/3/4 и gamepad navigation regressions.
- Synthetic QA не доказывает hardware acceptance → сохранить отдельные gates и foreground diagnostic как предварительный.
