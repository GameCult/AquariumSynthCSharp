# Migration Checklist

This repo is now the authoring-side home for Aquarium Synth patch scripts and
Faust generation. The Rust crate remains useful as a reference lab, but the
current C# surface should not depend on somebody remembering what got built
there.

## Ported

- Patch graph model: voices, envelopes, pitch motion, duty motion, filters,
  phasers, arpeggios, FM, color shaping, formants, repeat, and control lanes.
- Patch script parser: `patch`, `default`, `def`, `voice`, `mod`/`bus`, and
  `lfo`/`control`, including field-only continuation lines for readable
  multi-line patches.
- DSL shorthand aliases kept for authoring convenience, including `p`, `d`,
  `v`, `u`, `w`, `f`, `g`, `s`, `pr`, `pdr`, `du`, `dur`, `l`, `h`, `drv`,
  `fl`, `fmix`, `fmi`, `mods`, and `m`.
- Modulation routing in both direct style (`mod n=wob g=.4 l=.5`) and routed
  style (`bus n=sway to=g:.1,l:-.2,fmix:.3`).
- Built-in reference script catalog for classic SFXR, BFXR-flavored effects,
  classic 808, FM bell, wobble bass, and advanced layered patches.
- Abstract SFXR reference script using defaults and templates.
- SFXR parameter presets, mutation, shorthand atoms, and SFXR-to-patch mapping.
- Aquarium preset patches: pluck, heartbeat, voice, and SFXR named bridge.
- Patch script scoring: terse, readability, and balanced metrics.
- Audio analysis and comparison: envelope, log-mel spectrogram, spectral
  features, distance metrics, and score.
- Faust source emitter and installed-compiler wrapper for C, C++, C#, and Rust
  target code generation.
- Tests that parse/export all built-in reference scripts and validate Faust
  source when Faust is installed.

## Still Deliberate

- The Rust sample renderer, patch player, and realtime audio-unit bridge stay in
  Rust unless we decide to write a real C#/Faust runtime wrapper.
- The experimental Rust-side DSL compiler branch can stay behind as reference
  machinery. The C# repo owns the script surface needed to drive Faust.
- Exhaustive Rust-renderer-vs-Faust audio parity stays with the Rust renderer.
  C# can compare buffers it is handed, but it should not recreate the old
  renderer just to prove the old renderer existed. That road has a sign on it.

## Next Pull

- Add JSON serialization contracts for `SynthPatch` once the Vortice-side load
  boundary is known.
- Add a Faust compilation cache keyed by script/options/source hash.
- Add a small CLI so build scripts can compile `.aqs` patch files into Faust or
  generated C# without writing host code first.
