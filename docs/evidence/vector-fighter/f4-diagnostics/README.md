# F4 diagnostics — NOT final visual acceptance

Neutral bones/axes were measured in actual Unity. `fit_digits.py` is an offline
NumPy angle-search aid, with an approximate fingertip direction. It is not a mesh
collision test or gameplay verification. Angles were reviewed in real Unity frames.
Run it with this directory as its base; input is the neutral subdirectory.

fit1: individually fitted phalanges; support grip collisions still visible for
RocketLauncher/Cutter. fit2 moved support forward but overreached on launcher;
rejected. fit3 added a rendered-frame boundary before screenshots and reduced
forward shift; front handles still intersect the palm/forearm. Do not ship these
alternative target offsets. Default-source checkpoint keeps original F4 targets
and fitted digit offsets, with the front-handle issue explicitly open.

The initial diagnostic tests each passed one structural walking fixture. This
never certified visual contact. Final source checks are recorded separately.
