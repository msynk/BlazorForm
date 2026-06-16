namespace BlazorForm.Core.Data;

/// <summary>One segment of a form path: either a named property or an array index.</summary>
public readonly struct PathSegment
{
    private PathSegment(string? name, int index)
    {
        Name = name;
        Index = index;
    }

    /// <summary>Property name, or null when this is an index segment.</summary>
    public string? Name { get; }

    /// <summary>Array index, or -1 when this is a property segment.</summary>
    public int Index { get; }

    public bool IsIndex => Name is null;

    public static PathSegment Property(string name) => new(name, -1);
    public static PathSegment At(int index) => new(null, index);

    public override string ToString() => IsIndex ? $"[{Index}]" : Name!;
}

/// <summary>
/// Parses and represents dotted/indexed form paths such as <c>address.city</c> or <c>items[0].name</c>.
/// </summary>
public static class FormPath
{
    /// <summary>Parses a path string into segments.</summary>
    public static IReadOnlyList<PathSegment> Parse(string path)
    {
        var segments = new List<PathSegment>();
        if (string.IsNullOrEmpty(path)) return segments;

        var i = 0;
        var current = new System.Text.StringBuilder();

        void FlushName()
        {
            if (current.Length > 0)
            {
                segments.Add(PathSegment.Property(current.ToString()));
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
                    segments.Add(PathSegment.At(int.Parse(inner)));
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
