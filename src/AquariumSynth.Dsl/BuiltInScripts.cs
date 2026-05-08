namespace AquariumSynth.Dsl;

public static class BuiltInScripts
{
    public const string PatchScriptExample = """
        # One command per line. Comments start with #.
        patch gain=0.7 soft_clip=true
        mod name=wobble wave=sine hz=5 pitch=0.01 formant=0.08
        bus n=shimmer w=triangle hz=2 to=gain:0.08,lpf:-0.04,fmix:0.12
        voice wave=sine freq=220 gain=0.12 attack=0.002 sustain=0.03 decay=0.2 vibrato=0.02 vibrato_hz=5 formants=620:90:1,1040:150:0.8 formant_mix=0.45 mods=formant:sine:2:0.12,pitch:triangle:3:0.015
        voice wave=triangle freq=440 gain=0.04 attack=0 sustain=0.02 decay=0.18 lpf=0.7 hpf=0.02 mods=gain:sine:8:0.18,lpf:hold:12:0.08
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
        ("pickup", "v w=sq f=148.7934 g=.22 s=.01451247166 d=.1306122449 pu=.45 drv=.201 ad=.081844 am=1.116121255"),
        ("laser", "v w=saw f=229.0554 g=.22 s=.08185941043 d=.07346938776 pr=.703836 du=.31 dur=.056 h=.04 ph=.0001458 phr=-.000504 drv=.12"),
        ("explosion", "p r=.174744;v w=n f=20 g=.22 s=.1907029478 d=.293877551 pu=.52 pr=.0320625 ph=-.0008712 phr=-.00035 vi=.11 vh=3.4112 nz=.35 drv=.2136 tr=.0264 th=13.04"),
        ("powerup", "p r=.11315;v w=sin f=57.5946 g=.22 s=.1306122449 d=.1777777778 pr=-.208544 vi=.09 vh=3.9602 drv=.12 tr=.0216 th=13.94"),
        ("hit", "v w=n f=51.4206 g=.22 s=.00566893424 d=.09070294785 pr=1.050624 h=.12 nz=.35 drv=.12"),
        ("jump", "v w=sq f=78.2334 g=.22 s=.1097505669 d=.07346938776 pr=-.101156 du=.38 l=.72 h=.05 drv=.12"),
        ("blip", "v w=sin f=78.2334 g=.22 s=.03832199546 d=.01451247166 h=.1 drv=.12")
    ];

    public const string ClassicSfxrAbstractGolfScript =
        "d g=.22 drv=.12;" +
        "def name=N w=n nz=.35;" +
        "v w=sq f=148.7934 s=.01451247166 d=.1306122449 pu=.45 drv=.201 ad=.081844 am=1.116121255;" +
        "v w=saw f=229.0554 s=.08185941043 d=.07346938776 pr=.703836 du=.31 dur=.056 h=.04 ph=.0001458 phr=-.000504;" +
        "p r=.174744;v u=N f=20 s=.1907029478 d=.293877551 pu=.52 pr=.0320625 ph=-.0008712 phr=-.00035 vi=.11 vh=3.4112 drv=.2136 tr=.0264 th=13.04;" +
        "p r=.11315;v w=sin f=57.5946 s=.1306122449 d=.1777777778 pr=-.208544 vi=.09 vh=3.9602 tr=.0216 th=13.94;" +
        "v u=N f=51.4206 s=.00566893424 d=.09070294785 pr=1.050624 h=.12;" +
        "v w=sq f=78.2334 s=.1097505669 d=.07346938776 pr=-.101156 du=.38 l=.72 h=.05;" +
        "v w=sin f=78.2334 s=.03832199546 d=.01451247166 h=.1";

    public static readonly IReadOnlyList<string> Classic808Names = ["kick", "snare", "clap", "hat", "tom", "cowbell"];

    public static readonly IReadOnlyList<(string Name, string Script)> Classic808PrimitiveGolfScripts =
    [
        ("kick", "d w=sin g=.8 drv=.18;v f=58 s=.045 d=.42 pu=.65 pr=-3.8 min=32 l=.85"),
        ("snare", "d drv=.12;def n=N w=n nz=.85 h=.45 l=.55;v w=sin f=180 g=.08 s=.02 d=.12 pr=-1.2;v u=N f=140 g=.55 s=.035 d=.2"),
        ("clap", "d w=n nz=.95 h=.55 l=.42 g=.22 d=.11 drv=.1;v f=1800 s=.018 ph=.004;v f=2200 s=.022 ph=.008;v f=2600 s=.028 ph=.012"),
        ("hat", "d h=.9 l=.24 drv=.05;v w=n f=9000 g=.16 s=.006 d=.055 nz=1;v w=sq f=6800 g=.045 s=.005 d=.04"),
        ("tom", "d w=sin g=.55 drv=.12;v f=115 s=.055 d=.34 pu=.28 pr=-1.45 min=62 l=.78"),
        ("cowbell", "d w=sq h=.18 l=.82 drv=.16;v f=540 g=.16 s=.05 d=.18 du=.43;v f=800 g=.12 s=.045 d=.16 du=.47")
    ];

    public static readonly IReadOnlyList<string> FmBellNames = ["bell", "chime", "coin", "gong"];

    public static readonly IReadOnlyList<(string Name, string Script)> FmBellPrimitiveGolfScripts =
    [
        ("bell", "d w=sin g=.24 a=.002 s=.04 d=1.2 l=.9;def n=O fm=4.1 fmd=.55;v u=O f=440 fmi=5.8;v u=O f=880 g=.08 fmi=2.4"),
        ("chime", "d w=sin g=.18 a=.001 s=.025 d=.9 h=.02;def n=O fm=3 fmd=.38;v u=O f=660 fmi=4.2;v u=O f=990 g=.07 fmi=2.1"),
        ("coin", "d w=sin a=0 s=.02 d=.45 h=.04 drv=.08;v f=1200 g=.18 fm=5 fmi=3.4 fmd=.18;v f=1800 g=.09 fm=7 fmi=1.8 fmd=.12"),
        ("gong", "d w=sin a=.003 s=.08 d=1.6 l=.82 drv=.1;v f=196 g=.24 fm=2.414 fmi=7.2 fmd=.9;v f=311 g=.12 fm=3.73 fmi=4.1 fmd=.7")
    ];

    public static readonly IReadOnlyList<string> WobbleBassNames = ["talker", "growl", "yoy", "neuro"];

    public static readonly IReadOnlyList<(string Name, string Script)> WobbleBassPrimitiveGolfScripts =
    [
        ("talker", "d w=saw f=55 g=.18 s=.8 d=.25 l=.34 h=.02 drv=.3 fl=.08 fm=2 fmi=.8 fmd=.7 fs=520:90:.7,1250:170:1,2600:320:.45 fmix=.35;mod n=wob hz=4 w=tri g=.42 l=.48 fmix=.38 fmi=1.6 drv=.2 fl=.14;v;v f=110 g=.08 du=.42"),
        ("growl", "d w=saw f=44 g=.2 s=.7 d=.3 l=.28 h=.01 drv=.42 fl=.18 fm=1.5 fmi=1.2 fmd=.45 nz=.05;mod n=wob hz=6 w=sin g=.32 l=.55 p=.035 drv=.28 fl=.22 nz=.08 fmi=2.2;v;v w=sq f=88 g=.09 du=.36"),
        ("yoy", "d w=sq f=62 g=.16 s=.65 d=.22 l=.3 h=.03 drv=.25 fm=3 fmi=.7 fmd=.5 fs=400:80:.6,900:120:1,2100:260:.4 fmix=.28;mod n=wob hz=5 w=sq g=.5 l=.5 fmix=.45 p=.025 du=.08;v;v w=saw f=124 g=.07"),
        ("neuro", "d w=saw f=49 g=.16 s=.75 d=.28 l=.25 h=.04 drv=.38 fl=.24 fm=2.7 fmi=1.5 fmd=.35 nz=.04;mod n=wob hz=7 w=hold g=.28 l=.52 p=.04 drv=.25 fl=.3 nz=.1 fmi=2.8;v;v w=tri f=147 g=.06")
    ];

    public static IEnumerable<(string Family, string Name, string Script)> PrimitiveGolfScripts()
    {
        foreach (var item in ClassicSfxrPrimitiveGolfScripts) yield return ("sfxr", item.Name, item.Script);
        foreach (var item in Classic808PrimitiveGolfScripts) yield return ("808", item.Name, item.Script);
        foreach (var item in FmBellPrimitiveGolfScripts) yield return ("fm-bell", item.Name, item.Script);
        foreach (var item in WobbleBassPrimitiveGolfScripts) yield return ("wobble-bass", item.Name, item.Script);
    }
}
