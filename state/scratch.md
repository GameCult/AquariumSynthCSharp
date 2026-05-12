# Scratch

Current standing order: grow AquariumSynthCSharp through external reference
targets, not through speculative field sprawl. `state/spine.yaml` and
`docs/reference-synth-roadmap.md` are the handoff surfaces for agents working on
the synth library while Aquarium work continues elsewhere.

The next likely slice is DX7/Dexed:

- Add neutral reference model contracts with provenance/license/hash fields.
- Implement a DX7 SysEx parser skeleton for single voices and cartridge payloads.
- Extract operator topology, envelopes, ratios, feedback, levels, LFO, and pitch
  envelope into feature records before trying to translate.
- Rebuild one or two small DX7-style sounds in Aquarium DSL.
- Keep tests focused on structure first, then add rendered audio comparison once
  the render path is explicit.
