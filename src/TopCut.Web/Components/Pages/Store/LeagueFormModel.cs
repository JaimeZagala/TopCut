using System.ComponentModel.DataAnnotations;
using TopCut.Application.Abstractions;
using TopCut.Web.Resources;

namespace TopCut.Web.Components.Pages.Store;

/// <summary>
/// What the create and edit league screens bind to. A season is two <see cref="DateOnly"/> in the
/// domain and two pickers on screen, and a picker hands back a nullable <see cref="DateTime"/>.
/// </summary>
public sealed class LeagueFormModel
{
    [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationGameRequired))]
    public int TcgId { get; set; }

    [Required(ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationLeagueNameRequired))]
    [StringLength(200, ErrorMessageResourceType = typeof(Textos), ErrorMessageResourceName = nameof(Textos.ValidationLeagueNameLength))]
    public string Name { get; set; } = string.Empty;

    public DateTime? StartsOn { get; set; }

    public DateTime? EndsOn { get; set; }

    /// <summary>A season starting today and running three months, which is the usual shape.</summary>
    public static LeagueFormModel For(int tcgId, DateTime today) => new()
    {
        TcgId = tcgId,
        StartsOn = today.Date,
        EndsOn = today.Date.AddMonths(3)
    };

    public static LeagueFormModel From(LeagueDetails league) => new()
    {
        TcgId = league.TcgId,
        Name = league.Name,
        StartsOn = league.StartsOn.ToDateTime(TimeOnly.MinValue),
        EndsOn = league.EndsOn.ToDateTime(TimeOnly.MinValue)
    };

    public LeagueDraft ToDraft() => new(
        TcgId,
        Name,
        DateOnly.FromDateTime(StartsOn ?? DateTime.Today),
        DateOnly.FromDateTime(EndsOn ?? DateTime.Today));
}
