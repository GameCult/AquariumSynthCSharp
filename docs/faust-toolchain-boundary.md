# Faust Toolchain Boundary

AquaSynth owns the path from patch intent to loadable DSP product. Aquarium
Engine owns audio hosting. That split keeps the compiler brain near the patch
model and keeps the engine from becoming a synth compiler with a game loop
stapled to it.

Package ownership follows that boundary:

- `AquaSynth.Core` owns patch meaning: model records, `.aqua` parsing, analysis,
  presets, and Faust source emission.
- `AquaSynth.Faust` depends on Core and owns toolchain/rendering: Faust CLI
  validation, target-code generation, native `libfaust` loading, compile
  manifests, factory lifetime, and offline/sample rendering.

## Pipeline Map

1. AquaSynth parses `.aqua` script into an explicit `SynthPatch`.
2. AquaSynth emits stable, inspectable Faust source.
3. AquaSynth resolves a pinned Faust toolchain or caller-provided Faust path.
4. AquaSynth compiles the patch into a target artifact.
5. AquaSynth emits a manifest for parameters, buses, MIDI behavior, sample-rate
   assumptions, cache keys, diagnostics, and artifact provenance.
6. Aquarium loads the artifact, binds parameters, schedules notes and buffers,
   and calls the DSP from its realtime path.

Aquarium may request compilation, cache lookup, reload, or diagnostics. It
should not need to know how AquaSynth chooses Faust flags, target languages,
architecture files, generated source layout, or artifact identity.

## Ownership

AquaSynth owns:

- patch graph contracts
- Faust source emission
- Faust toolchain selection and version pinning
- compile cache keys
- generated source/native artifact layout
- parameter and bus manifests
- diagnostics and compile provenance
- dynamic patch compilation outside the realtime callback
- native Faust factory lifetime and offline/sample-buffer rendering for hosts

Aquarium owns:

- device I/O
- buffer scheduling
- audio-thread rules
- note/event delivery
- plugin or engine integration
- artifact loading policy for its platform
- presentation and editor UX

## Runtime Rule

Compilation is never an audio-thread operation. Dynamic patches compile in a
background worker or build step, then Aquarium swaps in a finished DSP artifact
through a realtime-safe handoff.

Parameter changes are not recompiles. Recompile only when graph shape changes;
ordinary patch controls move through the compiled DSP parameter API.

## Target Lanes

- Authoring/tests may use Faust-generated C# because it is easy to render,
  inspect, and compare inside the .NET test suite.
- Shipping builds should prefer Faust-generated native targets when the platform
  permits it.
- A bundled Faust compiler belongs in an AquaSynth toolchain package or
  installer lane, not as an accidental dependency of the core DSL package.
- Consumer runtimes should normally bundle compiled DSP artifacts plus manifests,
  not the compiler itself, unless live user patch authoring is a product feature.

Current API foothold: `AquaSynthNativeCompiler` loads `libfaust`, compiles
`.aqua` scripts into native Faust factories, writes optional `.dsp` artifacts,
returns `AquaSynthNativeManifest`, and renders mono sample buffers for hosts that
need triggered one-shot playback.

## Invariants

- AquaSynth remains the authority for what a patch means.
- Faust remains the pure DSP target, not the host runtime.
- Aquarium remains the realtime host, not the compiler.
- Generated artifacts are reproducible from script, AquaSynth version, Faust
  version, target, compile options, and architecture inputs.
- Licenses and provenance travel with toolchains, reference inputs, generated
  artifacts, and packaged output.
