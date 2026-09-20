using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TopCut.Domain.Catalog;

namespace TopCut.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores the attendance table as one compact column, <c>"8:3:;16:5:4;32:5:8"</c>, in the same
/// spirit as the tiebreaker list: a handful of ordered rows that nothing ever queries individually,
/// so a child table would add a join and a migration for no gain.
/// </summary>
internal static class AttendanceBandConverter
{
    public static readonly ValueConverter<List<AttendanceBand>, string> Converter = new(
        bands => string.Join(';', bands.Select(band => $"{band.UpToPlayers}:{band.SwissRounds}:{band.TopCutSize}")),
        text => Parse(text));

    /// <summary>Without this EF would never notice a revised table and the change would never save.</summary>
    public static readonly ValueComparer<List<AttendanceBand>> Comparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        bands => bands.Aggregate(0, (hash, band) => HashCode.Combine(hash, band.GetHashCode())),
        bands => bands.ToList());

    private static List<AttendanceBand> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return [.. text
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split(':'))
            .Select(parts => new AttendanceBand(
                UpToPlayers: int.Parse(parts[0]),
                SwissRounds: int.Parse(parts[1]),
                TopCutSize: string.IsNullOrEmpty(parts[2]) ? null : int.Parse(parts[2])))];
    }
}
