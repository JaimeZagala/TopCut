using TopCut.Domain.Catalog;
using TopCut.Domain.Common;

namespace TopCut.Domain.Tests.Catalog;

/// <summary>
/// Pins the seed values to the official rules cited in each ruleset. If a governing body revises
/// its rulebook, these are the tests that should fail first.
/// </summary>
public class SeedRulesetsTests
{
    // A fresh instance on every access, so no test can contaminate another.
    private static Ruleset Magic => SeedCatalog.MagicRuleset(tcgId: 1);

    private static Ruleset OnePiece => SeedCatalog.OnePieceRuleset(tcgId: 2);

    [Fact]
    public void Magic_scores_3_1_0_and_a_bye_is_worth_a_win()
    {
        Assert.Equal(3, Magic.PointsFor(MatchResult.Win));
        Assert.Equal(1, Magic.PointsFor(MatchResult.Draw));
        Assert.Equal(0, Magic.PointsFor(MatchResult.Loss));
        Assert.Equal(Magic.PointsFor(MatchResult.Win), Magic.PointsFor(MatchResult.Bye));
    }

    [Fact]
    public void Magic_applies_the_three_MTR_tiebreakers_in_order()
    {
        Assert.Equal(
            [
                Tiebreaker.OpponentsMatchWinPercentage,
                Tiebreaker.GameWinPercentage,
                Tiebreaker.OpponentsGameWinPercentage
            ],
            Magic.Tiebreakers);
    }

    [Fact]
    public void Magic_plays_both_phases_best_of_three()
    {
        Assert.Equal(MatchFormat.Bo3, Magic.SwissPhase.Format);
        Assert.Equal(MatchFormat.Bo3, Magic.TopCutPhase.Format);
    }

    [Fact]
    public void Magic_runs_50_minute_swiss_and_90_minute_top_cut_with_no_clock_overtime()
    {
        Assert.Equal(TimeSpan.FromMinutes(50), Magic.SwissPhase.RoundLength);
        Assert.Equal(TimeSpan.FromMinutes(90), Magic.TopCutPhase.RoundLength);
        Assert.Equal(TimeSpan.Zero, Magic.SwissPhase.ExtraTime);
    }

    [Fact]
    public void OnePiece_plays_swiss_as_a_single_game_and_the_top_cut_best_of_three()
    {
        Assert.Equal(MatchFormat.Bo1, OnePiece.SwissPhase.Format);
        Assert.Equal(MatchFormat.Bo3, OnePiece.TopCutPhase.Format);
    }

    [Fact]
    public void OnePiece_adds_clock_overtime_to_both_phases()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), OnePiece.SwissPhase.RoundLength);
        Assert.Equal(TimeSpan.FromMinutes(5), OnePiece.SwissPhase.ExtraTime);
        Assert.Equal(TimeSpan.FromMinutes(35), OnePiece.SwissPhase.TotalLength);

        Assert.Equal(TimeSpan.FromMinutes(60), OnePiece.TopCutPhase.RoundLength);
        Assert.Equal(TimeSpan.FromMinutes(10), OnePiece.TopCutPhase.ExtraTime);
        Assert.Equal(TimeSpan.FromMinutes(70), OnePiece.TopCutPhase.TotalLength);
    }

    [Fact]
    public void OnePiece_breaks_ties_on_the_players_own_percentage_first()
    {
        Assert.Equal(
            [
                Tiebreaker.OwnMatchWinPercentage,
                Tiebreaker.OpponentsMatchWinPercentage,
                Tiebreaker.HeadToHead
            ],
            OnePiece.Tiebreakers);

        // Its swiss phase is a single game, so it cannot break ties on games won.
        Assert.DoesNotContain(Tiebreaker.GameWinPercentage, OnePiece.Tiebreakers);
    }

    [Fact]
    public void The_two_games_treat_byes_differently_in_the_players_own_percentage()
    {
        // This is the difference that makes the ruleset configuration rather than code.
        Assert.True(Magic.ByesCountInOwnWinPercentage);
        Assert.False(OnePiece.ByesCountInOwnWinPercentage);
    }

    [Fact]
    public void Both_games_apply_the_033_floor_to_percentages()
    {
        Assert.Equal(0.33m, Magic.MinimumPercentage);
        Assert.Equal(0.33m, OnePiece.MinimumPercentage);
    }

    [Fact]
    public void Both_games_default_to_the_official_leader_wins_rule_on_time_expiry()
    {
        // ADR-0009: both rulebooks give the match to whoever is ahead once extra turns are done.
        Assert.Equal(TimeExpiryPolicy.LeaderWins, Magic.TimeExpiry);
        Assert.Equal(TimeExpiryPolicy.LeaderWins, OnePiece.TimeExpiry);
    }

    [Fact]
    public void Every_ruleset_cites_the_rulebook_version_it_was_validated_against()
    {
        foreach (var ruleset in new[] { Magic, OnePiece })
        {
            Assert.False(string.IsNullOrWhiteSpace(ruleset.RulesVersion));
            Assert.StartsWith("https://", ruleset.OfficialSource);
        }
    }

    [Fact]
    public void A_ruleset_rejects_duplicate_tiebreakers()
    {
        var error = Assert.Throws<DomainException>(() => OnePiece.SetTiebreakers(
        [
            Tiebreaker.OpponentsMatchWinPercentage,
            Tiebreaker.OpponentsMatchWinPercentage
        ]));

        Assert.Contains("no pueden repetirse", error.Message);
    }
}
