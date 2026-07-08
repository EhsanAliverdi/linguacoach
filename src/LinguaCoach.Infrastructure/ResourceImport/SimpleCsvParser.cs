namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Minimal hand-rolled CSV parser (RFC-4180-ish): supports comma-delimited fields, double-quote
/// wrapping, escaped quotes via doubled "", and both \r\n and \n line endings. Deliberately not a
/// general-purpose CSV library — this codebase has no existing CSV dependency, and Phase E1's
/// scope only needs "header row + simple data rows" fidelity, not exotic CSV dialects.
/// </summary>
internal static class SimpleCsvParser
{
    /// <summary>Parses the whole CSV text into a header row plus data rows. Returns an empty
    /// list of rows (but a header, if present) when there is no data below the header.</summary>
    public static (IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows) Parse(string csvText)
    {
        var records = ParseRecords(csvText);
        if (records.Count == 0)
            return (Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());

        var header = records[0];
        var rows = records.Skip(1).Where(r => r.Count > 0 && !(r.Count == 1 && r[0].Length == 0)).ToList();
        return (header, rows);
    }

    private static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();
        var current = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    current.Add(field.ToString());
                    field.Clear();
                    i++;
                    break;
                case '\r':
                    i++;
                    break;
                case '\n':
                    current.Add(field.ToString());
                    field.Clear();
                    records.Add(current);
                    current = new List<string>();
                    i++;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        // Flush trailing field/record if the text didn't end with a newline.
        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            records.Add(current);
        }

        return records;
    }
}
