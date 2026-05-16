# AquaSynth Patch Library

This folder is the development patch library for AquaSynth `.aqua` scripts. It is where reusable stock, reference rebuilds, calibration probes, and inspiration patches live as files instead of being trapped inside C# string constants or ignored render artifacts.

The library is intentionally outside `src/` and is not part of the shipped `AquaSynth.Dsl` NuGet package. Treat it as repo-owned source material for future synth consumers, examples, and parity work.

## Layout

- `examples/` - syntax and feature tours.
- `sfxr/`, `bfxr/`, `808/`, `fm-bell/`, `wobble-bass/` - stock patches derived from existing built-in reference catalogs.
- `dx7/` - DX7-inspired rebuilds and public-domain parity candidates.
- `advanced/` - richer multi-voice inspiration patches.
- `library.yaml` - machine-readable index of the patch files.

Every `.aqua` file in this tree should parse through `PatchScript.Parse`. If a patch is experimental and does not parse yet, keep it elsewhere until the syntax is real.