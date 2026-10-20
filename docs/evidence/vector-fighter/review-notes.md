# VECTOR review package

This is a working Blender/Unity prototype. **The visual target is not achieved.**

Open the editable source at `scripts/vector_fighter/vector-fighter.blend`.
Compare `model-front.png`, `model-back.png` and `helmet.png` against
`approved-vector-concept.png`. The modeling reference is guidance, not a 3D render.
Studio armor is shown in teal; source albedo is neutral for player-color assignment.

`fps-walk.mp4` contains actual Unity rendering for all four weapons. The left seat
moves and the right seat remains stationary. The sleeve is closed, the hands keep
their grip, and each seat retains its assigned color. The near-camera cloth is
still too bright and needs material refinement. This short clip does not cover
shooting, switching, full world locomotion, ordinary jumping or ragdoll video.

`unity-poses/` shows the actual skeletal fixture's grounded/rising/falling/landing
samples with the same skin. Its actor position is intentionally unchanged to test
that presentation cannot move the motor. Do not read it as a jump trajectory.

Focused checks and limitations are summarized in `checkpoint.md`.
`verification-plan.md` retains the investigation and failures rather than replacing
them with a green-only history. Automated checks do not constitute human acceptance.
