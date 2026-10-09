# Center results content

Feedback F1, 2026-10-10. Final table height equals occupied rows. Table, awards and actions form one centered block with uniform scale and at least 6% gutters. Frozen results/selection/navigation are preserved.

Affected Editor PlayMode: 4/4 passed (NativeRivalryPresentationTests, NativeAchievementsLayoutTests, NativeScoreboardLayoutTests). Renders: FFA and teams at 1280×720, 1920×1080, 3440×1440, 3840×2160. Viewed 720p, 1080p teams ultrawide FFA and 4K FFA. UI gate: 3 EditMode and 13 PlayMode passed with isolated QA Lab history. First UI attempt was stopped during 153 MB user-history validation (no PlayMode result); rerun used the unchanged UI fixtures. No Player build or deployment; human/physical-device acceptance pending.

Integration: 17/17 PlayMode tests passed after merging main 871af0c, tested source tree 469ddc3. Runtime layout files unchanged from screenshot evidence.
