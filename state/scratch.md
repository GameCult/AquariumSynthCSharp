# Scratch

Current standing order: grow AquariumSynthCSharp through external reference
targets, not through speculative field sprawl. `state/spine.yaml` and
`docs/reference-synth-roadmap.md` are the handoff surfaces for agents working on
the synth library while Aquarium work continues elsewhere.

Completed this slice:

- Added `Dx7SysEx` records and parsing for DX7 voice edit buffers, packed
  128-byte voices, and packed 32-voice SysEx banks.
- Extracted six operators, envelopes, algorithm, feedback, oscillator sync,
  pitch envelope, LFO, transpose, and voice name into neutral DX7 records.
- Added `Dx7Voice.ToReferencePatch` and structural feature extraction so DX7
  voices can pressure the reference model before translation exists.
- Checksum validation rejects bad wrapped SysEx payloads.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 28 passed.

Previous slice:

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

- Add explicit DX7 algorithm topology metadata: carrier operators, modulator
  edges, and feedback edge location for algorithms 1-32. Keep it as reference
  topology data, not Aquarium graph translation yet.
- Rebuild one or two small DX7-style sounds in Aquarium DSL.
- Keep tests focused on structure first, then add rendered audio comparison once
  the render path is explicit.
