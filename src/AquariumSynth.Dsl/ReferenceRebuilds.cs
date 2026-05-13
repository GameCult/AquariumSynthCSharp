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
                new("self_feedback_operators", "6", "Approximated with fold/drive on the highest partial.")
            ],
            [
                new("operator_envelopes", "per-operator four-stage DX7 EG", "Aquarium now has ADSR envelopes, but not DX7 rate/level envelopes."),
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
                new("runtime_macros", "brightness,strike", "Field-site parameters expose useful sound controls without recompilation.")
            ],
            [
                new("dx7_feedback_register", "one-sample feedback register", "Aquarium operator feedback currently uses a smoothed self-reference approximation, not exact DX7 feedback register timing."),
                new("operator_envelopes", "independent DX7 rate/level envelopes", "Aquarium has simpler per-voice envelopes, not per-operator EGs.")
            ],
            "This patch is deliberately a pressure test. The new operator graph owns the topology; exact DX7 envelope and feedback-register behavior remain pressure for later refinement.")
    ];

    public static IEnumerable<ReferenceRebuild> All()
    {
        foreach (var rebuild in Dx7Rebuilds)
        {
            yield return rebuild;
        }
    }
}
