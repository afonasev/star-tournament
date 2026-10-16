# Draft discard warning06

Approved mockup: docs/design/template-standard/06-draft-warning.png. Runtime implementation: ProvingGroundDesignLab. This slice addresses LAB-DRAFT-02; it does not complete the entire Lab child.

Warning lists every changed visible parameter independently of current search/group, including invalid raw input. It shows group/field and baseline → draft values, keeps actions outside the scroll region, focuses Continue editing first and reveals keyboard/gamepad-focused rows. Cancel retains draft/saved/active/file bytes. Confirm resets the draft then invokes the original route. Clear and Delete now pass the same guard; deleting a profile still requires its separate destructive confirmation after discard. Existing profile/version selection, menu back and application quit share this warning. Saved versions and gameplay contracts are unchanged.

QA commands, exact route and selection reasons: qa-scope.md. First Null Device run failed1/4; its PNGs are blank and retained only as failed diagnostics. Graphics-enabled reruns retain all assertions and render actual Unity UI. Screenshots are Editor evidence, not native Player or human/device acceptance. No Player build, installer, full suite or publication.

The requirements-review.md file covers all28 pinned Lab IDs and records remaining dev/project Git, release flags/Default/reset, migration/default completion and05 UX work. Do not mark Lab complete from this warning slice.
