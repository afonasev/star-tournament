## Context

База fc85267, native human-only 2–4 seats. Моторами и боем владеет NativeCombatSession; CombatPresentation читает life state. V2 восстановлен pipeline a47acfe с неизменным исходником. См. proposal.md. Read-only Astra review выполнен до выбора архитектуры.

## Goals / Non-Goals

**Goals:** editor-imported skinned ресурсы, семь existing clips, реальный shotgun grip и hands derivative, независимый clock/ownership presentation.

**Non-Goals:** Humanoid retargeting стороннего mocap, новые персонажи, gameplay или пакеты; полная художественная и target-hardware приёмка.

## Decisions

- Generic Animator с manual Playables и editor-imported nonlegacy клипами. Humanoid потребовал бы отдельного T-pose/Avatar retarget и может изменить позы пальцев; для собственных baked clips не нужен. Импортированный glTFast controller не считается надёжным: его сохранение не реализовано; loopTime импортируется true для всех, поэтому Editor сохраняет проверенные clip assets с правильной loop policy.
- Gameplay session clock управляет sampling, включая pause; Animator.applyRootMotion=false. Death трансформирует только внутренний skeleton Root визуального corpse; motor/collider никогда не дочерние Animator. Прежний rigid 90° fall исключён для trooper.
- Body и first-person руки — производные одной геометрии/skin, с общими immutable материалами/текстурами и клипами; per-seat pose instance и layer isolation. First-person body/head/legs не рендерятся. Хват адаптируется к реальному existing shotgun, а не сохраняется как два независимых цилиндра.
- Shared профили с metadata управляют animation transitions/locomotion thresholds/view offsets. Новые renderer state не входят в gameplay snapshot.
- Устойчивая исходная/v2 копия: /Users/eaafonasev/Documents/Codex/asset-vault/star-tournament/trooper-v2. Shipping GLB и attribution проверяются и входят в Git. Исторический handoff16 перенесён в evidence/CANDIDATE_HANDOFF.md, новый handoff21.

## Risks / Trade-offs

- [Тяжёлые текстуры] → bounded shipping resize/reuse, измерения размеров и shared instance ресурсов, diagnostic frame time.
- [Новый rig, разные координатные системы] → editor checks skeleton/clip bindings, actual Player animated evidence, тесты root isolation.
- [Хват и естественность движений] → actual mesh contact audit и PNG по фазам; художественная приёмка не выводится из численного PASS.
- [Скрытая утечка per-seat/Repeat] → tests на layers/shared resources/reset/pause и 2/3/4 Player capture.

## Migration Plan

Изолированная ветка codex/unity-trooper-integration; main не менять. Восстановить v2, build shipping, подготовить imported assets, подключить presentation, последовательно prepare/test-edit/test-play/build и muted Player. Отдельный проверенный commit; physical/TV/reference hardware/long foreground60FPS gates остаются открытыми.
