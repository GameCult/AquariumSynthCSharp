namespace AquariumSynth.Dsl;

public sealed record ReferenceRebuild(
    string Id,
    string Family,
    string Name,
    string ReferenceId,
    string Script,
    IReadOnlyList<ReferenceFeature> MatchedFeatures,
    IReadOnlyList<ReferenceFeature> MissingFeatures,
    string Notes);

public static class ReferenceRebuildCatalog
{
    public static readonly IReadOnlyList<ReferenceRebuild> Dx7Rebuilds =
    [
        new(
            "dx7/algo32-additive-organ/aquarium",
            "dx7",
            "Algorithm 32 additive organ",
            "dx7/algo32-additive-organ",
            BuiltInScripts.Dx7StyleAlgorithm32AdditiveOrgan,
            [
                new("carrier_operators", "1,2,3,4,5,6", "Six independent carriers map cleanly to six Aquarium voices."),
                new("modulation_edge_count", "0", "No operator-to-operator modulation is needed for the additive shape."),
                new("self_feedback_operators", "6", "Approximated with fold/drive on the highest partial."),
                new("operator_envelope_approximation", "DX7 EG -> ADSR", "DX7 four-rate/four-level envelopes can now be lowered to labeled ADSR approximations.")
            ],
            [
                new("operator_envelope_exactness", "per-operator four-stage DX7 EG", "Aquarium has an ADSR approximation helper, not exact DX7 rate/level envelope execution."),
                new("output_compensation", "algorithm ROM COM", "Aquarium voice gains are explicit and not derived from DX7 COM scaling.")
            ],
            "This is the honest easy case: DX7 algorithm 32 is mostly additive synthesis, so the current voice DSL can represent the carrier layout without pretending to own a full operator graph."),
        new(
            "dx7/algo8-bright-pair/aquarium",
            "dx7",
            "Algorithm 8 bright pair",
            "dx7/algo8-bright-pair",
            BuiltInScripts.Dx7StyleAlgorithm8BrightPair,
            [
                new("carrier_operators", "1,3", "Represented as carriers in a first-class Aquarium operator graph."),
                new("modulation_edges", "6->5,4+5->3,2->1", "Represented as explicit operator edges in the graph."),
                new("feedback_sources", "4", "Represented as operator-local feedback approximation on operator 4."),
                new("runtime_macros", "brightness,strike", "Field-site parameters expose useful sound controls without recompilation."),
                new("operator_envelope_approximation", "DX7 EG -> ADSR", "DX7 four-rate/four-level envelopes can now be lowered to labeled ADSR approximations.")
            ],
            [
                new("dx7_feedback_register", "DX7 feedback scaling/register details", "Aquarium operator feedback is now renderable as a one-sample delayed self-reference, but not calibrated to exact DX7 feedback scaling."),
                new("operator_envelope_exactness", "independent DX7 rate/level envelopes", "Aquarium has per-operator ADSR approximation, not exact DX7 EG execution.")
            ],
            "This patch is deliberately a pressure test. The new operator graph owns the topology; exact DX7 envelope and feedback-register behavior remain pressure for later refinement."),
        new(
            "dx7/public-domain-mc-mm-5-3/aquarium",
            "dx7",
            "Public-domain MC-MM 5-3",
            "dx7/public-domain/analog1/13",
            BuiltInScripts.Dx7StylePublicDomainMcMm53,
            [
                new("reference_fixture", "analog1.syx#13", "Public-domain DX7 SysEx fixture from Musical Artifacts artifact 152."),
                new("audio_parity", "score>=0.75 log_mel<0.12 envelope<0.10", "First rendered DX7 parity target using dexed-py reference audio and Faust-rendered Aquarium audio."),
                new("dominant_pitch", "654.0639Hz", "The rendered reference behaves as a short C3-derived 5x-ratio sine-like tone."),
                new("operator_envelope_approximation", "DX7 EG -> ADSR", "ADSR timing is fitted to the rendered reference envelope.")
            ],
            [
                new("operator_topology_exactness", "2->1 FM pair", "The first parity candidate matches the rendered behavior as a sine-like tone; it does not claim exact DX7 operator-level execution."),
                new("operator_envelope_exactness", "DX7 rate/level EG", "The candidate uses Aquarium ADSR timing matched to rendered audio.")
            ],
            "This is the first thresholded audio parity rung. It proves the render/compare loop on a lawful fixture without pretending the DX7 internals are solved.")
    ];

    public static IEnumerable<ReferenceRebuild> All()
    {
        foreach (var rebuild in Dx7Rebuilds)
        {
            yield return rebuild;
        }
    }
}
