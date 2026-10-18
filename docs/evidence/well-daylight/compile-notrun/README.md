# Startup/compile attempts, not executed tests

1. `editmode.log.gz`: CS0200 on read-only URP additionalLightsShadowResolutionTier. No XML, no tests executed. Fixed by removing the unsupported setter and using three standard high-tier shadow sources rather than six low-tier sources. Exact installed URP source inspected; Context7 network lookup returned fetch failed. No private-field access, pipeline edit, larger atlas, or timeout change.
2. `editmode-2.log.gz`: CS0104 from two imported ShadowQuality enums in new PlayMode test. No XML, no tests executed. Fixed by qualifying UnityEngine.ShadowQuality.

These attempts remain NotRun; they are not counted as Passed or test-body failures. Subsequent successful XML is separate.
