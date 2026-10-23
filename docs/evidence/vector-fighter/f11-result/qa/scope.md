# F11 — минимальная проверка
Baseline: ba8bf6b7e2e92140d4f9d98cfd05b646295ebe94; runtime F9 20cce04d.
Изменения: контур керамики, непрерывный рукав/манжета, посадка прежних деталей перчатки, woven PBR; support elbow IK и Trooper @17 с точным predecessor @16.
Зависимости: оба GLB, импорт/prefabs/material roles, frozen grip и сохранённая Lab history.
Проверки: Blender export; GLB hierarchy/animation equality; TrooperPreparation.RefreshExistingScene; TrooperImportTests; OwnHistoryKeepsEveryCompatibleSavedValueAcrossFactoryRetunes (14 cases); SupportWristAlignmentPreservesPalmDigitsAndBoneLengths; FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene; OrbitalGripDoesNotAccumulateAcrossFrozenSamples; MixedReviewHasOwnedMaterialsForEveryParticipant.
Команды: .local/vector-f11/verify.sh. Только shared Unity -noaudio -muted, отдельная история; реальные кадры всех четырёх оружий с factory profile, без overrides.
Full suite, natural matches, Player build, integration/release/deploy, performance/device и human acceptance — NotRun.
