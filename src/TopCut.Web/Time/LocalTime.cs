using Microsoft.Extensions.Configuration;

namespace TopCut.Web.Time;

/// <summary>
/// Converts between the wall-clock time the organiser types and the instant the domain stores
/// (ADR-0025).
/// <para>
/// It cannot use the machine's own time zone: locally that is Madrid and on Azure it is UTC, so the
/// same "18:00" typed into the same form would mean two different moments depending on where the
/// application happens to be running — and the tournament would start two hours late. The zone is
/// configuration (<c>TopCut:TimeZone</c>), not a guess.
/// </para>
/// </summary>
public sealed class LocalTime
{
    /// <summary>Where the v1 store is. Configuration overrides it without touching code.</summary>
    private const string DefaultZone = "Europe/Madrid";

    public LocalTime(IConfiguration configuration)
    {
        var id = configuration["TopCut:TimeZone"];

        Zone = TryFind(string.IsNullOrWhiteSpace(id) ? DefaultZone : id)
            ?? TryFind(DefaultZone)
            ?? TimeZoneInfo.Utc;
    }

    public TimeZoneInfo Zone { get; }

    /// <summary>The instant a date and a time of day refer to in the store's zone.</summary>
    public DateTimeOffset ToInstant(DateTime date, TimeSpan timeOfDay)
    {
        var wallClock = DateTime.SpecifyKind(date.Date + timeOfDay, DateTimeKind.Unspecified);

        return new DateTimeOffset(wallClock, Zone.GetUtcOffset(wallClock));
    }

    /// <summary>The same instant as the organiser would read it off the wall.</summary>
    public DateTime ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;

    public string Format(DateTimeOffset instant) => ToLocal(instant).ToString("g");

    private static TimeZoneInfo? TryFind(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
