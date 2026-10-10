namespace Sab39.KdlPs;

/// <summary>
/// Decodes Ascii85, which is what KDL's base85 annotation means. Accepts the optional &lt;~ ~&gt;
/// delimiters, z for a group of four zero bytes, and whitespace anywhere.
/// </summary>
internal static class Ascii85
{
    public static byte[] Decode(string text)
    {
        var body = text.Trim();
        if (body.StartsWith("<~", StringComparison.Ordinal)) body = body[2..];
        if (body.EndsWith("~>", StringComparison.Ordinal)) body = body[..^2];

        List<byte> bytes = [];
        ulong group = 0;
        var count = 0;
        foreach (var c in body)
        {
            if (char.IsWhiteSpace(c)) continue;

            if (c == 'z')
            {
                if (count != 0) throw new FormatException("'z' can only appear between groups.");
                bytes.AddRange([0, 0, 0, 0]);
                continue;
            }

            if (c is < '!' or > 'u') throw new FormatException($"'{c}' isn't an Ascii85 character.");

            group = (group * 85) + (ulong)(c - '!');
            if (++count == 5)
            {
                AppendGroup(bytes, group, 4);
                group = 0;
                count = 0;
            }
        }

        if (count == 1) throw new FormatException("A final Ascii85 group needs at least two characters.");

        // A short final group is padded with the highest digit, and yields one byte fewer than its length.
        if (count > 0)
        {
            for (var i = count; i < 5; i++) group = (group * 85) + 84;
            AppendGroup(bytes, group, count - 1);
        }

        return [.. bytes];
    }

    private static void AppendGroup(List<byte> bytes, ulong group, int byteCount)
    {
        if (group > uint.MaxValue) throw new FormatException("An Ascii85 group is out of range.");
        for (var i = 0; i < byteCount; i++) bytes.Add((byte)(group >> (24 - (8 * i))));
    }
}
