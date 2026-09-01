## ADDED Requirements

### Requirement: Полный профиль basic match loop
`prototype-v1` SHALL содержать числовые параметры duration bounds/default, score-target bounds/default/step, assist window/points, kill-chain gap/cumulative totals/post-five increment и death/respawn timing. Каждое поле MUST иметь ровно один descriptor со стабильным path, группой, подписью, описанием, unit, hard minimum/maximum и input step; match configuration validation MUST использовать эти же metadata.

#### Scenario: Score target descriptors
- **WHEN** shipped profile загружен
- **THEN** target default равен 3000, configuration minimum 1000, maximum 20 000 и step 100 доступны через registry, а nullable match target по умолчанию остаётся выключенной настройкой configuration

#### Scenario: Неверная цель
- **WHEN** configuration включает target ниже 1000, выше 20 000 либо не кратный шагу 100 от minimum
- **THEN** validation отклоняет configuration до первого tick со stable path цели

#### Scenario: Полное match coverage
- **WHEN** schema и descriptor registry аудируются автоматически
- **THEN** все числовые match/scoring/death/respawn fields имеют один descriptor, orphan и duplicate paths отсутствуют

#### Scenario: Cross-field ranges
- **WHEN** minimum/default/maximum duration или target нарушают порядок либо kill-chain totals не возрастают
- **THEN** profile validation возвращает deterministic cross-field code и все связанные paths

