# Migration Checklist

This repo is now the authoring-side home for Aquarium Synth patch scripts and
Faust generation. The Rust crate remains useful as a reference lab, but the
current C# surface should not depend on somebody remembering what got built
there.

## Ported

- Patch graph model: voices, envelopes, pitch motion, duty motion, filters,
  phasers, arpeggios, FM, color shaping, formants, repeat, and control lanes.
- Patch script parser: `patch`, `default`, `def`, `voice`, `mod`/`bus`, and
  `lfo`/`control`.
- Rust DSL shorthand aliases used by the golfed scripts, including `p`, `d`,
  `v`, `u`, `w`, `f`, `g`, `s`, `pr`, `pdr`, `du`, `dur`, `l`, `h`, `drv`,
  `fl`, `fmix`, `fmi`, `mods`, and `m`.
- Modulation routing in both direct style (`mod n=wob g=.4 l=.5`) and routed
  style (`bus n=sway to=g:.1,l:-.2,fmix:.3`).
- Built-in script catalog for classic SFXR, classic 808, FM bell, and wobble
  bass primitive golf scripts.
- Abstract SFXR golf script using defaults and templates.
- Faust source emitter and installed-compiler wrapper for C, C++, C#, and Rust
  target code generation.
- Tests that parse/export all built-in primitive scripts and validate Faust
  source when Faust is installed.

## Still Deliberate

- The C# repo does not yet include an audio renderer, analysis metric suite, or
  exhaustive Rust-vs-Faust audio parity harness. Faust is the renderer target,
  so C# should own authoring and validation first.
- SFXR preset atoms such as `sfxr preset=laser mutate_seed=...` are not carried
  over yet. The current migration preserves the primitive patch construction
  path, which is the part we want in the engine.
- Random mutation, parameter-space search, and readability/code-golf scoring are
  still Rust-reference features until we decide which ones belong in C# tools
  versus offline authoring utilities.

## Next Pull

- Add JSON serialization contracts for `SynthPatch` once the Vortice-side load
  boundary is known.
- Add a Faust compilation cache keyed by script/options/source hash.
- Add a small CLI so build scripts can compile `.aqs` patch files into Faust or
  generated C# without writing host code first.
