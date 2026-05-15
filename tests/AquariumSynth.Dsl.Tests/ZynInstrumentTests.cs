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

    [Fact]
    public void ToleratesLeadingWhitespaceBeforeXmlDeclaration()
    {
        var instrument = ZynInstrumentReader.Parse(Encoding.UTF8.GetBytes("""

              <?xml version="1.0" encoding="UTF-8"?>
              <ZynAddSubFX-data>
                <INSTRUMENT>
                  <INFO><string name="name">Loose XML</string></INFO>
                  <INSTRUMENT_KIT>
                    <INSTRUMENT_KIT_ITEM id="0">
                      <par_bool name="enabled" value="yes"/>
                      <par_bool name="add_enabled" value="yes"/>
                    </INSTRUMENT_KIT_ITEM>
                  </INSTRUMENT_KIT>
                </INSTRUMENT>
              </ZynAddSubFX-data>
            """));

        Assert.Equal("Loose XML", instrument.Name);
        AssertFeature(instrument, "engine_add", "1");
    }

    [Fact]
    public void CountsOnlyActualFormantFilterBlocks()
    {
        var instrument = ZynInstrumentReader.Parse(Encoding.UTF8.GetBytes("""
            <ZynAddSubFX-data>
              <INSTRUMENT>
                <INFO><string name="name">Formant Counting</string></INFO>
                <INSTRUMENT_KIT>
                  <INSTRUMENT_KIT_ITEM id="0">
                    <par_bool name="enabled" value="yes"/>
                    <par_bool name="add_enabled" value="yes"/>
                    <ADD_SYNTH_PARAMETERS>
                      <FILTER_PARAMETERS>
                        <FORMANT_FILTER>
                          <par name="num_formants" value="3"/>
                          <VOWEL id="0"><FORMANT id="0"/><FORMANT id="1"/></VOWEL>
                        </FORMANT_FILTER>
                      </FILTER_PARAMETERS>
                    </ADD_SYNTH_PARAMETERS>
                  </INSTRUMENT_KIT_ITEM>
                </INSTRUMENT_KIT>
              </INSTRUMENT>
            </ZynAddSubFX-data>
            """));

        AssertFeature(instrument, "formant_filter_count", "1");
    }

    [Fact]
    public void SurveyRanksWorstFeaturePressure()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ZynAddSubFX", "ProjectAuthored");

        var survey = ZynInstrumentSurvey.RankDirectory(root, take: 3);

        Assert.Equal(3, survey.Count);
        Assert.Equal("vocal-layer.xiz", survey[0].Path);
        Assert.True(survey[0].ComplexityScore > survey[^1].ComplexityScore);
        Assert.Contains(survey[0].Features, feature => feature.Name == "formant_filter_count" && feature.Value == "1");
    }

    [Fact]
    public async Task SurveysUpstreamGplInstrumentBankWhenAvailable()
    {
        var root = RepositoryRoot();
        var bankRoot = Path.Combine(root, "external", "zynaddsubfx", "instruments", "banks");
        if (!Directory.Exists(bankRoot))
        {
            return;
        }

        var files = Directory.GetFiles(bankRoot, "*.xiz", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            return;
        }

        var survey = ZynInstrumentSurvey.RankDirectory(bankRoot, take: 20);
        Assert.NotEmpty(survey);
        Assert.Contains(survey, item => item.Features.Any(feature => feature.Name == "engine_pad" && feature.Value != "0"));

        var artifactDir = Path.Combine(root, "artifacts", "parity", "zyn-upstream-bank-survey");
        Directory.CreateDirectory(artifactDir);
        var report = new List<string>
        {
            "ZynAddSubFX upstream GPL instrument bank survey",
            "source: https://github.com/zynaddsubfx/instruments",
            "license: GPL test/development corpus via upstream ZynAddSubFX instruments submodule",
            "usage: feature pressure and parity fixtures only; never NuGet/package content",
            $"bank_root: {bankRoot}",
            $"xiz_count: {files.Length}",
            ""
        };
        foreach (var item in survey)
        {
            var features = item.Features.ToDictionary(feature => feature.Name, feature => feature.Value);
            report.Add($"{item.ComplexityScore}\t{item.Path}\t{item.Name}\tpad={features["engine_pad"]} add={features["engine_add"]} sub={features["engine_sub"]} layers={features["enabled_kit_items"]} env={features["envelope_count"]} free={features["free_envelope_count"]} lfo={features["lfo_count"]} filter={features["filter_count"]} formant={features["formant_filter_count"]} fx={features["effect_count"]}");
        }

        await File.WriteAllLinesAsync(Path.Combine(artifactDir, "report.txt"), report);
    }

    private static void AssertFeature(ZynInstrument instrument, string name, string value) =>
        Assert.Contains(instrument.Features(), feature => feature.Name == name && feature.Value == value);

    private static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AquariumSynthCSharp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("could not find repository root");
    }
}
