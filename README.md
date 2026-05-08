# Aquarium Synth CSharp

C# front-end for Aquarium Synth patch scripts. It parses the terse Aquarium DSL
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

Faust `2.85.5` supports `-lang csharp`, so the intended hot path is:

```csharp
var patch = PatchScript.Parse("v w=saw f=55 g=.2 s=.5 d=.2");
var export = FaustEmitter.Emit(patch, new FaustExportOptions("bass"));
await FaustCompiler.CompileAsync(
    export.Source,
    new FaustCompileOptions(FaustTargetLanguage.CSharp, "Generated/Bass.cs"));
```

## Status

This is the first scaffolded slice, not the whole cathedral. It covers the graph
surface needed by the current wobble/FM/formant scripts and validates generated
Faust when the compiler is installed. Next job is tightening parity against the
Rust crate and porting the remaining preset/macro conveniences.
