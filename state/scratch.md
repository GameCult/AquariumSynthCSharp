# Scratch

Current standing order: grow AquariumSynthCSharp through external reference
targets, not through speculative field sprawl. `state/spine.yaml` and
`docs/reference-synth-roadmap.md` are the handoff surfaces for agents working on
the synth library while Aquarium work continues elsewhere.

Doctrine update:

- External synths are parity pressure for Aquarium's DSL, not internals to
  clone. Only consume targets that can be backed by parity tests proving
  Aquarium can reproduce the behavior in terse, readable syntax.
- Parsing a synth format is inventory. Rebuilding and testing behavior is the
  proof.

Completed this slice:

- Added `Dx7OperatorLevelApproximation`, which distills DX7 operator output
  level plus key/velocity scaling into a normal Aquarium `level=`-style value.
  This remains DX7 reference-import knowledge, not a generic DSL feature.
- Retried `PRC SYNTH1` with effective levels and envelope tweaks. Static level
  mapping alone did not rescue the hard target: quick probes moved from ~0.24
  to ~0.34, and envelope tweaks reached ~0.42. The next missing pressure is
  DX7-style operator envelope/gain evolution, not just static operator levels.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 50 passed.

Previous slice:

- Tried the harder `analog1.syx` voice 17, `PRC SYNTH1` (algorithm 8). The
  initial routed Aquarium candidate scored badly (~0.23), which exposed a real
  missing invariant rather than a threshold-tuning problem.
- Reworked operator feedback emission from a cyclic smoothed self-reference to
  a renderable Faust feedback expression using delayed recursion. Added
  `FaustCompilerRendersOperatorFeedbackWhenInstalled`.
- Exact DX7 feedback scaling and EG behavior are still not solved, but feedback
  no longer makes the Faust render path fall over.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 49 passed.

Previous slice:

- Added the first thresholded DX7 rendered-audio parity rebuild:
  `BuiltInScripts.Dx7StylePublicDomainMcMm53` maps public-domain
  `analog1.syx` voice 13, `MC-MM 5-3`, to a terse Aquarium sine patch.
- Added `PublicDomainDx7McMm53MeetsFirstRenderedParityThresholdWhenInstalled`,
  which renders the DX7 reference through `dexed-py`, renders the Aquarium
  candidate through Faust-generated C#, and asserts score, log-mel distance,
  envelope distance, duration/RMS ratios, zero-crossing ratio, and centroid
  ratio.
- This is behavioral parity for a simple sine-like rendered voice, not exact
  DX7 operator execution. The next DX7 pressure should be a harder voice that
  audibly needs the operator graph.
- Verified with bundled Python plus `dexed-py`: parity test passed.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 48 passed.

Previous slice:

- Added a public-domain DX7 SysEx fixture from Musical Artifacts artifact 152:
  `tests/AquariumSynth.Dsl.Tests/Fixtures/Dx7/PublicDomain/analog1.syx`, with
  provenance and SHA-256 recorded beside it.
- Added a test-only `dexed-py` reference renderer. It uses
  `AQUARIUM_DX7_PYTHON` when set, otherwise probes `py`, `python`, and
  `python3`; if `dexed-py` is absent, the render test returns without turning
  optional tooling into a hard dependency.
- Added a `.nupkg` boundary test that packs `AquariumSynth.Dsl` and asserts
  test fixtures, SysEx banks, and Python helpers are not shipped.
- Verified with bundled Python plus `dexed-py`: the DX7 fixture renders through
  Dexed successfully.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 47 passed.

Previous slice:

- Added `FaustCompiler.RenderAsync`, which compiles Aquarium Faust output to
  Faust-generated C#, runs it in a temp .NET project, and returns a mono float
  sample buffer for analysis.
- Added a render test that proves a generated Aquarium patch produces non-silent
  audio and can be compared through `AudioAnalyzer`.
- DX7 audio parity is still not claimed: the candidate renderer exists, but the
  reference side needs Dexed output or a captured licensed fixture.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 44 passed.

Previous slice:

- Added `Dx7EnvelopeApproximation`, which lowers a DX7 four-rate/four-level EG
  to a labeled Aquarium ADSR approximation plus gate duration.
- Reference rebuilds now record `operator_envelope_approximation` as matched
  pressure and `operator_envelope_exactness` as still missing.
- Doctrine now states that DX7 EG approximation is not exact DX7 envelope
  execution.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 43 passed.

Previous slice:

- Added field-site parameter binding for readable operator graph authoring:
  graph gain/note fields, operator ratio/level/feedback/envelope fields, and
  route index fields now accept `@/param`.
- Operator graph Faust emission now substitutes bound parameter expressions at
  the exact `/opgraphs/...` field paths.
- Fixed AD operator envelope binding to use `/env/decay` for the second AD
  value, matching the ADSR model instead of calling it release by accident.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 42 passed.

Previous slice:

- Added a patch-level `Playback` contract for `OneShot`, `Mono`, and `Poly`
  playback with Faust MIDI polyphony settings.
- `instrument midi=true polyphony=8` now lowers to Faust's standard
  `[midi:on][nvoices:8]` option and `freq`, `gain`, `gate` controls.
- Host/MIDI playback no longer emits per-voice `/voices/0/note/frequency` and
  `/voices/0/note/gate` controls; Faust architecture owns allocation.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 41 passed.

Previous slice:

- Split note timing from envelope shape. `Note` now owns frequency, one-shot
  gate duration, and host/MIDI source; `Envelope` now owns ADSR shape:
  attack, decay, sustain level, and release.
- SFXR sustain duration now maps to `Note.GateSeconds`; SFXR punch maps to a
  lower ADSR sustain level plus voice gain compensation during import/legacy
  parsing.
- Added host note mode for MIDI-oriented patches through stable note frequency
  and note gate controls in generated Faust.
- Moved built-in authoring examples off `punch=` and onto `sustain_level=`.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 40 passed.

Previous slice:

- Replaced the DX7 algorithm-8 authoring surface with readable operator graph
  syntax: `operator`, `route`, and `carrier` declarations.
- Added `env=ad:attack:decay` and `env=adsr:attack:decay:sustain:release`
  envelope forms for operator declarations.
- Kept compact `ops=`/`edges=` syntax as parser/interchange scaffolding, but it
  is no longer the built-in authoring example.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 36 passed.

Previous slice:

- Added a first-class `OperatorGraph` model with operators, modulation edges,
  carriers, graph gain, and operator feedback.
- Added `opgraph` patch-script syntax. Example:
  `opgraph name=core freq=330 carriers=1,3 ops=6:4:.9,5:3:.8,4:2:.7:.18,3:2:.6,2:1:.75,1:1:.82 edges=6>5:1.1,5>3:.9,4>3:.75,2>1:.85`
- Faust emission now mixes operator graphs alongside normal voices.
- Updated the DX7 algorithm-8 rebuild to use a real operator graph for the
  topology. The remaining missing feature is exact DX7 feedback-register timing
  and DX7 rate/level envelopes, not graph ownership.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 35 passed.

Earlier rebuild slice:

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

Earlier topology slice:

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

- Refine operator graphs with parameter bindings inside graph fields and decide
  how host MIDI note age/retrigger semantics should be represented beyond the
  current frequency/gate controls.
- Keep tests focused on structure first, then add rendered audio comparison once
  the render path is explicit.
