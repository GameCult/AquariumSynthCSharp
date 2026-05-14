using System.IO.Compression;
using System.Text;
using AquariumSynth.Dsl;

namespace AquariumSynth.Dsl.Tests;

public sealed class ZynInstrumentTests
{
    [Fact]
    public void ParsesProjectAuthoredAdditiveLeadFixture()
    {
        var path = FixturePath("ZynAddSubFX", "ProjectAuthored", "additive-lead.xiz");
        var bytes = File.ReadAllBytes(path);

        var instrument = ZynInstrumentReader.Parse(bytes);
        var reference = instrument.ToReferencePatch(
            "zyn/project/additive-lead",
            ZynInstrumentReader.SourceForBytes("fixture://zyn/project/additive-lead.xiz", "project-authored", bytes));

        Assert.Equal("AQ Additive Lead", instrument.Name);
        Assert.Equal("AquariumSynthCSharp tests", instrument.Author);
        Assert.Contains(reference.Features, feature => feature.Name == "engine_add" && feature.Value == "1");
        Assert.Contains(reference.Features, feature => feature.Name == "engine_sub" && feature.Value == "0");
        Assert.Contains(reference.Features, feature => feature.Name == "engine_pad" && feature.Value == "0");
        Assert.Contains(reference.Features, feature => feature.Name == "envelope_count" && feature.Value == "2");
        Assert.Contains(reference.Features, feature => feature.Name == "lfo_count" && feature.Value == "1");
        Assert.Equal("zynaddsubfx", reference.Family);
        Assert.Equal("zyn-xiz", reference.Source.Kind);
        Assert.Equal(64, reference.Source.Hash.Length);
    }

    [Fact]
    public void ClassifiesPadAndFormantLayerPressure()
    {
        var pad = ZynInstrumentReader.ParseFile(FixturePath("ZynAddSubFX", "ProjectAuthored", "pad-texture.xiz"));
        var vocal = ZynInstrumentReader.ParseFile(FixturePath("ZynAddSubFX", "ProjectAuthored", "vocal-layer.xiz"));

        AssertFeature(pad, "engine_pad", "1");
        AssertFeature(pad, "filter_count", "1");
        AssertFeature(pad, "free_envelope_count", "1");

        AssertFeature(vocal, "layered_instrument", "yes");
        AssertFeature(vocal, "engine_add", "1");
        AssertFeature(vocal, "engine_sub", "1");
        AssertFeature(vocal, "formant_filter_count", "1");
        AssertFeature(vocal, "effect_count", "1");
    }

    [Fact]
    public void ParsesGzippedXizXml()
    {
        var xml = File.ReadAllText(FixturePath("ZynAddSubFX", "ProjectAuthored", "additive-lead.xiz"));
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(xml));
        }

        var instrument = ZynInstrumentReader.Parse(output.ToArray());

        Assert.Equal("AQ Additive Lead", instrument.Name);
        Assert.Single(instrument.KitItems);
    }

    [Fact]
    public void EngineEnableFlagsOverrideStaleParameterSections()
    {
        var instrument = ZynInstrumentReader.Parse(Encoding.UTF8.GetBytes("""
            <ZynAddSubFX-data>
              <INSTRUMENT>
                <INFO><string name="name">Stale Engine Section</string></INFO>
                <INSTRUMENT_KIT>
                  <INSTRUMENT_KIT_ITEM id="0">
                    <par_bool name="enabled" value="yes"/>
                    <par_bool name="pad_enabled" value="no"/>
                    <PAD_SYNTH_PARAMETERS/>
                  </INSTRUMENT_KIT_ITEM>
                </INSTRUMENT_KIT>
              </INSTRUMENT>
            </ZynAddSubFX-data>
            """));

        AssertFeature(instrument, "engine_pad", "0");
    }

    private static void AssertFeature(ZynInstrument instrument, string name, string value) =>
        Assert.Contains(instrument.Features(), feature => feature.Name == name && feature.Value == value);

    private static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);
}
