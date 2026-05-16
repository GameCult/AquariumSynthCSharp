# AquaSynth Instructions

## Purpose

AquaSynth is the C# authoring and compiler front-end for AquaSynth
synth patches. It owns patch graph models, terse patch scripting, SFXR-derived
patch mapping, analysis/scoring tools, preset catalogs, and Faust source/code
generation for the AquaSynth/Vortice engine path.

The AquaSynth-rs branch may remain a reference lab and audio renderer. This repo should
not depend on AquaSynth-rs for reusable authoring, analysis, preset, or Faust
compiler behavior.

## Persistent State

- `state/map.yaml` is the canonical repo map.
- `state/spine.yaml` is the rehydration spine for multi-agent synth library work.
- `state/memory.json` stores durable project taste, doctrine, and decisions.
- `state/scratch.md` is disposable current-slice working context.
- `state/evidence.jsonl` stores short lessons that should change future work.
- `docs/audio-doctrine.md` is the live doctrine for DSP, realtime audio, and
  patch-language design.

Update these when the synth learns something durable. Delete stale guidance
instead of stacking little commemorative plaques in the hallway.

## Operating Doctrine

- Keep the audio work in this repo self-contained. External Faust is a compiler
  target, not an excuse to hide local contracts.
- Authoring code may allocate, parse, search, score, and explain. Generated DSP
  and runtime-facing code must assume realtime rules: no unbounded waits, no
  filesystem, no console I/O, no locks, no surprise allocation.
- Treat patch scripts as a dataflow language: terse surface, explicit graph,
  analyzable AST, deterministic lowering.
- Separate control-rate intent from audio-rate signal work. LFOs, envelopes,
  UI controls, and buses need clear rate semantics even when they compile into
  one Faust expression.
- Favor block-structured reasoning. Buffer-oriented APIs should make block
  size, sample rate, latency, and channel count visible where they matter.
- Keep Faust output boring and inspectable. The DSL may be expressive; emitted
  Faust should be stable, readable enough to debug, and easy to diff.
- Analysis metrics are tools, not judges. Spectral and envelope comparisons
  catch regressions; listening, target references, and gameplay context still
  matter.

## Verification

Use focused checks:

```powershell
dotnet test
```

For compiler-affecting changes, make sure tests cover parse -> patch -> Faust
source. If Faust is installed, validation tests should continue to pass.
