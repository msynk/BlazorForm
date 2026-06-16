namespace BlazorForm;

/// <summary>
/// Parses and represents dotted/indexed form paths such as <c>address.city</c> or <c>items[0].name</c>.
/// </summary>
public static class BlazorFormPath
{
    /// <summary>Parses a path string into segments.</summary>
    public static IReadOnlyList<BlazorFormPathSegment> Parse(string path)
    {
        var segments = new List<BlazorFormPathSegment>();
        if (string.IsNullOrEmpty(path)) return segments;

        var i = 0;
        var current = new System.Text.StringBuilder();

        void FlushName()
        {
            if (current.Length > 0)
            {
                segments.Add(BlazorFormPathSegment.Property(current.ToString()));
                current.Clear();
            }
        }

        while (i < path.Length)
        {
            var c = path[i];
            switch (c)
            {
                case '.':
                    FlushName();
                    i++;
                    break;
                case '[':
                    FlushName();
                    var close = path.IndexOf(']', i);
                    if (close < 0) throw new FormatException($"Unterminated index in path '{path}'.");
                    var inner = path[(i + 1)..close];
                    segments.Add(BlazorFormPathSegment.At(int.Parse(inner)));
                    i = close + 1;
                    break;
                default:
                    current.Append(c);
                    i++;
                    break;
            }
        }

        FlushName();
        return segments;
    }

    /// <summary>Joins a parent path and a child key into a single path.</summary>
    public static string Combine(string parent, string child)
        => string.IsNullOrEmpty(parent) ? child : $"{parent}.{child}";

    /// <summary>Builds an indexed path, e.g. <c>Combine("items", 2) => "items[2]"</c>.</summary>
    public static string Combine(string parent, int index) => $"{parent}[{index}]";
}
