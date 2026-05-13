# Aquarium Synth Audio Doctrine

## Mission

AquariumSynthCSharp is the authoring and compiler surface for Aquarium synth
patches. It should let us sketch sound quickly, compile to Faust, inspect the
graph, compare results, and hand stable contracts to the C#/Vortice side.

The point is not to rebuild a whole audio engine in C# because we got excited
near a compiler. The point is to make patch intent portable, analyzable, and
pleasantly dangerous.

## Realtime Law

Realtime audio has one primitive moral fact: the callback deadline wins.

Runtime-facing code must avoid operations with unbounded or scheduler-dependent
latency:

- dynamic allocation or deallocation
- filesystem, network, console, or logging I/O
- locks, mutexes, blocking waits, sleeps, or context switches
- host/API calls not explicitly documented as callback-safe
- graph compilation, parsing, reflection, or cache misses

Authoring tools can do expensive work. Audio callbacks cannot. Build patches,
compile Faust, allocate buffers, score references, and serialize contracts
outside the audio thread. Feed the realtime side precompiled DSP, fixed buffers,
plain parameters, and lock-free or double-buffered control updates.

## Dataflow Shape

Patch scripts are a textual dataflow language. Their job is to describe a graph:
voices, controls, modulators, envelopes, filters, and output routing.

Pure Data is useful discipline here:

- A patch is a graph of objects and connections, not a bag of imperative steps.
- Audio-rate work is block-based, and block size is part of the execution model.
- Subpatches can change block size, overlap, resampling, and on/off DSP state;
  those ideas map to explicit compile/runtime boundaries, not hidden globals.
- Control events and signal streams are different animals. They can meet, but
  the crossing should be named.

For this repo that means:

- Keep `SynthPatch` explicit and serializable.
- Keep parser sugar as sugar. Defaults, templates, SFXR atoms, and buses should
  all lower into visible patch graph data.
- Keep block size, sample rate, channel count, and latency visible in APIs that
  consume or produce audio buffers.
- Prefer deterministic lowering. Same script plus same options must produce the
  same patch and Faust source.

## Faust Boundary

Faust describes pure DSP: inputs to outputs. Architecture files and host code
connect that pure processor to drivers, UI, MIDI, sensors, plugins, and the
outside world.

That separation is the model for this repo:

- The DSL owns musical intent and graph construction.
- The Faust emitter owns pure signal expression.
- The engine owns scheduling, buffers, device I/O, threading, and presentation.
- Patch parameters are runtime controls, not compile-time constants. A compiled
  DSP must be able to expose stable parameter paths so Aquarium can vary a sound
  without recompiling the patch.
- Host parameters should be smoothed where they cross into signal-rate behavior.

Generated Faust should be stable, boring, and inspectable. Cleverness belongs in
the DSL compiler only when it lowers to a graph a tired engineer can still read
after midnight.

## Control And Modulation

The Serum-style promise is not "one primitive that puts an oscillator on
everything." The promise is that any meaningful parameter can be a modulation
target in an ergonomic, visible way.

Rules:

- Every modulation target must have a stable semantic name.
- Every exposed parameter must declare a stable path, default, min, max, step,
  unit or scale when meaningful, and whether it is safe to automate at control
  rate.
- Global controls and voice-local modulators should share target names.
- Modulators need waveform, rate, depth, phase, and bias.
- Buses are authoring conveniences; compiled patches should expose individual
  target lanes.
- UI/control-rate movement should be smoothed before it becomes audible zipper
  noise.
- Recompilation is for graph shape changes. Parameter changes must flow through
  the hosted DSP control API.

## Notes And Envelopes

Notes own pitch and gate. Envelopes own level shape. Do not mix those
authorities.

- `Note` carries note frequency, gate duration for one-shot patches, and source
  selection for host/MIDI-driven patches.
- `Envelope` is ADSR-shaped: attack time, decay time, sustain level, and release
  time.
- Legacy SFXR sustain maps to note gate duration, because it is how long the
  generated sound is held before release.
- Legacy SFXR punch maps to a lower sustain level plus voice gain compensation
  when importing old SFXR material: ADSR peak stays implicit at 1, and the
  sustain level falls below that peak. Keep that as compatibility mapping, not
  as a general envelope field.
- Host/MIDI note mode should expose stable note frequency and note gate controls
  that the engine can wire to MIDI note-on/note-off behavior.
- Faust-managed polyphony is the preferred MIDI path. Aquarium should describe a
  single voice graph and emit Faust's standard `freq`, `gain`, and `gate`
  controls with `[midi:on][nvoices:n]` options, leaving MIDI decoding and voice
  allocation to Faust architectures unless a concrete target proves that
  boundary insufficient.

## Metrics

Analysis exists to catch regressions and support search, not to crown winners.

Useful metrics:

- envelope distance for shape and timing
- log-mel spectrogram distance for perceptual-ish spectral shape
- peak/RMS ratios for loudness sanity
- zero-crossing and centroid ratios for noise/brightness drift
- script readability and terseness scores for DSL golf work

Do not confuse high metric agreement with musical success. The game context,
the reference sound, and the user’s ear still outrank the spreadsheet. Annoying,
but civilization has survived worse.

## Source Distillations

- PortAudio callback guidance: callbacks are delicate realtime contexts; avoid
  unbounded calls such as allocation, I/O, context switching, mutex operations,
  and unsafe API calls.
  <https://files.portaudio.com/docs/v19-doxydocs/writing_a_callback.html>
- Pure Data `block~`/`switch~`: DSP is block-structured; subpatches can set
  block size, overlap, resampling, and switched DSP state. Pd’s default block
  size is 64 samples.
  <https://pd.iem.sh/objects/block~/>
- Faust architecture docs: a Faust program is pure DSP mapping inputs to
  outputs; architecture files connect that DSP to drivers and controllers.
  <https://faustdoc.grame.fr/manual/architectures/>
- Faust UI controls: `hslider`, `vslider`, `nentry`, `button`, and `checkbox`
  expose runtime controls; UI helper classes such as `MapUI` provide
  `setParamValue`/`getParamValue` style access by path or short name.
  <https://faustdoc.grame.fr/manual/architectures/>
- Faust signals library: buses, block termination, interpolation, repeat, and
  smoothing are first-class signal-composition tools; smooth control crossings
  instead of letting UI steps become audio artifacts.
  <https://faustlibraries.grame.fr/libs/signals/>
