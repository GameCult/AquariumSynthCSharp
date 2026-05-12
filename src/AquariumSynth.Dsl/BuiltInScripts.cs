namespace AquariumSynth.Dsl;

public static class BuiltInScripts
{
    public const string PatchScriptExample = """
        # Commands can span lines. A field-only line belongs to the command above it.
        patch
            gain=0.7
            soft_clip=true

        mod name=wobble
            wave=sine
            hz=5
            pitch=0.01
            formant_mix=0.08

        bus name=shimmer
            wave=triangle
            hz=2
            to=gain:0.08,lpf:-0.04,formant_mix:0.12

        voice
            wave=sine
            freq=220
            gain=0.12
            attack=0.002
            sustain=0.03
            decay=0.2
            vibrato=0.02
            vibrato_hz=5
            formants=620:90:1,1040:150:0.8
            formant_mix=0.45

        voice
            wave=triangle
            freq=440
            gain=0.04
            sustain=0.02
            decay=0.18
            lpf=0.7
            hpf=0.02

        sfxr
            preset=laser
            mutate_seed=9
            mutate=0.01
        """;

    public static readonly IReadOnlyList<string> ClassicSfxrNames =
    [
        "pickup",
        "laser",
        "explosion",
        "powerup",
        "hit",
        "jump",
        "blip"
    ];

    public const string ClassicSfxrPrimitiveGolfScript = "pickup;laser;explosion;powerup;hit;jump;blip";

    public static readonly IReadOnlyList<(string Name, string Script)> ClassicSfxrPrimitiveGolfScripts =
    [
        ("pickup", """
            voice
                wave=square
                freq=148.7934
                gain=0.22
                sustain=0.01451247166
                decay=0.1306122449
                punch=0.45
                drive=0.201
                arp_delay=0.081844
                arp_mult=1.116121255
            """),
        ("laser", """
            voice
                wave=saw
                freq=229.0554
                gain=0.22
                sustain=0.08185941043
                decay=0.07346938776
                pitch_ramp=0.703836
                duty=0.31
                duty_ramp=0.056
                hpf=0.04
                phaser=0.0001458
                phaser_ramp=-0.000504
                drive=0.12
            """),
        ("explosion", """
            patch repeat=0.174744
            voice
                wave=noise
                freq=20
                gain=0.22
                sustain=0.1907029478
                decay=0.293877551
                punch=0.52
                pitch_ramp=0.0320625
                phaser=-0.0008712
                phaser_ramp=-0.00035
                vibrato=0.11
                vibrato_hz=3.4112
                noise=0.35
                drive=0.2136
                tremolo=0.0264
                tremolo_hz=13.04
            """),
        ("powerup", """
            patch repeat=0.11315
            voice
                wave=sine
                freq=57.5946
                gain=0.22
                sustain=0.1306122449
                decay=0.1777777778
                pitch_ramp=-0.208544
                vibrato=0.09
                vibrato_hz=3.9602
                drive=0.12
                tremolo=0.0216
                tremolo_hz=13.94
            """),
        ("hit", """
            voice
                wave=noise
                freq=51.4206
                gain=0.22
                sustain=0.00566893424
                decay=0.09070294785
                pitch_ramp=1.050624
                hpf=0.12
                noise=0.35
                drive=0.12
            """),
        ("jump", """
            voice
                wave=square
                freq=78.2334
                gain=0.22
                sustain=0.1097505669
                decay=0.07346938776
                pitch_ramp=-0.101156
                duty=0.38
                lpf=0.72
                hpf=0.05
                drive=0.12
            """),
        ("blip", """
            voice
                wave=sine
                freq=78.2334
                gain=0.22
                sustain=0.03832199546
                decay=0.01451247166
                hpf=0.1
                drive=0.12
            """)
    ];

    public const string ClassicSfxrAbstractGolfScript = """
        defaults gain=0.22 drive=0.12
        template name=Noise
            wave=noise
            noise=0.35

        voice
            wave=square
            freq=148.7934
            sustain=0.01451247166
            decay=0.1306122449
            punch=0.45
            drive=0.201
            arp_delay=0.081844
            arp_mult=1.116121255

        voice
            wave=saw
            freq=229.0554
            sustain=0.08185941043
            decay=0.07346938776
            pitch_ramp=0.703836
            duty=0.31
            duty_ramp=0.056
            hpf=0.04
            phaser=0.0001458
            phaser_ramp=-0.000504

        patch repeat=0.174744
        voice
            use=Noise
            freq=20
            sustain=0.1907029478
            decay=0.293877551
            punch=0.52
            pitch_ramp=0.0320625
            phaser=-0.0008712
            phaser_ramp=-0.00035
            vibrato=0.11
            vibrato_hz=3.4112
            drive=0.2136
            tremolo=0.0264
            tremolo_hz=13.04

        patch repeat=0.11315
        voice
            wave=sine
            freq=57.5946
            sustain=0.1306122449
            decay=0.1777777778
            pitch_ramp=-0.208544
            vibrato=0.09
            vibrato_hz=3.9602
            tremolo=0.0216
            tremolo_hz=13.94

        voice
            use=Noise
            freq=51.4206
            sustain=0.00566893424
            decay=0.09070294785
            pitch_ramp=1.050624
            hpf=0.12

        voice
            wave=square
            freq=78.2334
            sustain=0.1097505669
            decay=0.07346938776
            pitch_ramp=-0.101156
            duty=0.38
            lpf=0.72
            hpf=0.05

        voice
            wave=sine
            freq=78.2334
            sustain=0.03832199546
            decay=0.01451247166
            hpf=0.1
        """;

    public static readonly IReadOnlyList<string> BfxrNames = ["coin-spark", "shield-pop", "ui-bloom", "portal-chirp"];

    public static readonly IReadOnlyList<(string Name, string Script)> BfxrReferenceScripts =
    [
        ("coin-spark", """
            defaults
                wave=sine
                gain=0.18
                attack=0
                sustain=0.022
                decay=0.42
                hpf=0.05
                drive=0.08

            voice
                freq=1180
                fm=5
                fm_index=3.2
                fm_decay=0.16

            voice
                freq=1770
                gain=0.09
                fm=7
                fm_index=1.7
                fm_decay=0.11
            """),
        ("shield-pop", """
            defaults
                wave=triangle
                gain=0.2
                sustain=0.04
                decay=0.36
                punch=0.32
                lpf=0.68
                hpf=0.03
                drive=0.12

            voice
                freq=260
                pitch_ramp=-0.75
                vibrato=0.04
                vibrato_hz=7.2

            voice
                wave=noise
                freq=900
                gain=0.05
                sustain=0.018
                decay=0.16
                noise=0.7
                hpf=0.4
            """),
        ("ui-bloom", """
            patch repeat=0.18
            defaults
                wave=sine
                gain=0.12
                sustain=0.05
                decay=0.5
                punch=0.22
                lpf=0.78
                drive=0.06

            voice
                freq=392
                pitch_ramp=0.18
                arp_delay=0.08
                arp_mult=1.25

            voice
                freq=588
                gain=0.06
                attack=0.004
                sustain=0.04
                decay=0.38
            """),
        ("portal-chirp", """
            defaults
                wave=saw
                freq=320
                gain=0.14
                sustain=0.09
                decay=0.28
                pitch_ramp=0.85
                duty=0.38
                hpf=0.08
                lpf=0.72
                drive=0.16
                fold=0.08

            mod name=waver
                wave=sine
                hz=9
                pitch=0.018
                duty=0.06
                lpf=0.08

            voice
            voice wave=square freq=640 gain=0.05 duty=0.44
            """)
    ];

    public static readonly IReadOnlyList<string> Classic808Names = ["kick", "snare", "clap", "hat", "tom", "cowbell"];

    public static readonly IReadOnlyList<(string Name, string Script)> Classic808PrimitiveGolfScripts =
    [
        ("kick", """
            defaults wave=sine gain=0.8 drive=0.18
            voice
                freq=58
                sustain=0.045
                decay=0.42
                punch=0.65
                pitch_ramp=-3.8
                min_freq=32
                lpf=0.85
            """),
        ("snare", """
            defaults drive=0.12
            template name=Noise
                wave=noise
                noise=0.85
                hpf=0.45
                lpf=0.55

            voice
                wave=sine
                freq=180
                gain=0.08
                sustain=0.02
                decay=0.12
                pitch_ramp=-1.2

            voice
                use=Noise
                freq=140
                gain=0.55
                sustain=0.035
                decay=0.2
            """),
        ("clap", """
            defaults
                wave=noise
                noise=0.95
                hpf=0.55
                lpf=0.42
                gain=0.22
                decay=0.11
                drive=0.1

            voice freq=1800 sustain=0.018 phaser=0.004
            voice freq=2200 sustain=0.022 phaser=0.008
            voice freq=2600 sustain=0.028 phaser=0.012
            """),
        ("hat", """
            defaults hpf=0.9 lpf=0.24 drive=0.05
            voice
                wave=noise
                freq=9000
                gain=0.16
                sustain=0.006
                decay=0.055
                noise=1

            voice
                wave=square
                freq=6800
                gain=0.045
                sustain=0.005
                decay=0.04
            """),
        ("tom", """
            defaults wave=sine gain=0.55 drive=0.12
            voice
                freq=115
                sustain=0.055
                decay=0.34
                punch=0.28
                pitch_ramp=-1.45
                min_freq=62
                lpf=0.78
            """),
        ("cowbell", """
            defaults wave=square hpf=0.18 lpf=0.82 drive=0.16
            voice
                freq=540
                gain=0.16
                sustain=0.05
                decay=0.18
                duty=0.43

            voice
                freq=800
                gain=0.12
                sustain=0.045
                decay=0.16
                duty=0.47
            """)
    ];

    public static readonly IReadOnlyList<string> FmBellNames = ["bell", "chime", "coin", "gong"];

    public static readonly IReadOnlyList<(string Name, string Script)> FmBellPrimitiveGolfScripts =
    [
        ("bell", """
            defaults wave=sine gain=0.24 attack=0.002 sustain=0.04 decay=1.2 lpf=0.9
            template name=Overtone
                fm=4.1
                fm_decay=0.55

            voice use=Overtone freq=440 fm_index=5.8
            voice use=Overtone freq=880 gain=0.08 fm_index=2.4
            """),
        ("chime", """
            defaults wave=sine gain=0.18 attack=0.001 sustain=0.025 decay=0.9 hpf=0.02
            template name=Overtone
                fm=3
                fm_decay=0.38

            voice use=Overtone freq=660 fm_index=4.2
            voice use=Overtone freq=990 gain=0.07 fm_index=2.1
            """),
        ("coin", """
            defaults wave=sine attack=0 sustain=0.02 decay=0.45 hpf=0.04 drive=0.08
            voice freq=1200 gain=0.18 fm=5 fm_index=3.4 fm_decay=0.18
            voice freq=1800 gain=0.09 fm=7 fm_index=1.8 fm_decay=0.12
            """),
        ("gong", """
            defaults wave=sine attack=0.003 sustain=0.08 decay=1.6 lpf=0.82 drive=0.1
            voice freq=196 gain=0.24 fm=2.414 fm_index=7.2 fm_decay=0.9
            voice freq=311 gain=0.12 fm=3.73 fm_index=4.1 fm_decay=0.7
            """)
    ];

    public static readonly IReadOnlyList<string> WobbleBassNames = ["talker", "growl", "yoy", "neuro"];

    public static readonly IReadOnlyList<(string Name, string Script)> WobbleBassPrimitiveGolfScripts =
    [
        ("talker", """
            defaults
                wave=saw
                freq=55
                gain=0.18
                sustain=0.8
                decay=0.25
                lpf=0.34
                hpf=0.02
                drive=0.3
                fold=0.08
                fm=2
                fm_index=0.8
                fm_decay=0.7
                formants=520:90:0.7,1250:170:1,2600:320:0.45
                formant_mix=0.35

            mod name=wobble
                hz=4
                wave=triangle
                gain=0.42
                lpf=0.48
                formant_mix=0.38
                fm_index=1.6
                drive=0.2
                fold=0.14

            voice
            voice freq=110 gain=0.08 duty=0.42
            """),
        ("growl", """
            defaults
                wave=saw
                freq=44
                gain=0.2
                sustain=0.7
                decay=0.3
                lpf=0.28
                hpf=0.01
                drive=0.42
                fold=0.18
                fm=1.5
                fm_index=1.2
                fm_decay=0.45
                noise=0.05

            mod name=wobble
                hz=6
                wave=sine
                gain=0.32
                lpf=0.55
                pitch=0.035
                drive=0.28
                fold=0.22
                noise=0.08
                fm_index=2.2

            voice
            voice wave=square freq=88 gain=0.09 duty=0.36
            """),
        ("yoy", """
            defaults
                wave=square
                freq=62
                gain=0.16
                sustain=0.65
                decay=0.22
                lpf=0.3
                hpf=0.03
                drive=0.25
                fm=3
                fm_index=0.7
                fm_decay=0.5
                formants=400:80:0.6,900:120:1,2100:260:0.4
                formant_mix=0.28

            mod name=wobble
                hz=5
                wave=square
                gain=0.5
                lpf=0.5
                formant_mix=0.45
                pitch=0.025
                duty=0.08

            voice
            voice wave=saw freq=124 gain=0.07
            """),
        ("neuro", """
            defaults
                wave=saw
                freq=49
                gain=0.16
                sustain=0.75
                decay=0.28
                lpf=0.25
                hpf=0.04
                drive=0.38
                fold=0.24
                fm=2.7
                fm_index=1.5
                fm_decay=0.35
                noise=0.04

            mod name=wobble
                hz=7
                wave=hold
                gain=0.28
                lpf=0.52
                pitch=0.04
                drive=0.25
                fold=0.3
                noise=0.1
                fm_index=2.8

            voice
            voice wave=triangle freq=147 gain=0.06
            """)
    ];

    public static IEnumerable<(string Family, string Name, string Script)> ReferenceScripts()
    {
        foreach (var item in ClassicSfxrPrimitiveGolfScripts) yield return ("sfxr", item.Name, item.Script);
        foreach (var item in BfxrReferenceScripts) yield return ("bfxr", item.Name, item.Script);
        foreach (var item in Classic808PrimitiveGolfScripts) yield return ("808", item.Name, item.Script);
        foreach (var item in FmBellPrimitiveGolfScripts) yield return ("fm-bell", item.Name, item.Script);
        foreach (var item in WobbleBassPrimitiveGolfScripts) yield return ("wobble-bass", item.Name, item.Script);
    }

    public static IEnumerable<(string Family, string Name, string Script)> PrimitiveGolfScripts() => ReferenceScripts();
}
