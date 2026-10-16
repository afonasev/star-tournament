Baseline 630f8244, codex/game-standard-alignment.

Scope: missing-field fill source uses latest published Default by Revision in production / actual marked project Default in author mode; trusted legacy mapping extracted without changing rules, registry index shared for entire migration. No gameplay tuning or UI design change.

Affected checks: LabAuthoringTests (new prod/dev defaults + mark/history/projection neighbors), LabReleaseCatalogTests (published interpretation/staging), DesignLabHistoryTests.AudioUpgrade / FootstepMixUpgrade / GamepadUpgrade / DeathProfileUpgrade / DeathImpulseUpgrade / ShotOriginDescriptorMigrates / ScoreboardUpgrade / HistoricalWarmCache. These touch extracted historical mappings without all unrelated gameplay tests. Run via shared test-edit-filter, -noaudio -muted. Full/Player/build/deploy not requested. Task3.1 remains open for persisted generated counterpart lifecycle and explicit incompatibility audit.
