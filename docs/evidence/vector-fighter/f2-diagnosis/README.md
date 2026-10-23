# F2 grip diagnosis

All images are actual Unity renders of F1 assets during the F2 runtime correction.
- before: user's defect, palm middle proximal x=0.167913 (grip face x=0.055). Existing walk test passes despite visual defect.
- rejected: static mount reset after sampling displaced the gun and doubled the relative wrist correction; visual FAIL although two older tests passed.
- corrected: restore additive wrist/mount state before graph sampling, preserve actual sampled weapon world transform; palm x=0.080115 and distal x=-0.042343. Two existing checks pass.
Final F2 assets and expanded contact/fire/death checks are recorded separately. No diagnostic image is final acceptance evidence.
