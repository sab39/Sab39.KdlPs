using System.Text;

namespace Sab39.KdlPs;

/// <summary>
/// Decides which convenience members a node gets, and what they're called: one per KDL property, plus
/// Arg1, Arg2... for its arguments, and Argument when it has exactly one.
/// </summary>
internal static class GeneratedMembers
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
    /// Takes the arguments, and the properties with distinct keys in source order, and returns the
    /// members to generate - argument members first.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, object?>> Plan(
        IReadOnlyList<object?> arguments,
        IEnumerable<KeyValuePair<string, object?>> properties)
    {
        var propertyMembers = PlanProperties(properties);

        // The document's own properties win over these, the same way a real name property wins over
        // the Name alias.
        HashSet<string> claimed = new(propertyMembers.Select(m => m.Key), StringComparer.OrdinalIgnoreCase);
        var argumentMembers = arguments.Select((value, index) => new KeyValuePair<string, object?>($"Arg{index + 1}", value));
        if (arguments.Count == 1) argumentMembers = argumentMembers.Prepend(new("Argument", arguments[0]));

        return [.. argumentMembers.Where(m => !claimed.Contains(m.Key)), .. propertyMembers];
    }

    /// <summary>
    /// Keys converting to the same member name - case-insensitively, as PowerShell compares them -
    /// produce one member: a key already spelled exactly as its member name wins, and otherwise the
    /// first key does.
    /// </summary>
    private static List<KeyValuePair<string, object?>> PlanProperties(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        var candidates = properties
            .Select(p => (Property: p, Name: ToPascalCase(p.Key)))
            .Where(c => c.Name.Length > 0)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(sameName => sameName.FirstOrDefault(c => c.Property.Key == c.Name, sameName.First()));

        return [.. candidates.Select(c => new KeyValuePair<string, object?>(c.Name, c.Property.Value))];
    }
}
