using System.Diagnostics;

namespace AquariumSynth.Dsl.Tests;

public sealed class ZynReferenceParityTests
{
    [Fact]
    public async Task ZynAddSubFxReferenceSourceIsPinnedForTestOnlyParity()
    {
        var root = RepositoryRoot();
        var zynRoot = Path.Combine(root, "external", "zynaddsubfx");

        Assert.True(Directory.Exists(zynRoot), "ZynAddSubFX reference source submodule is missing. Run `git submodule update --init --recursive`.");
        Assert.True(File.Exists(Path.Combine(zynRoot, "COPYING")), "ZynAddSubFX reference source should keep its GPL license file.");
        Assert.True(File.Exists(Path.Combine(zynRoot, "src", "Params", "PADnoteParameters.cpp")), "PAD parameter implementation is the parity source, not Aquarium runtime code.");
        Assert.True(File.Exists(Path.Combine(zynRoot, "src", "Synth", "PADnote.cpp")), "PAD note implementation is the parity source, not Aquarium runtime code.");
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", await File.ReadAllTextAsync(Path.Combine(zynRoot, "COPYING")));

        var revision = await RunAsync("git", ["-C", zynRoot, "rev-parse", "HEAD"]);
        Assert.Equal(0, revision.ExitCode);
        Assert.Equal("3ab608c432996ba4d582176572c0b0f82328c825", revision.Stdout.Trim());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AquariumSynthCSharp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("could not find repository root");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"failed to start `{fileName}`");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }
}
