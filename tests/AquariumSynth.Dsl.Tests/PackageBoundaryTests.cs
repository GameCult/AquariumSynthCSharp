using System.Diagnostics;
using System.IO.Compression;

namespace AquariumSynth.Dsl.Tests;

public sealed class PackageBoundaryTests
{
    [Fact]
    public async Task NuGetPackageContainsOnlyPublishedSynthSurface()
    {
        var root = RepositoryRoot();
        var outputDir = Path.Combine(Path.GetTempPath(), $"aquarium-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            var result = await RunAsync(
                "dotnet",
                ["pack", Path.Combine(root, "src", "AquariumSynth.Dsl", "AquariumSynth.Dsl.csproj"), "-c", "Release", "--no-restore", "-o", outputDir]);
            Assert.Equal(0, result.ExitCode);

            var package = Assert.Single(Directory.GetFiles(outputDir, "AquariumSynth.Dsl.*.nupkg"));
            using var archive = ZipFile.OpenRead(package);
            var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

            Assert.Contains(entries, entry => entry.StartsWith("lib/net10.0/AquariumSynth.Dsl.dll", StringComparison.Ordinal));
            Assert.Contains("README.md", entries);
            Assert.DoesNotContain(entries, entry => entry.StartsWith("tests/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.StartsWith("Fixtures/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.StartsWith("patches/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.EndsWith(".aqua", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.EndsWith(".syx", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.EndsWith(".py", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
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
