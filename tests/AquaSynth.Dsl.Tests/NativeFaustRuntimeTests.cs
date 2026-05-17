using AquaSynth.Dsl;
using AquaSynth.Faust;

namespace AquaSynth.Dsl.Tests;

public sealed class NativeFaustRuntimeTests
{
    [Fact]
    public void CompileKeyTracksRevisionNameAndScript()
    {
        var first = new AquaSynthCompileIdentity("patch", "tone", "voice wave=sine freq=440 gain=.1", 1);
        var same = new AquaSynthCompileIdentity("patch", "tone", "voice wave=sine freq=440 gain=.1", 1);
        var changed = new AquaSynthCompileIdentity("patch", "tone", "voice wave=sine freq=441 gain=.1", 1);

        Assert.Equal(first.CompileKey, same.CompileKey);
        Assert.NotEqual(first.CompileKey, changed.CompileKey);
    }

    [Fact]
    public void NativeFaustRendererRendersAudiblePatchWhenToolchainIsAvailable()
    {
        const string script = """
            voice
                wave=sine
                freq=440
                gain=0.2
                attack=0.001
                sustain=0.06
                decay=0.12
            """;

        if (!AquaSynthNativeCompiler.TryRenderScript("native_smoke", script, 1.0f, out var samples, out var error))
        {
            if (error?.Contains("Faust toolchain not found", StringComparison.OrdinalIgnoreCase) == true ||
                error?.Contains("Faust DLL not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            Assert.Fail($"AquaSynth native Faust render failed: {error}");
        }

        Assert.True(samples.Length > 2048, $"Rendered too few samples: {samples.Length}.");
        Assert.Contains(samples, sample => MathF.Abs(sample) > 0.001f);
        Assert.InRange(samples.Max(sample => MathF.Abs(sample)), 0.001f, 1.0f);
    }
}
