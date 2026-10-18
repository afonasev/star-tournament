# HDR diagnostics before final lamp calibration

Shared/lifecycle controls: 5/5 Passed with map-local HDR grading clone, exact restoration, off/zero and map transition. Detail render repeated with HDR target: 1/1 Passed. Visual inspection still found excessive real highlights at raw lamp25 (effective37.5); the LDR target had previously hidden these as a plateau around RGB219-221.

Final calibration restores the original lamp source default7 and existing max50, with the new Lighting response producing10.5 at default50. Daylight2, chosen azimuth/pitch, cool fill, tone/exposure and three bounded local shadows remain. This keeps daylight dominant and avoids solving poor contrast by overpowering 24 ceiling lights. These raw diagnostics are preserved, not approved final images. Final render and affected default migration rerun separately.
