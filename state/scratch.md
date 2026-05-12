# Scratch

Current standing order: grow AquariumSynthCSharp through external reference
targets, not through speculative field sprawl. `state/spine.yaml` and
`docs/reference-synth-roadmap.md` are the handoff surfaces for agents working on
the synth library while Aquarium work continues elsewhere.

Completed this slice:

- Added `ReferenceRebuild` and `ReferenceRebuildCatalog` for explicit
  reference-target rebuild attempts.
- Added two DX7-style Aquarium DSL rebuilds:
  - `dx7/algo32-additive-organ`: algorithm 32's six-carrier additive shape,
    which maps cleanly to six Aquarium voices.
  - `dx7/algo8-bright-pair`: algorithm 8's two-carrier FM shape, which is only
    an approximation because the current voice DSL cannot express
    `6->5`, `4+5->3`, `2->1`, or operator-local self-feedback.
- Added matched/missing feature records so topology mismatch becomes evidence
  instead of being hidden inside prose.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 34 passed.

Previous slice:

- Added DX7 algorithm topology metadata for algorithms 1-32 using Ken
  Shirriff's reverse-engineered OPS algorithm ROM table.
- `Dx7SysEx.AlgorithmTopology` now exposes carrier operators, modulation
  edges, feedback-register writers, direct self-feedback operators,
  delayed-feedback targets, and raw ROM steps.
- `Dx7Voice.Features()` now emits `carrier_operators`,
  `modulation_edge_count`, `feedback_sources`, and
  `self_feedback_operators`.
- Representative tests cover algorithm 8, algorithm 16, and algorithm 32.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 31 passed.

Earlier slice:

- Added `Dx7SysEx` records and parsing for DX7 voice edit buffers, packed
  128-byte voices, and packed 32-voice SysEx banks.
- Extracted six operators, envelopes, algorithm, feedback, oscillator sync,
  pitch envelope, LFO, transpose, and voice name into neutral DX7 records.
- Added `Dx7Voice.ToReferencePatch` and structural feature extraction so DX7
  voices can pressure the reference model before translation exists.
- Checksum validation rejects bad wrapped SysEx payloads.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 28 passed.

Parameter slice:

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

- Decide whether the repeated DX7 algorithm-8 mismatch justifies a first-class
  operator graph model. Map ownership before implementing it.
- Keep tests focused on structure first, then add rendered audio comparison once
  the render path is explicit.
