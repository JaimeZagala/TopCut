using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TopCut.Domain.Catalog;

namespace TopCut.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores an ordered tiebreaker list as a short comma-separated column (<c>"2,3,4"</c>) instead of
/// a child table. The order is the whole point of the list, a comma-separated column preserves it
/// for free, and nothing ever queries an individual tiebreaker.
/// </summary>
internal static class TiebreakerListConverter
{
    public static readonly ValueConverter<List<Tiebreaker>, string> Converter = new(
        list => string.Join(',', list.Select(t => (int)t)),
        text => Parse(text));

    /// <summary>
    /// EF needs this to know when a list changed: without it, reordering the tiebreakers in place
    /// would not be detected and the change would never reach the database.
    /// </summary>
    public static readonly ValueComparer<List<Tiebreaker>> Comparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        list => list.Aggregate(0, (hash, t) => HashCode.Combine(hash, (int)t)),
        list => list.ToList());

    private static List<Tiebreaker> Parse(string text) => string.IsNullOrWhiteSpace(text)
        ? []
        : [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(part => (Tiebreaker)int.Parse(part))];
}
