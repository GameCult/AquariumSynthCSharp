# Project-Authored ZynAddSubFX Fixtures

These `.xiz` files are tiny Aquarium-authored XML fixtures shaped like
ZynAddSubFX instrument files. They are not copied from an upstream Zyn bank.

Purpose:

- exercise Zyn instrument feature extraction without vendoring unclear preset
  licenses;
- keep the first Zyn slice focused on structural classification, not audio
translation;
- preserve a clean NuGet boundary. Test fixtures are development material only.

Current fixtures:

- `additive-lead.xiz`
- `pad-pure.xiz`
- `pad-texture.xiz`
- `pad-harmonic.xiz`
- `vocal-layer.xiz`

License: project-authored for AquariumSynthCSharp tests.
