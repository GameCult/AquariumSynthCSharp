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
  controls. Target binding is not implemented yet and emits a warning; do not
  pretend the parameter value shapes sound until binding exists.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 20 passed.

Next likely slice:

- Decide whether `param` should bind to explicit semantic targets such as
  `target=lpf` or whether fields should accept parameter references such as
  `lpf=@/macro/brightness`. Do not add both without a clear ownership model.
- Implement a DX7 SysEx parser skeleton for single voices and cartridge payloads.
- Extract operator topology, envelopes, ratios, feedback, levels, LFO, and pitch
  envelope into feature records before trying to translate.
- Rebuild one or two small DX7-style sounds in Aquarium DSL.
- Keep tests focused on structure first, then add rendered audio comparison once
  the render path is explicit.
