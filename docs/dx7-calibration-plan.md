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
  level curve, and graph/output gain as distinct calibration facts.
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
   - Rejected as carrier-balance authority. ROM `COM` scaling made several
     community patches louder but pushed the harmonic balance away from Dexed.
   - `OperatorOutputCompensation` now returns unity; loudness belongs to graph
     gain calibration, not hidden per-carrier COM boosts.
   - Keep this rung as a warning: passing RMS/envelope with the wrong overtone
     balance is not parity.

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
   - The summed/cascaded scale is now `6.0`. A project-authored algorithm-8
     summed-stack parity test backs the topology-aware scale outside PRC. Split
     project-authored probes now also isolate the cascaded `6 -> 5 -> 3` branch
     at log-mel `0.03811625` and the summed `4 + 5 -> 3` branch at log-mel
     `0.042429477`, with listening WAV artifacts under
     `artifacts/parity/dx7-project-authored/`. The hard PRC replay improves to
     log-mel `0.13874426`, envelope distance `0.067291506`, centroid ratio
     `1.1156512`, and score `0.7546445` after graph gain normalization.

8. **Hard target replay**
   - Re-run `PRC SYNTH1`.
   - Keep the log-mel gate and listening artifacts.
   - Raise thresholds only when the calibrated lowering improves for the right
     reason.
   - Current curved-envelope plus stronger-stack replay gates log-mel at
     `<= 0.145`, envelope distance at `<= 0.08`, zero-crossing at `>= 0.75`,
     RMS ratio near unity, and score at `>= 0.74`.

9. **Community voice survey**
   - Use public-domain community SysEx fixtures as broader behavioral probes
     after each isolated calibration rung.
   - `analog1.syx` from Musical Artifacts artifact 152 now has a rendered
     parity test for the community voices `{ Mooger }`, `Piano Bass`, and
     `RES SYNTH1`. The
     test writes listening artifacts under
     `artifacts/parity/dx7-community-analog1/` and gates log-mel at `<= 0.25`,
     envelope distance at `<= 0.14`, zero-crossing ratio in `0.8..1.11`, and
     score at `>= 0.6`.
   - `Piano Bass` exposed a real pitch bug: the candidate ignored the DX7 voice
     transpose byte, so `transpose=12` rendered one octave too high. Aquarium
     now uses `Dx7SysEx.NoteFrequencyHz(midiNote, transpose)`, where `24` is
     neutral.
   - `ANLGSYN 1` exposed a second real pitch/frequency bug: DX7 fixed-frequency
     operators were being lowered as fake note ratios. Aquarium now maps fixed
     operators through the Dexed log-frequency formula and divides the absolute
     Hz by the graph base frequency. This restores the low fixed-frequency
     carrier/modulation buzz for op1/op3 instead of flattening the voice into
     the wrong drone.
   - Community listening then exposed two note-dependent lowering gaps:
     operator key-scaling was computed against hardcoded MIDI note 60, and DX7
     operator rate scaling was parsed but not applied to the block-interpolated
     envelope trace. Aquarium now lowers both against the effective played
     MIDI note after voice transpose. This makes `RES SYNTH1` attack much
     closer and improves `Piano Bass` log-mel, with graph gain rebalanced to
     `0.72` for `Piano Bass`.
   - `ANLGSYN 1` also exposed that the previous max-feedback value was too tame
     for audible community patches. DX7 feedback value `7` now lowers to
     Aquarium feedback `2.2`, while feedback value `5` remains at `0.19` for
     the hard PRC target. The hotter value restores much more of the 1.2-5 kHz
     buzzing-harmonic band, with envelope/RMS still imperfect.
   - Listening then caught that the max-feedback candidate still lost punch
     halfway through the note. Output `soft_clip` helped the attack but did
     not protect the sustained buzz by itself. The accepted lowering now
     preserves a higher sustain floor for max-feedback source operators and
     gates `ANLGSYN 1` with a sustained 2.5-5 kHz band-energy check so the
     old drifting version cannot pass by global log-mel score alone.
   - Removing ROM COM carrier boosts made the community harmonics closer by
     ear but much quieter. Restoring loudness with graph gain keeps the improved
     balance: `{ Mooger }` uses `gain=0.75`, `Piano Bass` uses `gain=0.72`,
     and `RES SYNTH1` uses `gain=0.75`.
   - `DX1 LEAD B` and `MELLOWSOLO` also sounded harmonically closer with COM
     disabled and graph gain restored, but they still fail the current numeric
     parity gates. `MELLOWSOLO` now has a pressure artifact test that writes
     listening WAVs without claiming parity. Keep them as pressure for branch
     emphasis, envelope detail, and LFO/operator attack behavior, not as
     passing stock.

## Rejected Path

Do not keep tuning `PRC SYNTH1` with branch-specific magic constants. That patch
is the exam, not the textbook.
