using System.ComponentModel.DataAnnotations;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Web.Resources;
using TopCut.Web.Time;

namespace TopCut.Web.Components.Pages.Store;

/// <summary>
/// What the create and edit screens bind to. It is not the domain's shape: a form has a date and a
/// time in two boxes, minutes rather than <see cref="TimeSpan"/>, and can sit half-filled while the
/// organiser thinks. Turning it into something the domain accepts happens on submit.
/// </summary>
public sealed class TournamentFormModel
{
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationGameRequired))]
    public int TcgId { get; set; }

    [Required(ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationTournamentNameRequired))]
    [StringLength(200, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationTournamentNameLength))]
    public string Name { get; set; } = string.Empty;

    public DateTime? Date { get; set; }

    public TimeSpan? Time { get; set; }

    [Range(2, 5000, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationCapacityRange))]
    public int Capacity { get; set; } = 16;

    [Range(1, 20, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationRoundsRange))]
    public int SwissRounds { get; set; } = 4;

    public int? TopCutSize { get; set; }

    [Required(ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationStreetRequired))]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationCityRequired))]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationProvinceRequired))]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationPostalCodeRequired))]
    public string PostalCode { get; set; } = string.Empty;

    public MatchFormat SwissFormat { get; set; } = MatchFormat.Bo3;

    [Range(5, 240, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationRoundLengthRange))]
    public int SwissMinutes { get; set; } = 50;

    [Range(0, 60, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationExtraTimeRange))]
    public int SwissExtraMinutes { get; set; }

    public MatchFormat TopCutFormat { get; set; } = MatchFormat.Bo3;

    [Range(5, 240, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationRoundLengthRange))]
    public int TopCutMinutes { get; set; } = 90;

    [Range(0, 60, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationExtraTimeRange))]
    public int TopCutExtraMinutes { get; set; }

    public TimeExpiryPolicy TimeExpiry { get; set; } = TimeExpiryPolicy.LeaderWins;

    /// <summary>A brand-new form: the chosen game's own defaults, starting next Saturday evening.</summary>
    public static TournamentFormModel For(TcgOption game, DateTime today)
    {
        var model = new TournamentFormModel
        {
            Date = NextSaturday(today),
            Time = new TimeSpan(17, 0, 0)
        };

        model.AdoptDefaultsOf(game);
        return model;
    }

    /// <summary>
    /// Takes the game's rules as the starting point, including what its rulebook prescribes for the
    /// capacity already typed in. Called when the organiser switches game, so the form never shows
    /// One Piece timings under a Magic heading.
    /// </summary>
    public void AdoptDefaultsOf(TcgOption game)
    {
        TcgId = game.Id;
        SwissFormat = game.SwissPhase.Format;
        SwissMinutes = Minutes(game.SwissPhase.RoundLength, fallback: 50);
        SwissExtraMinutes = (int)game.SwissPhase.ExtraTime.TotalMinutes;
        TopCutFormat = game.TopCutPhase.Format;
        TopCutMinutes = Minutes(game.TopCutPhase.RoundLength, fallback: 90);
        TopCutExtraMinutes = (int)game.TopCutPhase.ExtraTime.TotalMinutes;
        TimeExpiry = game.TimeExpiry;

        ApplyRecommendationOf(game);
    }

    /// <summary>Puts the rulebook's rounds and cut for the current capacity into the form.</summary>
    public void ApplyRecommendationOf(TcgOption game)
    {
        if (game.RecommendationFor(Capacity) is not { } band)
        {
            return;
        }

        SwissRounds = band.SwissRounds;
        TopCutSize = band.TopCutSize;
    }

    public static TournamentFormModel From(TournamentDetails details, LocalTime clock)
    {
        var startsAt = clock.ToLocal(details.StartsAt);

        return new TournamentFormModel
        {
            TcgId = details.TcgId,
            Name = details.Name,
            Date = startsAt.Date,
            Time = startsAt.TimeOfDay,
            Capacity = details.Capacity,
            SwissRounds = details.SwissRounds,
            TopCutSize = details.TopCutSize,
            Street = details.Venue.Street,
            City = details.Venue.City,
            Province = details.Venue.Province,
            PostalCode = details.Venue.PostalCode,
            SwissFormat = details.SwissPhase.Format,
            SwissMinutes = Minutes(details.SwissPhase.RoundLength, fallback: 50),
            SwissExtraMinutes = (int)details.SwissPhase.ExtraTime.TotalMinutes,
            TopCutFormat = details.TopCutPhase.Format,
            TopCutMinutes = Minutes(details.TopCutPhase.RoundLength, fallback: 90),
            TopCutExtraMinutes = (int)details.TopCutPhase.ExtraTime.TotalMinutes,
            TimeExpiry = details.TimeExpiry
        };
    }

    public TournamentDraft ToDraft(LocalTime clock) => new(
        TcgId,
        Name,
        clock.ToInstant(Date ?? DateTime.Today, Time ?? TimeSpan.Zero),
        Street,
        City,
        Province,
        PostalCode,
        Capacity,
        SwissRounds,
        TopCutSize);

    public TournamentRules ToRules() => new(
        new PhaseSettings(SwissFormat, TimeSpan.FromMinutes(SwissMinutes), TimeSpan.FromMinutes(SwissExtraMinutes)),
        new PhaseSettings(TopCutFormat, TimeSpan.FromMinutes(TopCutMinutes), TimeSpan.FromMinutes(TopCutExtraMinutes)),
        TimeExpiry);

    /// <summary>Saturday is when a local store runs its events; the organiser can move it.</summary>
    private static DateTime NextSaturday(DateTime today)
    {
        var daysAway = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;

        // Today being Saturday means the next one, not this morning.
        return today.Date.AddDays(daysAway == 0 ? 7 : daysAway);
    }

    private static int Minutes(TimeSpan? length, int fallback) =>
        length is { } value ? (int)value.TotalMinutes : fallback;
}
