# ZynAddSubFX Pressure Survey

## Objective

Find the ZynAddSubFX instruments most likely to break AquaSynth's current patch
language before spending time on syntax polish.

This is a feature-pressure survey, not an audio parity claim. The upstream Zyn
instrument bank is GPL reference feedstock through the pinned Zyn submodule
tree. It is development/test material only: useful for reports, parity renders,
and pressure selection, never package content or AquaSynth patch-library stock.

## Source Checked

- Repository: `https://github.com/zynaddsubfx/instruments`
- Observed commit: `c5c912131b31df5fdf372d2f06a25aaf2375837f`
- Files scanned: `1318` `.xiz` instruments
- Reader result after hardening: `1318` parsed, `0` errors
- Local path: `external/zynaddsubfx/instruments/banks`

## Scoring

`ZynInstrumentSurvey.ComplexityScore` ranks instruments by enabled kit items,
layering, active ADD/SUB/PAD engines, envelope count, free-envelope count, LFO
count, filter count, formant-filter count, and effect count.

The score is deliberately crude. It is a triage knife, not a musicological
thesis.

## Top Pressure Targets

| Score | Path | Name | Why It Hurts |
| ---: | --- | --- | --- |
| 666 | `olivers-100/0032-Drum Kit.xiz` | Drum Kit | 16 enabled kit items, ADD/SUB/PAD mixed engines, 106 envelopes, 25 free envelopes, 73 LFOs, 37 filters, 3 formant filters. This is the named-layer/kit-routing boss fight. |
| 398 | `Companion/0056-Wide Bass.xiz` | Wide Bass | Only 2 enabled kit items, but huge modulation/formant pressure: 86 envelopes, 54 LFOs, 18 filters, 18 formant filters. This is vowel/formant motion pressure wearing bass clothes. |
| 357 | `Cris Owl Alvarez/0160-DRUMKIT toy drummer 2.xiz` | DRUMKIT toy drummer 2 | 12 enabled ADD kit items with 77 envelopes, 9 free envelopes, 38 LFOs, and 32 filters. Layered percussion without PAD escape hatches. |
| 260 | `olivers-other/0038-Deep Zyn.xiz` | Deep Zyn | 4 ADD layers and 36 free envelopes. This is the free-envelope case without relying on mixed engines. |
| 251 | `Companion/0121-Ghost Ensemble.xiz` | Ghost Ensemble | ADD/SUB/PAD all active, layered, 50 envelopes, 30 LFOs, 11 filters, 11 formant filters. Good broad-spectrum stress target. |
| 220 | `Drums/0001-Drums Kit1.xiz` | Drums Kit | 11 enabled kit items, ADD/SUB mix, 30 envelopes, 11 free envelopes, 15 LFOs, 12 filters. A smaller drum-kit target than Oliver's monster. |
| 181 | `olivers-100/0124-Simple Clonewheel.xiz` | Simple Clonewheel | 8 enabled kit items with 7 PAD engines. Best early pressure for layered PAD routing without drum semantics. |

## Upstream PAD Listening Batch

`ZynPadReferenceRendererSurveysUpstreamGplPadFixturesWhenBuilt` renders a small
GPL PAD batch through the real Zyn oracle when the renderer is built:

- `Pads/0002-sin2x  pad.xiz`
- `Pads/0065-Soft Pad.xiz`
- `Dual/0008-Organ Choir Pad2.xiz`
- `Laba170bank/0098-DoublePadBass.xiz`
- `Companion/0121-Ghost Ensemble.xiz`

Each render lands in a per-synth timestamped folder under
`artifacts/parity/zyn-upstream-pad-fixtures/`, with Zyn reference WAVs, source
table WAVs, `report.txt`, and `candidate.aqua`. The candidate is now generated
per preset by `ZynInstrumentReader.RebuildFirstPadAsAquaSynthScript`: first
enabled PAD kit item, OSCIL harmonic magnitudes, oracle-reported table
basefreq, and basic volume/envelope scaffolding. The report names missing
semantics such as Zyn harmonic profile shaping, bandwidth/profile behavior, and
filter envelope/LFO lowering. These are listening and parity fixtures, not
library candidates.

## Upstream Formant Listening Batch

`ZynVoiceReferenceRendererSurveysUpstreamFormantFixturesWhenBuilt` renders a
small GPL ADD/formant batch through `ZynVoiceReference` when the reference
renderer is built:

- `Companion/0056-Wide Bass.xiz`
- `Companion/0121-Ghost Ensemble.xiz`
- `Companion/0119-Ghost Choir.xiz`

The batch intentionally requires multi-frame formant motion so the first workout
does not get hijacked by drum-kit envelope/source failures. Each run writes Zyn
reference WAVs, AquaSynth candidate WAVs, `candidate.aqua`, and `report.txt`
under `artifacts/parity/zyn-upstream-formant-fixtures/`. The first baseline
scores are poor but useful: Wide Bass `0.280990`, Ghost Ensemble `0.312813`,
and Ghost Choir `0.363644`. The shared missing features are exact Zyn
filter-position-driven vowel motion, formant smoothing/clearness, and ADD voice
source detail.

This is now ready for the real parity workout. The fixtures are expected to
scream. The point is that they scream in a chorus instead of letting one wounded
patch write the architecture.

## Pressure Conclusions

- **Named layers are real.** Anonymous AquaSynth voices can sketch small Zyn
  layers, but drum kits and clonewheel-style instruments need stable layer names,
  key ranges, engine identity, and per-layer routing.
- **Free envelopes are not optional forever.** Zyn's worst cases use dozens of
  free envelopes. ADSR plus staged operator envelopes will not remain honest for
  normal voice-layer work.
- **Formant motion is bigger than static formant filters.** Wide Bass and Ghost
  Ensemble show that static formant banks are only the foothold.
- **PAD is both source and layer pressure.** PADsynth-style patches are not just
  saw pads with slow attack; they need a spectral/wavetable source authority.
- **Drum kits should not be first golf targets.** They are the best stress test
  for the model, but probably too broad for the first new syntax slice.

## Coverage Map

`ZynInstrumentSurvey.CoverageDirectory` now emits a handled/counted/unknown
coverage report instead of only ranking instruments by aggregate pressure. The
upstream GPL bank report is written to
`artifacts/parity/zyn-upstream-bank-survey/coverage.md` by
`SurveysUpstreamGplInstrumentBankWhenAvailable`.

Current upstream bank coverage:

- Files parsed: `1318/1318`
- Enabled kit items: `1459`
- Layered instruments: `63`
- Handled with parity-backed or native AquaSynth coverage: native band-pass and
  notch filters, PAD layers, named
  kit layers, PAD table modes/bandwidth, PAD source-table extraction for the
  implemented OscilGen subset, PAD LP/HP/BPF/notch cutoff/order/Q/envelopes,
  staged non-free envelopes, and authored vowel-frame formant motion.
- Counted but not translated: ADD, SUB, mixed-engine kits, free envelopes,
  active LFO routing beyond explicit control lanes, exact Zyn formant position
  drivers/smoothing, and effects.
- Unknown PAD filter pressure: formant filters, peak filters, shelf filters,
  and other unimplemented PAD global filter categories/types such as
  `cat=1 type=0`, `cat=0 type=6`, `cat=0 type=8`, and smaller buckets.
  Analog and state-variable band-pass/notch (`cat=0 type=4/5`,
  `cat=2 type=2/3`) now lower through AquaSynth's native `bpf`/`notch`
  filter authority.
- Unknown PAD OscilGen pressure: unimplemented source filter types
  `10`, `12`, `11`, `4`, `5`, `3`, and `8`; unimplemented waveshapers
  `2`, `14`, `5`, `6`, `3`, `13`, `8`, and `9`.

This map is not a clone checklist. It is a workout list. A bucket graduates from
counted or unknown only when a parity target proves which AquaSynth authority
should own the behavior.

## Recommended Next Cut

Start with **ADD/SUB voice extraction or free-envelope point curves**, not more
PAD synthesis.

Reason: named layers, PAD spectral sources, staged envelopes, native
band-pass/notch filters, and a first vowel-frame formant-motion surface now
exist. The next counted Zyn pressure is no longer "where does this live?" for
formants; it is either ADD/SUB source detail or arbitrary envelope shape.

## First Responses

AquaSynth now has a minimal `layer` command:

```text
layer name=pad_low engine=pad min_key=36 max_key=84 gain=.07
voice layer=pad_low freq=130.8128
```

The layer owns source identity and metadata while current lowering still emits
ordinary voices. This is the scaffold needed before promoting PAD sources,
additive harmonic banks, free envelopes, or richer formant motion.

Additive layers now also have a `harmonics` command:

```text
layer name=body engine=add gain=.16
harmonics layer=body root=220 partials=1:.16,2:.075,3:.045
```

The harmonic bank records authored additive partial intent under a named layer,
then lowers to ordinary sine voices. It does not claim Zyn ADDsynth's full
oscillator table: phase, bandwidth, oscillator shaping, and exact free-envelope
behavior remain separate pressure.

Normal voices can also use staged rate/level envelopes through the same
`env=rl` surface already used by operator graphs:

```text
layer name=pad engine=pad env=rl rates=.5,.7,1.0,1.4 levels=1,.85,.7,0 gate=2.2
voice layer=pad freq=220
```

That gives PAD and vocal layers asymmetric contour authority without returning
to anonymous ADSR piles. Arbitrary Zyn free-envelope point curves remain
pressure; the staged surface is the first coherent rung, not the whole ladder.

PAD layers now have a spectral-cloud source command:

```text
layer name=pad_low engine=pad
spectrum layer=pad_low root=130.8128 spread=.012 partials=1:.07,1.5:.052
```

The command records PAD-like partial intent under the layer and emits a static
Faust wavetable source before normal voice treatment. The table is generated by
a PADsynth-style authoring path: frequency-domain harmonic spreading,
deterministic random phase, one inverse FFT, and normalization. This is not full
Zyn PAD engine parity across every harmonic profile and pitch-zone behavior, but
it is now real spectral-table synthesis rather than voice-count theater.

Wide Bass promoted the first formant-motion rung:

```text
voice layer=wide_bass formant_mix=.7 vowel_hz=.39
    vowels=600:90:1,1200:160:.7|500:80:1,900:120:.8
```

The `vowels` field is an AquaSynth voice authority: each `|`-separated frame is
a formant bank, and Faust blends adjacent frames cyclically at `vowel_hz`. The
Zyn workout can seed frames from `FORMANT_FILTER` vowel sequences, but exact Zyn
filter-frequency position driving, slowness, and vowel clearness remain pressure
rather than hidden compatibility sludge.
