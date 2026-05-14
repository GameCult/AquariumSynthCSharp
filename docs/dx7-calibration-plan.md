# DX7 Calibration Plan

## Objective

Make DX7 reference rebuilds prove Aquarium can emulate the machine, not merely
draw a similar operator graph. The hard `PRC SYNTH1` target showed the current
DSL surface is expressive enough to describe the topology, but the importer
math is still fake in the places that matter perceptually.

## Current Mechanism

- `dexed-py` renders lawful public-domain or project-authored DX7 references.
- Aquarium renders candidate patches through Faust-generated C#.
- Tests compare rendered audio with log-mel, envelope, duration, RMS,
  zero-crossing, centroid, and listening WAV artifacts.
- `OperatorGraph` owns topology: operators, routes, carriers, feedback, and
  staged operator envelopes.

## Invariants

- Do not add new DSL syntax until a calibration rung proves the current surface
  cannot express the behavior.
- Keep calibration fixtures project-authored or public-domain and test-only.
- Treat log-mel distance and listening notes as the primary perceptual evidence
  for timbre targets. Aggregate score is secondary.
- Separate carrier amplitude, modulation index, feedback amount, envelope
  level curve, and algorithm output compensation as distinct calibration facts.
- Cut PRC-specific constants once a calibrated DX7 lowering owns the same fact.

## Calibration Ladder

1. **Single carrier amplitude**
   - Render project-authored DX7 algorithm-32 patches with one audible carrier.
   - Sweep operator output levels.
   - Fit output level to linear carrier amplitude.
   - Verify Aquarium single-carrier renders match relative RMS.

2. **Two-operator modulation index**
   - Render a carrier plus one modulator across modulator output levels.
   - Fit DX7 output level to Aquarium route/index scaling using log-mel and
     harmonic energy, not RMS.
   - Keep carrier amplitude fixed while calibrating modulation index.

3. **Feedback scaling**
   - Render one self-feedback operator across DX7 feedback values.
   - Fit feedback amount separately from route/index scaling.

4. **Ratio detune**
   - Lower DX7 ratio-mode detune into Aquarium operator ratios.
   - Keep detune as frequency behavior, not a modulation or chorus feature.

5. **Algorithm output compensation**
   - Use ROM `COM` values to balance carriers before any PRC-specific branch
     boosting.
   - `COM` stores the output count minus one. Because Aquarium's carrier
     amplitude curve was fitted against algorithm 32's six-output baseline,
     carriers scale by `6 / (COM + 1)` during DX7 lowering.
   - Replace manual carrier scale constants with algorithm-derived lowering.

6. **Envelope level curve**
   - Render fixed-output operators with varied DX7 EG levels and rates.
   - Fit the rate/level envelope amplitude curve instead of assuming
     `level / 99`.
   - Current cut lesson: reusing the operator output-level amplitude curve for
     EG levels improves some envelope metrics but fails the hard PRC log-mel
     target. Do not land it without isolated envelope parity.
   - Current trace evidence lives in
     `artifacts/parity/dx7-envelope-trace/egstep.csv`: DX7 raw EG state can
     spike above unit gain and collapse to a tiny sustain within milliseconds,
     unlike the current linear `env=rl` approximation.
   - Dexed's rendered audio does not hear that raw state directly. The operator
     path samples EG once per 64-sample block and linearly interpolates the
     applied gain across the block, so a DX7-capable Aquarium envelope must
     model both the rate/level state machine and its block-interpolated output.
   - Aquarium staged operator envelopes now support per-segment curves, e.g.
     `env=rl rates=.00145,.0508,.5268,.35 levels=2,.297,.0156,0 curves=lin,exp,exp,exp`.
     This is a general staged-contour feature, not a DX7-specific syntax escape.
   - `Dx7SysEx.ApproximateAppliedRateLevelEnvelope` lowers a DX7 EG into a
     curved staged contour by tracing Dexed-style block-interpolated applied
     gain and normalizing it against Aquarium's separate operator-output level.
     On `PRC SYNTH1`, this improves log-mel from `0.24476814` to `0.17186502`,
     envelope distance from `0.43700194` to `0.13057204`, and zero-crossing
     ratio from `0.7931764` to `0.7815517` while keeping RMS at unity.

7. **Summed/cascaded modulation**
   - Render project-authored algorithm-8 stacks that isolate `6 -> 5 -> 3`
     and `4 + 5 -> 3`.
   - Replace hard `PRC SYNTH1` route probe logic with a topology-aware DX7
     route rule: isolated direct branches keep the two-op scale, while
     summed/cascaded branches use the summed-modulator scale.
   - The summed/cascaded scale is now `2.4`. A project-authored algorithm-8
     summed-stack parity test backs the topology-aware scale outside PRC, and
     the hard PRC replay improves to log-mel `0.16794802`, envelope distance
     `0.09510617`, centroid ratio `1.1254044`, and score `0.68491656` after
     graph gain normalization.

8. **Hard target replay**
   - Re-run `PRC SYNTH1`.
   - Keep the log-mel gate and listening artifacts.
   - Raise thresholds only when the calibrated lowering improves for the right
     reason.
   - Current curved-envelope plus stronger-stack replay gates log-mel at
     `<= 0.18`, envelope distance at `<= 0.14`, zero-crossing at `>= 0.75`,
     RMS ratio near unity, and score at `>= 0.64`.

## Rejected Path

Do not keep tuning `PRC SYNTH1` with branch-specific magic constants. That patch
is the exam, not the textbook.
