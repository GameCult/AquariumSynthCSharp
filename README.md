# Aquarium Synth CSharp

C# front-end for Aquarium Synth patch scripts. It parses the Aquarium patch DSL
into a serializable patch graph, emits Faust `.dsp`, and can ask an installed
Faust compiler to generate backend code such as C#, C++, C, or Rust.

This repo is the C# bridge for Aquarium/Vortice-land. Rust remains the reference
lab; this project is the tool that lets the engine script Faust without dragging
the Rust crate into the room by the ankle.

## Run

```powershell
dotnet test
```

## Shape

- `PatchScript.Parse(script)` lowers terse script into `SynthPatch`.
- `FaustEmitter.Emit(patch)` emits Faust source.
- `FaustCompiler.ValidateAsync(source)` compile-checks with Faust when present.
- `FaustCompiler.CompileAsync(source, options)` writes generated backend code.
- `BuiltInScripts.ReferenceScripts()` carries readable SFXR, BFXR-flavored,
  808, FM bell, wobble bass, and advanced layered patches. They are stable
  references for testing and for judging whether the DSL can express useful
  sound designs cleanly.
- `SfxrParams`, `PatchScriptScoring`, `AudioAnalyzer`, and `Presets` carry the
  reusable Rust-side analysis, scoring, SFXR, and preset tools.

Faust `2.85.5` supports `-lang csharp`, so the intended hot path is:

```csharp
var patch = PatchScript.Parse("""
    voice
        wave=saw
        freq=55
        gain=0.2
        sustain=0.5
        decay=0.2
    """);
var export = FaustEmitter.Emit(patch, new FaustExportOptions("bass"));
await FaustCompiler.CompileAsync(
    export.Source,
    new FaustCompileOptions(FaustTargetLanguage.CSharp, "Generated/Bass.cs"));
```

## Status

The current slice covers the modular graph surface needed by the reference
scripts, SFXR atoms, script scoring, audio comparison, presets, Faust emission,
and installed Faust validation. Migration coverage is tracked in
[`docs/migration-checklist.md`](docs/migration-checklist.md), because leaving
important things behind in the old repo would be a very efficient way to become
our own haunted house.

Reference-driven DSL growth is tracked in
[`docs/reference-synth-roadmap.md`](docs/reference-synth-roadmap.md). Agent
handoff state lives in [`state/spine.yaml`](state/spine.yaml).
