# Scratch

Current standing order: keep migrating reusable synth tooling into C# while
leaving only the Rust renderer/runtime and experimental compiler branch behind.

The next likely slice is serialization and host integration:

- Define JSON contracts for `SynthPatch` and related records.
- Add a CLI for `.aqs` script -> `.dsp` / generated C#.
- Add compile cache keyed by script/options/source hash.
- Keep doctrine aligned with realtime constraints before adding runtime-facing
  APIs.
