# Scratch

Current standing order: grow AquariumSynthCSharp through external reference
targets, not through speculative field sprawl. `state/spine.yaml` and
`docs/reference-synth-roadmap.md` are the handoff surfaces for agents working on
the synth library while Aquarium work continues elsewhere.

Completed this slice:

- Added explicit `PatchParameter` contracts: stable path, label, default, min,
  max, step, unit, automation-rate notes, and notes.
- Added neutral `ReferencePatch`, `ReferenceSource`, and `ReferenceFeature`
  contracts with provenance/license/hash-ready fields.
- Added `param` script declarations and duplicate parameter path validation.
- Updated Faust emission so declared parameters produce smoothed `hslider`
  controls.
- Implemented Option B parameter binding: numeric fields may reference declared
  parameters with `@/path`, for example `lpf=@/macro/brightness`. The parser
  records an exact field binding such as `/voices/0/filter/lpf` and keeps the
  parameter default as the graph value.
- Faust emission substitutes the parameter expression only at the bound field
  site. Unbound parameters still emit a warning.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 23 passed.

Next likely slice:

- Implement a DX7 SysEx parser skeleton for single voices and cartridge payloads.
- Extract operator topology, envelopes, ratios, feedback, levels, LFO, and pitch
  envelope into feature records before trying to translate.
- Rebuild one or two small DX7-style sounds in Aquarium DSL.
- Keep tests focused on structure first, then add rendered audio comparison once
  the render path is explicit.
