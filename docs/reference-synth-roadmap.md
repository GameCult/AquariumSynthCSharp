# Reference Synth Roadmap

AquariumSynthCSharp needs external synthesis references for the same reason the
SFXR path was useful: a real target prevents the DSL from becoming a pile of
locally plausible knobs. The goal is not to clone every synth. The goal is to
use known engines and patch formats as pressure tests, then rebuild selected
sounds through Aquarium's own graph until the missing abstractions are obvious.

## Objective

Build a reference-driven synth library workflow:

- Parse or describe external synth patches as structured reference models.
- Render references when legally and technically practical.
- Rebuild selected references in Aquarium patch DSL.
- Compare audio with analysis metrics and listening notes.
- Promote only the abstractions that make repeated reference rebuilds simpler.

The DSL should grow because a reference target demanded a missing invariant, not
because a field name seemed maybe useful at 2 AM.

## Live Invariants

- Aquarium owns its patch graph. External synths are reference targets, not
runtime dependencies.
- Reference importers must preserve source provenance, license status, and the
source format hash.
- Tests may compare against rendered fixtures, extracted parameter topology, or
known structural expectations. Weak "it parsed" tests do not prove expressivity.
- Do not add a generic graph escape hatch until a specific family of references
forces it.
- When a reference target exceeds the current voice-centric DSL, document the
missing graph feature instead of smuggling it through unrelated parameters.

## Target Families

### Tier 1: DX7/Dexed

Use this first. DX7 SysEx is compact, structured, and musically meaningful:
six operators, thirty-two algorithms, per-operator envelopes, ratios, feedback,
LFO modulation, pitch envelopes, key scaling, and output levels.

Dexed is a GPL-3.0 DX7-compatible open-source synth that is closely modeled on
the Yamaha DX7 and accepts SysEx cartridge data. Its `msfa` synth engine remains
Apache-2.0, which may matter if we use code as a reference rather than only
format notes.

Pressure on Aquarium DSL:

- Operator graph representation.
- Multi-stage envelopes beyond ADSR.
- Algorithm presets as topology, not per-voice decoration.
- Feedback as a first-class edge.
- Ratio/fixed-frequency operator modes.
- Feature and audio parity tests against rendered references.

First deliverable:

- Add a `Dx7SysEx` parser for single voice and cartridge payloads.
- Convert one or two public-domain or project-authored DX7-style voices into
  `ReferencePatch` records.
- Build an Aquarium DSL approximation using explicit FM templates.
- Add mel/envelope/spectral comparison against rendered Dexed or internal
  reference output once the render path is chosen.

Source: [Dexed](https://github.com/asb2m10/dexed)

### Tier 2: ZynAddSubFX

ZynAddSubFX is a GPL-2.0-or-later synth with additive, subtractive, and PAD
synthesis engines. It ships a large instrument library and exposes formant
filters, state-variable filters, free envelope shapes, layered kits, modulation,
and effects routing.

Pressure on Aquarium DSL:

- Additive harmonic banks.
- Non-ADSR envelope curves.
- Layered instruments and kits.
- Formant and state-variable filter intent.
- PAD-style spectral/wavetable generation as a distinct source type.
- Part effects versus voice-local effects.

First deliverable:

- Parse a small set of Zyn instrument XML files into a neutral reference model.
- Classify features used by each instrument before trying to synthesize them.
- Rebuild one additive lead, one PAD texture, and one formant/vocal patch.

Sources:

- [ZynAddSubFX](https://github.com/zynaddsubfx/zynaddsubfx)
- [ZynAddSubFX feature list](https://github.com/zynaddsubfx/zynaddsubfx)

### Tier 3: Faust Examples And Libraries

Faust remains the best reference for general DSP graph topology. Its official
repo contains categorized examples, and the standard libraries include
oscillators, filters, delays, reverbs, physical models, DX7, virtual analog
effects, wave-digital models, spatialization, and more.

Pressure on Aquarium DSL:

- General signal graph expression.
- Multichannel routing.
- Delay networks and feedback delay networks.
- Reverb topologies.
- Physical modeling: waveguides, modal resonators, mass interaction, exciters.
- Wave-digital circuit trees.

First deliverable:

- Add an `ExternalFaustReference` catalog entry type that stores source path,
  license/provenance, graph family, and expected compile behavior.
- Select small fixtures from official examples first: comb string, additive
  drum, stereo reverb, physical model, WDF filter.
- Use Faust references to decide whether Aquarium needs a lower-level graph DSL
  beneath the current instrument patch surface.

Sources:

- [Faust examples](https://github.com/grame-cncm/faust)
- [Faust library docs](https://faustlibraries.grame.fr/)
- [Faust physical models](https://faustlibraries.grame.fr/libs/physmodels/)
- [Faust reverbs](https://faustlibraries.grame.fr/libs/reverbs/)
- [Faust wave-digital models](https://faustlibraries.grame.fr/libs/wdmodels/)

### Tier 4: Surge XT

Surge XT is a modern GPL-3.0 hybrid synth with a large real-world patch corpus.
It is a strong long-term target, but it is too broad for the first import pass.

Pressure on Aquarium DSL:

- Multi-scene patch structure.
- Multiple oscillator algorithms.
- Wavetables.
- Deep modulation routing.
- Filter/fx chains.
- Macro controls and performance mapping.

First deliverable:

- Inspect factory patch format and licensing.
- Build a feature extractor before attempting translation.
- Choose one simple subtractive patch, one wavetable patch, and one modulation
  patch as future targets.

Source: [Surge XT](https://github.com/surge-synthesizer/surge)

### Tier 5: Cardinal/VCV-Style Modular Patches

Cardinal is a GPL-3.0 self-contained open-source modular synth based on VCV Rack
code. It is the right target once Aquarium needs to prove arbitrary modular graph
expression.

Pressure on Aquarium DSL:

- Module graph representation.
- Cables as first-class edges.
- Control voltage semantics.
- Audio-rate versus control-rate modulation.
- Feedback and clocking.
- Polyphonic lanes.

First deliverable:

- Do not start here.
- When the lower-level graph layer exists, parse a small Cardinal patch and
  classify modules, ports, cables, and rates.

Source: [Cardinal](https://github.com/DISTRHO/Cardinal)

## Data Model Slice

Add reference models before importers grow teeth:

```csharp
public sealed record ReferencePatch(
    string Id,
    string Family,
    string Name,
    ReferenceSource Source,
    IReadOnlyList<ReferenceFeature> Features,
    string? AquariumScript);

public sealed record ReferenceSource(
    string Kind,
    string Uri,
    string License,
    string Hash,
    string Notes);
```

This keeps source authority visible. A reference can be "known target, not yet
translated" without pretending it is an Aquarium patch.

## Work Order

1. Create reference model contracts and tests.
2. Add DX7 SysEx parser and feature extraction.
3. Add two DX7-derived Aquarium DSL rebuilds.
4. Add audio fixture workflow for rendered references.
5. Add Zyn XML feature extraction.
6. Add Faust reference catalog entries and compile checks.
7. Reassess DSL shape: keep voice DSL, split lower-level graph DSL, or add
   explicit operator/routing sublanguage.
8. Only then touch Surge or Cardinal.

## Rejected Shortcuts

- Do not vendor giant preset banks before license and provenance are explicit.
- Do not translate everything into the current `voice` command by adding dozens
  of fields.
- Do not compare only parse success. That proves the parser is lenient, not that
  the DSL can express the sound.
- Do not use Faust as a hidden escape hatch for Aquarium patches. If a reference
  requires raw Faust, the DSL is missing an abstraction or the reference belongs
  in a lower-level graph layer.

## Success Criteria

- A new agent can pick one target family and know where to start.
- Every reference fixture says what feature pressure it applies.
- The DSL roadmap is driven by reference failures, not taste fumes.
- Aquarium can keep using readable reference patches while the synth library
  grows toward serious DSP graph expression.
