using System.Text;

namespace Sab39.KdlPs;

/// <summary>
/// Decides which KDL properties become generated members on a node, and what they're called.
/// </summary>
internal static class PropertyMemberNames
{
    /// <summary>
    /// Capitalizes each run of letters and digits and drops everything else, so first-name,
    /// first_name and firstName all become FirstName.
    /// </summary>
    public static string ToPascalCase(string key)
    {
        StringBuilder result = new(key.Length);
        var startsWord = true;
        foreach (var c in key)
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(startsWord ? char.ToUpperInvariant(c) : c);
                startsWord = false;
            }
            else
            {
                startsWord = true;
            }
        }

        return $"{result}";
    }

    /// <summary>
    /// Takes properties with distinct keys in source order, and returns the members to generate.
    /// Keys converting to the same member name - case-insensitively, as PowerShell compares them -
    /// produce one member: a key already spelled exactly as its member name wins, and otherwise the
    /// first key does.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, object?>> Plan(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        var candidates = properties
            .Select(p => (Property: p, Name: ToPascalCase(p.Key)))
            .Where(c => c.Name.Length > 0)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(sameName => sameName.FirstOrDefault(c => c.Property.Key == c.Name, sameName.First()));

        return [.. candidates.Select(c => new KeyValuePair<string, object?>(c.Name, c.Property.Value))];
    }
}
