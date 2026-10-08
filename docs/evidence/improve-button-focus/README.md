# Gold button navigation focus

Approved direction: thick gold rim, dark gap and pointer, preserving existing fills and text. UI-only implementation uses a shared MenuFocusGraphic for all ButtonElement factories, roster and manual per-seat pause/settings focus. Hover remains thin; disabled buttons do not get the navigation rim. Compact control strokes scale inward to avoid overlapping rows, and the graphic is behind text/semantic markers and never intercepts pointer input.

Verified so far: affected screens PlayMode 20/20, dedicated focus contract 2/2 (included in those 20), current UI layout EditMode 3/3; source diff whitespace and strict OpenSpec validation. Test scenes were regenerated with unity/tools.sh prepare without Player build. Generated scene/prefab/ProjectSettings derivatives are not part of the implementation.

Pending: UI gate PlayMode (FourPlayerMenuTests and IndependentPauseMenuTests), Editor render inspection, main integration and cleanup. Runtime admission remained unavailable for the remaining gate after over one hour overall; only the owned waiting commands were cancelled. The PlayMode gate did not start; it is not a source test failure. Approved concept is a mockup, not the rendered implementation. No Player build, installed update, distribution or physical/visual Player acceptance is authorized by this implementation request.
