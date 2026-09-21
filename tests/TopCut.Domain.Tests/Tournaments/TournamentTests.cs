using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Events;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Tournaments;

public class TournamentTests
{
    private static readonly Guid StoreId = Guid.CreateVersion7();

    private static Address Venue() => new("Calle Mayor 1", "Valencia", "Valencia", "46001");

    private static Tournament NewTournament(int capacity = 8, int swissRounds = 3, Ruleset? ruleset = null) =>
        Tournament.Create(
            StoreId,
            ruleset ?? SeedCatalog.MagicRuleset(tcgId: 1),
            "Torneo de prueba",
            DateTimeOffset.UtcNow.AddDays(7),
            Venue(),
            capacity,
            swissRounds);

    // --- Settings copied from the ruleset --------------------------------------------------------

    [Fact]
    public void A_tournament_copies_the_ruleset_and_later_changes_do_not_reach_it()
    {
        var ruleset = SeedCatalog.MagicRuleset(tcgId: 1);
        var tournament = NewTournament(ruleset: ruleset);

        // The Superadmin edits Magic's default ruleset after the tournament was created.
        ruleset.SetPhase(Phase.Swiss, new PhaseSettings(MatchFormat.Bo1, TimeSpan.FromMinutes(20), TimeSpan.Zero));

        Assert.Equal(MatchFormat.Bo3, tournament.Settings.SwissPhase.Format);
        Assert.Equal(TimeSpan.FromMinutes(50), tournament.Settings.SwissPhase.RoundLength);
    }

    [Fact]
    public void Tournament_settings_remember_which_ruleset_they_came_from()
    {
        var ruleset = SeedCatalog.OnePieceRuleset(tcgId: 2);
        var tournament = NewTournament(ruleset: ruleset);

        Assert.Equal(ruleset.Id, tournament.Settings.SourceRulesetId);
        Assert.Equal(ruleset.RulesVersion, tournament.Settings.RulesVersion);
    }

    [Fact]
    public void The_copied_phase_settings_are_new_instances_and_not_the_rulesets_own()
    {
        var ruleset = SeedCatalog.MagicRuleset(tcgId: 1);
        var tournament = NewTournament(ruleset: ruleset);

        // Equal by value, but distinct objects: sharing them would mean the tournament and the
        // catalog were the same row as far as the database is concerned.
        Assert.Equal(ruleset.SwissPhase, tournament.Settings.SwissPhase);
        Assert.NotSame(ruleset.SwissPhase, tournament.Settings.SwissPhase);
        Assert.NotSame(ruleset.TopCutPhase, tournament.Settings.TopCutPhase);
    }

    [Fact]
    public void A_store_can_mix_Bo3_swiss_with_a_Bo1_top_cut()
    {
        var tournament = NewTournament();
        tournament.SetTopCut(8);

        tournament.SetPhaseSettings(
            Phase.TopCut,
            new PhaseSettings(MatchFormat.Bo1, TimeSpan.FromMinutes(30), TimeSpan.Zero));

        Assert.Equal(MatchFormat.Bo3, tournament.Settings.SwissPhase.Format);
        Assert.Equal(MatchFormat.Bo1, tournament.Settings.TopCutPhase.Format);
    }

    [Fact]
    public void A_store_can_override_the_time_expiry_policy_before_starting()
    {
        var tournament = NewTournament();

        tournament.SetTimeExpiryPolicy(TimeExpiryPolicy.AlwaysDraw);

        Assert.Equal(TimeExpiryPolicy.AlwaysDraw, tournament.Settings.TimeExpiry);
    }

    // --- Top cut ----------------------------------------------------------------------------------

    [Fact]
    public void A_tournament_starts_with_no_top_cut()
    {
        var tournament = NewTournament();

        Assert.False(tournament.HasTopCut);
        Assert.Null(tournament.TopCutSize);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void The_top_cut_accepts_any_power_of_two(int size)
    {
        var tournament = NewTournament();

        tournament.SetTopCut(size);

        Assert.Equal(size, tournament.TopCutSize);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void The_top_cut_rejects_sizes_that_are_not_a_power_of_two(int size)
    {
        var tournament = NewTournament();

        Assert.Throws<DomainException>(() => tournament.SetTopCut(size));
    }

    [Fact]
    public void The_top_cut_can_be_changed_or_removed_until_the_tournament_starts()
    {
        var tournament = NewTournament();
        tournament.Publish();

        tournament.SetTopCut(8);
        tournament.SetTopCut(4);
        tournament.SetTopCut(null);

        Assert.False(tournament.HasTopCut);
    }

    [Fact]
    public void The_top_cut_freezes_once_the_first_round_starts()
    {
        var tournament = NewTournament();
        tournament.Publish();
        tournament.SignUp("Ana");
        tournament.SignUp("Bruno");
        tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        var error = Assert.Throws<DomainException>(() => tournament.SetTopCut(8));

        Assert.Contains("ya en curso", error.Message);
    }

    [Fact]
    public void A_top_cut_round_cannot_start_when_the_tournament_has_no_top_cut()
    {
        var tournament = NewTournament();
        tournament.Publish();

        var error = Assert.Throws<DomainException>(
            () => tournament.StartRound(Phase.TopCut, DateTimeOffset.UtcNow));

        Assert.Contains("no tiene top cut", error.Message);
    }

    // --- Registration and waitlist ------------------------------------------------------------------

    [Fact]
    public void Once_full_registrations_go_to_the_waitlist_in_arrival_order()
    {
        var tournament = NewTournament(capacity: 2);
        tournament.Publish();

        tournament.SignUp("Ana");
        tournament.SignUp("Bruno");
        var carla = tournament.SignUp("Carla");
        var diego = tournament.SignUp("Diego");

        Assert.True(tournament.IsFull);
        Assert.Equal(ParticipantStatus.Waitlisted, carla.Status);
        Assert.Equal(1, carla.WaitlistPosition);
        Assert.Equal(2, diego.WaitlistPosition);
    }

    [Fact]
    public void Dropping_before_the_start_promotes_the_first_in_line_and_reorders_the_rest()
    {
        var tournament = NewTournament(capacity: 2);
        tournament.Publish();

        var ana = tournament.SignUp("Ana");
        tournament.SignUp("Bruno");
        var carla = tournament.SignUp("Carla");
        var diego = tournament.SignUp("Diego");

        var promoted = tournament.Drop(ana.Id);

        Assert.Same(carla, promoted);
        Assert.Equal(ParticipantStatus.Registered, carla.Status);
        Assert.Null(carla.WaitlistPosition);
        Assert.Equal(1, diego.WaitlistPosition);
    }

    [Fact]
    public void Dropping_before_the_start_announces_who_took_the_freed_seat()
    {
        var tournament = NewTournament(capacity: 2);
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        tournament.SignUp("Bruno");
        var carla = tournament.SignUp("Carla");

        tournament.Drop(ana.Id);

        var raised = Assert.Single(tournament.DomainEvents.OfType<ParticipantDropped>());
        Assert.Equal(ana.Id, raised.ParticipantId);
        Assert.Equal(carla.Id, raised.PromotedParticipantId);
    }

    [Fact]
    public void Leaving_the_queue_closes_the_gap_behind()
    {
        var tournament = NewTournament(capacity: 2);
        tournament.Publish();
        tournament.SignUp("Ana");
        tournament.SignUp("Zoe");
        var bruno = tournament.SignUp("Bruno");
        var carla = tournament.SignUp("Carla");
        var diego = tournament.SignUp("Diego");

        Assert.Equal([1, 2, 3], new[] { bruno, carla, diego }.Select(p => p.WaitlistPosition));

        tournament.Drop(bruno.Id);

        // Sin renumerar, a Carla se le seguiria diciendo que es la segunda cuando ya no hay nadie
        // delante. Nadie lo noto mientras la cola solo la veia la tienda.
        Assert.Equal(1, carla.WaitlistPosition);
        Assert.Equal(2, diego.WaitlistPosition);

        // Y nadie ocupa plaza: quien se fue estaba en la cola, no sentado.
        var raised = Assert.Single(tournament.DomainEvents.OfType<ParticipantDropped>());
        Assert.Null(raised.PromotedParticipantId);
    }

    [Fact]
    public void A_player_who_withdraws_and_comes_back_keeps_one_row_and_loses_the_seat()
    {
        var tournament = NewTournament(capacity: 2);
        tournament.Publish();
        var ana = tournament.SignUp("Ana", "cuenta-de-ana");
        tournament.SignUp("Zoe");

        tournament.Drop(ana.Id);
        var bruno = tournament.SignUp("Bruno");

        var again = tournament.SignUpAgain(ana);

        Assert.Same(ana, again);
        Assert.Equal(3, tournament.Participants.Count);
        Assert.Equal(ParticipantStatus.Waitlisted, again.Status);
        Assert.Equal(1, again.WaitlistPosition);
        Assert.Equal(ParticipantStatus.Registered, bruno.Status);
    }

    [Fact]
    public void The_same_person_cannot_sign_up_twice()
    {
        var tournament = NewTournament();
        tournament.Publish();
        var ana = tournament.SignUp("Ana", "cuenta-de-ana");

        var error = Assert.Throws<DomainException>(() => tournament.SignUpAgain(ana));

        Assert.Contains("ya está inscrito", error.Message);
    }

    [Fact]
    public void Dropping_mid_tournament_promotes_nobody()
    {
        var tournament = NewTournament(capacity: 2);
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        tournament.SignUp("Bruno");
        var carla = tournament.SignUp("Carla");
        tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        var promoted = tournament.Drop(ana.Id);

        Assert.Null(promoted);
        Assert.Equal(ParticipantStatus.Waitlisted, carla.Status);
    }

    [Fact]
    public void A_dropped_player_stops_entering_pairings()
    {
        var tournament = NewTournament();
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        tournament.SignUp("Bruno");
        tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        tournament.Drop(ana.Id);

        Assert.DoesNotContain(ana, tournament.ActiveParticipants);
        Assert.Equal(ParticipantStatus.Dropped, ana.Status);
    }

    [Fact]
    public void A_participant_without_an_account_is_valid()
    {
        var tournament = NewTournament();

        var participant = tournament.SignUp("Jugador de mostrador");

        Assert.Null(participant.UserId);
        Assert.Equal(ParticipantStatus.Registered, participant.Status);
    }

    [Fact]
    public void Two_people_who_happen_to_share_a_name_are_two_participants()
    {
        var tournament = NewTournament();

        // What tells them apart is the entry in the store's book, not what they are called
        // (ADR-0035). A shop with two Anas is a shop with two Anas.
        tournament.SignUp("Ana");
        tournament.SignUp("Ana");

        Assert.Equal(2, tournament.Participants.Count);
        Assert.Equal(2, tournament.Participants.Select(p => p.StorePlayerId).Distinct().Count());
    }

    [Fact]
    public void A_tournament_in_progress_takes_no_registrations()
    {
        var tournament = NewTournament();
        tournament.Publish();
        tournament.SignUp("Ana");
        tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => tournament.SignUp("Tarde"));
    }

    // --- Rounds --------------------------------------------------------------------------------------

    [Fact]
    public void Starting_a_round_raises_the_event_and_puts_the_tournament_in_progress()
    {
        var tournament = NewTournament();
        tournament.Publish();

        var round = tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.Equal(1, round.Number);
        var raised = Assert.Single(tournament.DomainEvents.OfType<RoundStarted>());
        Assert.Equal(round.Id, raised.RoundId);
    }

    [Fact]
    public void A_round_cannot_start_while_another_is_open()
    {
        var tournament = NewTournament();
        tournament.Publish();
        tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        var error = Assert.Throws<DomainException>(
            () => tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow));

        Assert.Contains("ronda en curso", error.Message);
    }

    [Fact]
    public void No_more_swiss_rounds_than_planned_can_be_started()
    {
        var tournament = NewTournament(swissRounds: 1);
        tournament.Publish();
        var now = DateTimeOffset.UtcNow;
        var round = tournament.StartRound(Phase.Swiss, now);
        tournament.CloseRound(round.Id, now);

        var error = Assert.Throws<DomainException>(() => tournament.StartRound(Phase.Swiss, now));

        Assert.Contains("todas las rondas suizas", error.Message);
    }

    [Fact]
    public void A_tournament_only_completes_with_every_round_closed()
    {
        var tournament = NewTournament(swissRounds: 1);
        tournament.Publish();
        var round = tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(tournament.Complete);

        tournament.CloseRound(round.Id, DateTimeOffset.UtcNow);
        tournament.Complete();

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Single(tournament.DomainEvents.OfType<TournamentCompleted>());
    }
}
