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

4. **Envelope level curve**
   - Render fixed-output operators with varied DX7 EG levels and rates.
   - Fit the rate/level envelope amplitude curve instead of assuming
     `level / 99`.

5. **Algorithm output compensation**
   - Use algorithm topology/ROM compensation to balance carriers before any
     PRC-specific branch boosting.
   - Replace manual carrier scale constants with algorithm-derived lowering.

6. **Hard target replay**
   - Re-run `PRC SYNTH1`.
   - Keep the log-mel gate and listening artifacts.
   - Raise thresholds only when the calibrated lowering improves for the right
     reason.

## Rejected Path

Do not keep tuning `PRC SYNTH1` with branch-specific magic constants. That patch
is the exam, not the textbook.
