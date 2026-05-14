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

- Addressed the remaining `ANLGSYN 1` beat as DX7 pitch-LFO pressure, not
  amplitude-LFO pressure. Raw `analog1.syx` bytes show `AMD=71` but every
  operator has `AMS=0`; the active path is `PMD=32`, `PMS=1`, sine LFO speed
  `38`, and delay `33`.
- Added first-class operator-graph vibrato syntax:
  `opgraph ... vibrato=<depth> vibrato_hz=<hz> vibrato_delay=<seconds>`.
  Faust lowering modulates the graph frequency with a faded LFO onset instead
  of a hard delay gate.
- Lowered DX7 pitch LFO for the public-domain probe through that graph vibrato
  surface. Latest `ANLGSYN 1` metrics with the beat present: score `.697885`,
  log-mel `.19496867`, envelope `.15124573`, zero-crossing `.9430519`.
- Tested and cut the earlier operator-tremolo direction for this target. It was
  the wrong authority because ANLGSYN's operator `AMS` values are all zero.

Previous slice:

- Reframed the latest ear report as envelope pressure, not harmonic pressure:
  `{ Mooger }` needs a more aggressive attack contour; `MELLOWSOLO` needed a
  smoother captured tail; `RES SYNTH1` still has a phase-like attack-modulation
  issue even though oscillator sync is enabled.
- Extended only the `MELLOWSOLO` pressure render from `1.0s` to `1.25s` so the
  existing `0.6s` release after a `0.65s` gate is not chopped by the artifact
  boundary. Latest pressure metrics improve to score `.5722256`, log-mel
  `.3472474`, RMS `.9883968`.
- Tested and cut a global traced-release-duration change. It shortened releases
  too aggressively and broke PRC, `Piano Bass`, `RES SYNTH1`, and `ANLGSYN 1`;
  release duration remains part of the broader DX7 envelope model pressure.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  76 passed.

Previous slice:

- Listening split the latest community set again: `Piano Bass` is currently the
  anchor and should not be disturbed; `ANLGSYN 1` and `RES SYNTH1` were blowing
  out into drone at the modulator peak; `{ Mooger }` and `MELLOWSOLO` still
  have overtone-emphasis issues.
- Added scoped modulator peak headroom in the DX7 probe lowering:
  algorithm-2 non-carrier applied envelopes cap only Level1 at `.92`.
  `Piano Bass` remains unchanged; `RES SYNTH1` improves to score `.8140223`,
  log-mel `.12369786`, envelope `.06489321`; `ANLGSYN 1` keeps log-mel
  `.19461302` and sustained high-band evidence but aggregate score drops to
  `.6980632`, so its score gate is now `.69`.
- `MELLOWSOLO` nudges to log-mel `.42906323`, score `.55206907`, but remains a
  pressure artifact, not passing parity.
- Tested and cut a wider summed-route headroom rule for `{ Mooger }`
  (`5.25`/`5.75` for three-source sums). It worsened Mooger log-mel/envelope
  and does not deserve to live. Mooger's "too hard" harmonics remain unresolved
  overtone-emphasis pressure, not a solved high-band-energy problem.
- Focused verification with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore --filter "Dx7SysExTests|PublicDomainDx7MoogerAndPianoBassMeetRenderedParityWhenInstalled|PublicDomainDx7AnlgSyn1KeepsBuzzingModulationWhenInstalled|PublicDomainDx7MellowSoloWritesPressureWavsWhenInstalled|PublicDomainDx7PrcSynth1WritesListeningWavsWhenInstalled"`:
  28 passed.

Previous slice:

- Listening report split the remaining DX7 mismatch: `RES SYNTH1` mostly
  needed a harder attack, `ANLGSYN 1` still smells like missing LFO/operator
  attack behavior, and `MELLOWSOLO` is the clearest harmonic mismatch.
- Fixed two note-dependent DX7 lowering gaps:
  - operator key-scaling now uses the effective played MIDI note after voice
    transpose instead of hardcoded MIDI 60;
  - `Dx7SysEx.OperatorRateScaling` now feeds
    `ApproximateAppliedRateLevelEnvelope`/`TraceInterpolatedEnvelope`.
- Kept graph gain as loudness authority after the scaling change:
  `Piano Bass` moved from `.90` to `.72`.
- Added `PublicDomainDx7MellowSoloWritesPressureWavsWhenInstalled`; it writes
  listening artifacts but only gates as pressure (`log-mel <= .45`, score
  `>= .5`) because it is not passing parity by ear.
- Cut a tempting output-level-dependent envelope trace experiment. It looked
  more Dexed-shaped on paper, but worsened `MELLOWSOLO` and `ANLGSYN 1`, so it
  does not belong in the live machine yet.
- Focused verification with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore --filter "Dx7SysExTests|PublicDomainDx7MoogerAndPianoBassMeetRenderedParityWhenInstalled|PublicDomainDx7AnlgSyn1KeepsBuzzingModulationWhenInstalled|PublicDomainDx7MellowSoloWritesPressureWavsWhenInstalled|PublicDomainDx7PrcSynth1WritesListeningWavsWhenInstalled"`:
  28 passed.

Previous slice:

- Corrected DX7 ROM COM handling after user listening caught that the
  no-compensation probe had closer harmonics across the community patches and
  was mainly too quiet.
- `Dx7SysEx.OperatorOutputCompensation` now returns unity. Loudness recovery
  moved to graph gain instead of hidden carrier boosts.
- Community parity gains with COM disabled:
  `{ Mooger }` `.75`, `Piano Bass` `.90`, `RES SYNTH1` `.75`.
- `RES SYNTH1` is now in the passing community gate. Latest metrics:
  score `.7277295`, log-mel `.1461968`, envelope `.10764466`, RMS `.941083`,
  zero-crossing `.1.1022931`.
- `DX1 LEAD B` and `MELLOWSOLO` sound closer by ear with COM disabled and
  graph gain restored, but still fail numeric gates. Latest survey:
  `DX1 LEAD B` score `.45156077`, log-mel `.43533683`, RMS `.8193191`;
  `MELLOWSOLO` score `.5359293`, log-mel `.39420125`, RMS `.99559265`.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  74 passed.

Previous slice:

- Fixed the remaining `ANLGSYN 1` drift that listening caught after the
  conditional output-clip pass. The old candidate still lost 2.5-5 kHz energy
  midway through the note.
- Added `sustainFloor` to `Dx7SysEx.ApproximateAppliedRateLevelEnvelope` and
  uses a `.9` floor for max-feedback source operators in the DX7 probe
  lowering. This keeps the self-feedback source driving the loop after the
  attack instead of sagging into `deeooh`.
- Added a sustained 2.5-5 kHz band-energy parity gate for `ANLGSYN 1`; the old
  candidate was around `.175` at the sustained check, while the new lowering is
  above `1.0`.
- Raised `Piano Bass` graph gain to `.30`; latest metrics: score `.7552858`,
  log-mel `.16204594`, envelope `.06413663`, RMS `.9909909`.
- Fresh `ANLGSYN 1`: score `.7245976`, log-mel `.18460114`, envelope
  `.119950555`, RMS `.97143364`, zero-crossing `.97149533`.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  74 passed.

Earlier slice:

- Tightened `ANLGSYN 1` after listening caught the `deeooh` failure: the
  max-feedback candidate had a better attack but lost sustained buzz midway.
- Added conditional output nonlinearity in the DX7 probe lowering:
  `soft_clip=true` only when a voice has DX7 feedback `7` and a self-feedback
  topology. PRC stays `soft_clip=false`.
- Fresh `ANLGSYN 1`: score `.70946133`, log-mel `.18456098`, envelope
  `.10628832`, RMS `.9816304`, zero-crossing `.8657629`, centroid `.95726264`.
- Fresh PRC after the conditional clip gate: score `.7546429`, log-mel
  `.13874851`, envelope `.067291394`, RMS `.99121606`, zero-crossing
  `.95325506`.

Earlier slice:

- Retuned DX7 max-feedback lowering for `ANLGSYN 1`. Route-index sweeps did not
  materially improve the missing high-band buzz; hotter feedback did.
- Changed only feedback value `7` from `.66` to `2.2`. Feedback value `5`
  remains `.19`, so the hard PRC target keeps its existing feedback amount.
- Fresh `ANLGSYN 1` with `feedback=2.2`: log-mel `.17553389`, envelope
  `.16478956`, zero-crossing `.90904`, centroid `.9519112`, score `.6448498`.
  This improves spectral buzz while making envelope/RMS less tidy.
- Band evidence versus the previous fixed-frequency candidate:
  - 1.2-2.5 kHz candidate/reference energy: `.008 -> .656`
  - 2.5-5 kHz candidate/reference energy: near zero -> `.321`
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  73 passed.

Earlier slice:

- Fixed DX7 fixed-frequency operator lowering after `ANLGSYN 1` exposed the
  missing buzzing modulation. The old lowering treated fixed mode as a fake
  note ratio, so fixed carriers op1/op3 became `0.5` ratio against the graph
  frequency. Dexed treats fixed mode as absolute Hz.
- Added `Dx7SysEx.FixedOperatorFrequencyHz` using Dexed's fixed-mode
  log-frequency formula and changed `OperatorFrequencyRatio` to return
  `fixedHz / graphFrequency` for fixed operators.
- Fresh `ANLGSYN 1` candidate now lowers fixed op3 to ratio `.009227` and op1
  to `.007615` against `freq=130.8128`, instead of flattening both to `.5`.
- Added `PublicDomainDx7AnlgSyn1KeepsBuzzingModulationWhenInstalled` with
  focused gates. Latest metrics: log-mel `.19427659`, envelope `.14924917`,
  zero-crossing `.94662774`, centroid `.9182808`, score `.659334`.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  73 passed.

Previous slice:

- Fixed the `Piano Bass` octave bug. The parsed voice has `transpose=12`; the
  old generated Aquarium script hardcoded `freq=261.6256`, so it rendered an
  octave too high. `Dx7SysEx.NoteFrequencyHz(midiNote, transpose)` now treats
  transpose `24` as neutral and transpose `12` as one octave down.
- `Dx7VoiceProbeScript` now uses the voice transpose for graph frequency and
  for the detune note basis. Fresh `Piano Bass` artifact emits `freq=130.8128`.
- Narrowed the community parity gate to the two voices we can currently defend:
  `{ Mooger }` and fixed `Piano Bass`. `ANLGSYN 1`, `RES SYNTH1`, `DX1 LEAD B`,
  and `MELLOWSOLO` remain pressure, not passing parity.
- Fixed `Piano Bass` metrics: log-mel `.16180529`, envelope `.13544041`,
  zero-crossing `.905511`, score `.64267576`.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  71 passed.

Previous slice:

- Added `PublicDomainDx7AnalogCommunityVoicesMeetBroadRenderedParityWhenInstalled`,
  which renders actual public-domain `analog1.syx` community voices through
  Dexed and the current Aquarium DX7 lowering. Kept four broad parity voices:
  `ANLGSYN 1`, `{ Mooger }`, `Piano Bass`, and `RES SYNTH1`.
- The test writes WAVs/reports under
  `artifacts/parity/dx7-community-analog1/<voice>/` and gates log-mel
  `<= .3`, envelope distance `<= .16`, and score `>= .5`.
- Latest kept-voice metrics:
  - `ANLGSYN 1`: log-mel `.25955316`, envelope `.12271979`, score `.5709625`
  - `{ Mooger }`: log-mel `.23776375`, envelope `.07168135`, score `.66662127`
  - `Piano Bass`: log-mel `.19985504`, envelope `.14781862`, score `.5205584`
  - `RES SYNTH1`: log-mel `.22371109`, envelope `.109231755`, score `.54040927`
- First survey also tried `DX1 LEAD B` and `MELLOWSOLO`; they were excluded
  from the passing gate because log-mel and zero-crossing mismatch were too
  large. That is pressure, not library stock.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  70 passed.

Previous slice:

- Added two split project-authored DX7 algorithm-8 rendered parity probes:
  `ProjectAuthoredDx7AlgorithmEightCascadeProbeMeetsParityWhenInstalled` for
  `6 -> 5 -> 3`, and
  `ProjectAuthoredDx7AlgorithmEightSummedPairProbeMeetsParityWhenInstalled`
  for `4 + 5 -> 3`.
- Tightened the existing combined algorithm-8 stack gate now that real metrics
  are known. The project-authored probes gate log-mel at `<= .06`; latest real
  Dexed run:
  - combined stack: log-mel `0.04029555`, score `0.8664546`
  - summed pair: log-mel `0.042429477`, score `0.870778`
  - cascade: log-mel `0.03811625`, score `0.9157809`
- Project-authored DX7 parity tests now write listening WAVs and reports under
  `artifacts/parity/dx7-project-authored/<probe>/`. These split probes are
  intentionally not part of the patch library: they are useful measuring tools,
  not pleasant or reusable stock.
- Verified with bundled Python/dexed-py:
  `AQUARIUM_DX7_PYTHON=<bundled python> dotnet test AquariumSynthCSharp.slnx --no-restore`:
  69 passed.

Previous slice:

- Added a structured `.aqua` patch library under `patches/` with:
  `examples/`, `sfxr/`, `bfxr/`, `808/`, `fm-bell/`, `wobble-bass/`,
  `dx7/`, and `advanced/`.
- Added `patches/library.yaml` as the machine-readable index and
  `patches/README.md` as the folder contract. The library is development
  source material for stock, reference rebuilds, calibration probes, and patch
  inspiration, not a shipped package surface.
- Exported the existing built-in/reference catalog into `.aqua` files and added
  the calibrated `PRC SYNTH1` hard DX7 candidate at
  `patches/dx7/public-domain/prc-synth1-calibrated.aqua`.
- Added a test contract: every `.aqua` under `patches/` must parse through
  `PatchScript.Parse` and export Faust, while package-boundary tests keep
  `patches/` and `.aqua` files out of the NuGet package.
- Verified with `dotnet test AquariumSynthCSharp.slnx --no-restore`: 67 passed.

Previous slice:

- Increased `Dx7SysEx.SummedOperatorModulationRouteIndex` from `1.6` to `6.0`.
  This targets the missing harsh stacked-modulation brightness without adding a
  generic drive/overdrive knob. The independent project-authored algorithm-8
  summed-stack probe now uses graph gain `.218` and gates log-mel at `<= .12`
  with RMS normalization.
- Latest hard `PRC SYNTH1` after stronger stack scale and graph gain `.25`:
  log-mel `0.13874426`, envelope distance `0.067291506`, RMS ratio
  `0.9912135`, zero-crossing ratio `0.95325506`, centroid ratio `1.1156512`,
  score `0.7546445`.
- Added `Dx7SysEx.ApproximateAppliedRateLevelEnvelope`, which traces the
  Dexed-style block-interpolated applied EG gain and lowers it into Aquarium's
  curved staged envelope surface, normalized so operator output level remains a
  separate authority.
- Retried hard `PRC SYNTH1` with the applied-envelope lowering. Latest focused
  run after fixing the peak picker: log-mel `0.17186502` (was `0.24476814`),
  envelope distance `0.13057204` (was `0.43700194`), RMS ratio `1.0000212`,
  zero-crossing ratio `0.7815517`, score `0.6501268`.
- Listening note resolved: the prior applied-envelope lowering made op2 rise
  late (`0.014 -> 0.613`), causing the odd end lilt. The helper now finds the
  first near-peak before gate, so op2 hits early and decays
  (`0.981 -> 0.843 -> 0.660`) like the traced DX7 applied gain.
- Extended staged operator envelopes with per-segment curves:
  `curves=lin,exp,exp,lin`. Linear remains the default, and levels can now
  express deliberate transient overshoot above `1` instead of clipping away
  the DX7 evidence.
- Added `Dx7SysEx.TraceEnvelope`, a DX7 EG microscope that follows the internal
  rate/level state machine closely enough to expose gain and stage over time.
- Added `Dx7SysEx.TraceInterpolatedEnvelope`, which traces the gain actually
  applied by Dexed-style operator rendering: one EG sample per 64-sample block,
  linearly interpolated across the block.
- Added an envelope comparison artifact test that writes
  `artifacts/parity/dx7-envelope-trace/egstep.csv`. The first rows show both
  DX7 raw state and applied gain: raw jumps to `2` immediately, while applied
  gain ramps from `.03125` to `2` over the first 64-sample block and remains
  near `1` around 20 ms. Aquarium `env=rl` is a different contour entirely.
  The artifact now also includes a curved Aquarium staged-envelope candidate
  that tracks the applied-gain contour far more closely.
- Tried adding a first `env=dx7` runtime lowering, then cut it. The trace
  matched the Python `graph.py` envelope but not Dexed plugin audio, so the
  syntax was not allowed to survive. The durable result is the microscope and
  the applied-gain trace, not a half-proven model.

Previous slice:

- Added test-only project-authored Dexed patch rendering through
  `DexedPyRenderer.RenderPatchAsync`. It builds a `dexed.Patch` from explicit
  operator specs and renders it without adding any shipped fixture or package
  surface.
- Added an independent project-authored algorithm-8 summed-stack parity test.
  This backs the topology-aware route lowering outside the public-domain PRC
  patch, so `SummedOperatorModulationRouteIndex` is no longer only PRC-shaped
  evidence.
- Cut the attempted DX7 EG exponential level curve again. On an isolated
  envelope target, exponential EG levels plus a shorter timing scale improved
  shape versus linear levels, but PRC still failed log-mel (`0.2857573` at a
  tuned timing scale). The live lowering stays linear for EG levels until an
  isolated envelope target passes without harming the hard target.

Previous slice:

- Moved the remaining PRC cascaded-route scalar into a topology-aware DX7 route
  helper. `Dx7SysEx.OperatorRouteIndex(topology, edge)` now keeps the isolated
  two-op route scale (`6.275`) for standalone direct branches and uses the
  summed/cascaded scale (`1.6`) for sum edges and direct edges feeding a sum.
- The PRC probe no longer owns any private route-index function. Latest focused
  run still passes with log-mel `0.24476814`, score `0.5708394`, RMS ratio
  `1.0108943`.

Previous slice:

- Added the ratio-detune and algorithm-output-compensation rungs to the DX7
  lowering. `Dx7SysEx.OperatorFrequencyRatio` now applies ratio-mode detune
  from the Dexed/DX7 note formula, and `Dx7SysEx.OperatorOutputCompensation`
  maps ROM `COM` values relative to the algorithm-32 six-output baseline.
- The PRC probe no longer has a carrier-level scale table. It still has a
  fenced cascaded-route constant (`1.6`) for the `op6/op5/op4 -> op3` stack;
  that is now named as the next calibration target, not promoted into core
  lowering.
- Latest PRC run with detune, COM compensation, graph gain `.39`, and the
  fenced cascaded-route probe: log-mel `0.24476814`, score `0.5708394`, RMS
  ratio `1.0108943`, zero-crossing ratio `0.7931764`, centroid ratio
  `1.1131053`.

Previous slice:

- Completed the feedback calibration rung. Isolated Dexed feedback sweeps fit a
  nonlinear Aquarium feedback table: `0, .01, .02, .05, .10, .19, .38, .66`
  for DX7 feedback values `0..7`.
- Added `Dx7SysEx.OperatorFeedbackAmount` and moved the PRC feedback lowering
  off the old `voice.Feedback * 0.04` constant. PRC barely changed because
  feedback value `5` maps to `.19`, close to the previous accidental `.20`.
  The win is ownership: feedback scaling is no longer folklore.

Previous slice:

- Completed the second calibration rung for isolated two-operator FM. A Dexed
  sweep fits full-scale DX7 modulation at about `12.55` radians. Given the
  current Aquarium Faust formula, that maps to an Aquarium route index of
  `6.275` for a full-level modulator.
- Added `Dx7SysEx.OperatorModulationRouteIndex` and a regression test for the
  calibrated phase-deviation scale. The hard PRC probe applies that scale only
  to the isolated `op2 -> op1` branch; applying it blindly to the cascaded
  `op6/op5/op4 -> op3` stack made PRC worse, which confirms algorithm output
  compensation is a separate rung.
- Latest PRC run with the carrier curve plus isolated-route calibration:
  log-mel `0.2526254`, score `0.44758993`, duration ratio `0.9927914`, RMS
  ratio `0.8931149`. The log-mel gate is back to `<= 0.255`.

Previous slice:

- Wrote `docs/dx7-calibration-plan.md` and referenced it from memory/spine.
  The ladder is now: single-carrier amplitude, two-op modulation index,
  feedback scaling, envelope level curve, algorithm output compensation, then
  hard PRC replay.
- Completed the first calibration rung. A project-authored Dexed single-carrier
  output-level sweep fits `2^((outputLevel - 99) / 8)` for carrier amplitude.
  `Dx7SysEx.OperatorOutputAmplitude` now owns that curve, and
  `ApproximateOperatorLevel` uses it instead of the old internal-level proxy.
- Added tests for the measured output-level curve. The hard PRC probe now gates
  log-mel at `<= 0.26` while the next rung calibrates modulation index; latest
  run after the carrier fix: log-mel `0.2562075`, score `0.40430218`.
- Calibration lesson: the old operator level helper was not a harmless
  approximation. It made mid-level operators far too loud and let PRC-specific
  constants compensate for a broken foundation. Next pressure is route/index
  scaling for isolated two-operator FM.

Previous slice:

- Reoriented the `PRC SYNTH1` hard probe around the perceptual metric that
  matched listening: log-mel distance. The probe now asserts log-mel
  `<= 0.255` plus a loose aggregate score floor instead of treating aggregate
  score as the main judge.
- Spectral diagnosis showed the Dexed reference has dominant peaks around
  `393`, `131`, `524`, and `656` Hz. The prior Aquarium candidate had too
  little `80-160` Hz body when based at `392`, while the corrected DX7 note
  basis restored the low body but exposed weak mid harmonic structure and high
  centroid/zero-crossing mismatch.
- Current run with bundled Python and Faust: log-mel distance `0.25093183`,
  score `0.4135618`, envelope distance `0.3595946`, duration ratio
  `0.992578`, RMS ratio `1.0658501`, zero-crossing ratio `0.24567787`,
  centroid ratio `1.3291793`.
- Calibration lesson: the hard target is now a spectral calibration problem.
  DX7 output/index scaling still needs a real model; aggregate score can move
  in the wrong direction while log-mel and the ear move in the right direction.

Previous slice:

- Tightened the hard DX7 `PRC SYNTH1` probe from a "writes WAVs" smoke test to
  a modest passing parity target: threshold now requires score >= `0.60`.
- The useful cuts were not new syntax. The candidate improved by removing
  patch soft clipping, setting the graph base frequency to `392`, and boosting
  the `op3` carrier branch to compensate the algorithm-8 output/body balance.
  Global detune and per-edge route damping both made the score worse and were
  cut.
- Current best run with bundled Python and Faust writes timestamped artifacts
  under `artifacts/parity/dx7-prc-synth1/<run>/`: score `0.6057491`,
  log-mel distance `0.27658066`, envelope distance `0.38489524`, duration
  ratio `0.98714787`, RMS ratio `0.9756519`, zero-crossing ratio `1.0458903`,
  centroid ratio `1.093785`.

Previous slice:

- Added a hard DX7 `PRC SYNTH1` rendered parity probe that writes listening
  artifacts when `dexed-py` and Faust are available:
  `artifacts/parity/dx7-prc-synth1/reference-dexed.wav`,
  `candidate-aquarium.wav`, `candidate.aqua`, and `report.txt`.
- The probe rebuilds `analog1.syx` voice 17 with readable operator graph syntax
  and staged `env=rl` operator envelopes. It asserts a modest hard-target score
  floor instead of pretending exact DX7 parity is solved.
- Current run with bundled Python and Faust: score `0.4581856`,
  log-mel distance `0.26397973`, envelope distance `0.4109424`,
  duration ratio `1.0173812`, RMS ratio `0.61393267`, zero-crossing ratio
  `1.1713804`, centroid ratio `1.0978767`.
- Calibration lesson: shortening the naive DX7 EG timing and lowering the graph
  base frequency made the candidate much closer. The remaining mismatch is
  still gain/timbre calibration, not missing `env=rl` syntax.

Previous slice:

- Added staged operator envelopes for operator graphs:
  `env=rl rates=.004,.12,.2,.4 levels=1,.7,.25,0`. This gives the DSL a
  readable four-stage rate/level contour without infecting the general ADSR
  voice envelope model.
- Added `RateLevelEnvelope` to the operator model, parser support for readable
  and compact `env=rl` syntax, Faust `rl4_env(...)` rendering, and a DX7 helper
  that emits staged envelope script specs from four-rate/four-level operator EG
  data.
- Verified the surface structurally and through the render path:
  `dotnet test AquariumSynthCSharp.slnx --no-restore`: 53 passed.
- Retried the hard `PRC SYNTH1` probe with direct DX7 rate/level lowering. The
  best quick score was only ~0.316, worse than the hand-tweaked ADSR candidate.
  The DSL can now express the missing contour shape, but DX7 EG timing/gain
  calibration remains the next pressure.

Previous slice:

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
