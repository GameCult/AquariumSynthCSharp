namespace AquaSynth.Dsl;

public sealed record PatchScriptMetrics(
    int ByteCount,
    int LineCount,
    int StatementCount,
    int FieldCount,
    float AverageFieldsPerStatement,
    float AliasFieldRatio,
    float TerseScore,
    float ReadabilityScore,
    float BalancedScore);

public static class PatchScriptScoring
{
    public static PatchScriptMetrics Measure(string script)
    {
        var statements = PatchScriptStatements.Enumerate(script)
            .Select(statement => statement.Text)
            .ToList();
        var statementCount = statements.Count;
        var lineCount = script.Replace("\r\n", "\n")
            .Split('\n')
            .Count(line => !line.Split('#', 2)[0].Trim().Equals(""))
            .ClampMin(1);

        var fieldCount = 0;
        var aliasFields = 0;
        var namedCommands = 0;
        var numericChars = 0;
        var numericValues = 0;

        foreach (var statement in statements)
        {
            var parts = statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts[0].Length > 1) namedCommands++;
            foreach (var part in parts.Skip(1))
            {
                var split = part.Split('=', 2);
                if (split.Length != 2) continue;
                fieldCount++;
                if (split[0].Length <= 2) aliasFields++;
                if (float.TryParse(split[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    numericValues++;
                    numericChars += split[1].Count(char.IsAsciiDigit);
                }
            }
        }

        var averageFieldsPerStatement = fieldCount / (float)Math.Max(1, statementCount);
        var aliasFieldRatio = aliasFields / (float)Math.Max(1, fieldCount);
        var namedCommandRatio = namedCommands / (float)Math.Max(1, statementCount);
        var lineRoom = Math.Clamp(lineCount / (float)Math.Max(1, statementCount), 0, 1);
        var fieldLoad = Math.Max(0, 1 - Math.Clamp(averageFieldsPerStatement / 16, 0, 1));
        var numericBreath = numericValues == 0
            ? 1
            : Math.Clamp(1 - Math.Max(0, numericChars / (float)numericValues - 4) / 8, 0, 1);
        var readabilityScore = Math.Clamp(
            0.30f * (1 - aliasFieldRatio) +
            0.20f * namedCommandRatio +
            0.20f * lineRoom +
            0.15f * fieldLoad +
            0.15f * numericBreath,
            0,
            1);
        var byteCount = script.Trim().Length;
        var terseScore = Math.Clamp(1 / (1 + byteCount / 160f), 0, 1);
        var balancedScore = readabilityScore + terseScore <= float.Epsilon
            ? 0
            : 2 * readabilityScore * terseScore / (readabilityScore + terseScore);

        return new PatchScriptMetrics(
            byteCount,
            lineCount,
            statementCount,
            fieldCount,
            averageFieldsPerStatement,
            aliasFieldRatio,
            terseScore,
            readabilityScore,
            balancedScore);
    }
}

file static class IntExtensions
{
    public static int ClampMin(this int value, int min) => Math.Max(value, min);
}
