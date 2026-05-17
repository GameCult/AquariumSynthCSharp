namespace AquaSynth.Dsl;

internal readonly record struct PatchScriptStatement(string Text, int Line);

internal static class PatchScriptStatements
{
    public static IEnumerable<PatchScriptStatement> Enumerate(string script)
    {
        string? pending = null;
        var pendingLine = 1;
        var lineNumber = 1;

        foreach (var rawLine in script.Replace("\r\n", "\n").Split('\n'))
        {
            var uncommented = rawLine.Split('#', 2)[0];
            var segments = uncommented.Split(';');
            for (var index = 0; index < segments.Length; index++)
            {
                var statement = segments[index].Trim();
                if (statement.Length > 0)
                {
                    if (pending is not null && IsFieldContinuation(statement))
                    {
                        pending = $"{pending} {statement}";
                    }
                    else
                    {
                        if (pending is not null)
                        {
                            yield return new PatchScriptStatement(pending, pendingLine);
                        }

                        pending = statement;
                        pendingLine = lineNumber;
                    }
                }

                if (index < segments.Length - 1 && pending is not null)
                {
                    yield return new PatchScriptStatement(pending, pendingLine);
                    pending = null;
                }
            }

            lineNumber++;
        }

        if (pending is not null)
        {
            yield return new PatchScriptStatement(pending, pendingLine);
        }
    }

    private static bool IsFieldContinuation(string statement)
    {
        var firstTokenEnd = statement.IndexOfAny([' ', '\t']);
        var firstToken = firstTokenEnd < 0 ? statement : statement[..firstTokenEnd];
        return firstToken.Contains('=', StringComparison.Ordinal);
    }
}
