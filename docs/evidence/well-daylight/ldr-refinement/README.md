# Intensity-only refinement

2/2 rendering fixtures Passed, but visual review still found broad constant RGB219 regions on lamp-lit walls. Measured corner-room crop (0,0)-(500,350): initial-runtime has 163371 pixels at (219,219,219), intensity-only version has 160273. This is diagnostic evidence, not acceptance.

Root cause verified in installed URP17.3.0: source asset m_ColorGradingMode=0, LDR LUT saturates before Neutral can retain highlight gradients. Final fix uses an owned map-local transient URP asset copy with HDR grading; no project asset or color-space change. The final shared effect lifecycle/isolation and both render fixtures rerun. Default raw lamp25 is retained (37.5 at Lighting50).
