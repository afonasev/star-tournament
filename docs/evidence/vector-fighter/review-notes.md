# VECTOR review package — F1

Open `scripts/vector_fighter/vector-fighter.blend` for editable source.
Compare `model-front.png`, `model-back.png` and `helmet.png` with the selected
`approved-vector-concept.png`. The concept and modeling reference are not 3D evidence.
Studio armor has a display-only teal tint; the exported material is neutral for
player-color assignment. `render-source.json` binds the render to the shipping mesh.

Current first-person evidence is `f1-fps/`: four weapons, walking left seat and
stationary right seat, with slimmer shared forearms and relocated equipment fill.
`f1-world/` contains the current skeletal pose fixture, with a stationary actor.
The earlier `fps-walk.mp4`, `unity-fps/` and `unity-poses/` belong to the d71baf3
iteration and deliberately remain labeled historical.

Focused checks are documented in `verification-plan.md`. The intermediate F1
31/32 EditMode failure is retained: the airborne offset had contaminated the
WalkOffset API. The code now separates ViewOffset; the existing zero-step test
was not relaxed. Automated checks do not imply visual or human acceptance.

The latest model is a further art iteration, still simpler than the illustration.
See `checkpoint.md` for ownership, remaining gates and the continuation point.

## F2 review
User rejected F1 primary grip: the hand was visibly beside the rifle. This was a real defect despite the former structural tests. F2 turns the wrist into the handle and preserves sampled equipment placement. New geometry adds upper-body breadth and small manufactured details. The unchanged thin forearm and dark textile remain. Final images are actual model/Unity outputs; concept-level fidelity and complete world action/human acceptance remain open.
