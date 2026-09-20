using Microsoft.Extensions.Logging.Abstractions;
using TopCut.Application.Abstractions;
using TopCut.Infrastructure.Live;

namespace TopCut.IntegrationTests.Live;

/// <summary>
/// The fan-out behind the live screens. No database and no HTTP: this is about who gets told what,
/// and about the awkward cases — a screen that goes away mid-announcement, and one that stops
/// listening.
/// </summary>
public class BroadcasterTests
{
    [Fact]
    public async Task A_screen_watching_a_tournament_is_told_when_it_moves()
    {
        var broadcaster = New();
        var seen = new List<TournamentChange>();

        using var _ = broadcaster.Watch(Tournament, change =>
        {
            seen.Add(change);
            return Task.CompletedTask;
        });

        await broadcaster.PublishAsync(Tournament, TournamentChange.RoundStarted);
        await broadcaster.PublishAsync(Tournament, TournamentChange.ResultRecorded);

        Assert.Equal([TournamentChange.RoundStarted, TournamentChange.ResultRecorded], seen);
    }

    [Fact]
    public async Task Every_screen_on_the_same_tournament_is_told()
    {
        var broadcaster = New();
        var desk = 0;
        var projector = 0;

        using var _ = broadcaster.Watch(Tournament, _ => { desk++; return Task.CompletedTask; });
        using var __ = broadcaster.Watch(Tournament, _ => { projector++; return Task.CompletedTask; });

        await broadcaster.PublishAsync(Tournament, TournamentChange.ResultRecorded);

        Assert.Equal(1, desk);
        Assert.Equal(1, projector);
    }

    [Fact]
    public async Task A_screen_watching_another_tournament_hears_nothing()
    {
        var broadcaster = New();
        var told = false;

        using var _ = broadcaster.Watch(Guid.CreateVersion7(), _ => { told = true; return Task.CompletedTask; });

        await broadcaster.PublishAsync(Tournament, TournamentChange.ResultRecorded);

        Assert.False(told);
    }

    [Fact]
    public async Task A_screen_that_stopped_watching_is_not_told_again()
    {
        var broadcaster = New();
        var times = 0;

        var watch = broadcaster.Watch(Tournament, _ => { times++; return Task.CompletedTask; });

        await broadcaster.PublishAsync(Tournament, TournamentChange.ResultRecorded);
        watch.Dispose();
        await broadcaster.PublishAsync(Tournament, TournamentChange.ResultRecorded);

        Assert.Equal(1, times);

        // Disposing twice is what happens when a component is torn down oddly; it must be harmless.
        watch.Dispose();
    }

    [Fact]
    public async Task A_screen_that_blew_up_does_not_take_the_others_with_it()
    {
        var broadcaster = New();
        var survivor = false;

        using var _ = broadcaster.Watch(Tournament, _ => throw new InvalidOperationException("circuito caido"));
        using var __ = broadcaster.Watch(Tournament, _ => { survivor = true; return Task.CompletedTask; });

        // And above all it must not throw: the publisher is a domain event handler running right
        // after somebody's save, and a closed browser tab cannot be allowed to look like a failure.
        await broadcaster.PublishAsync(Tournament, TournamentChange.ResultRecorded);

        Assert.True(survivor);
    }

    [Fact]
    public async Task Announcing_a_tournament_nobody_is_watching_does_nothing()
    {
        var broadcaster = New();

        await broadcaster.PublishAsync(Tournament, TournamentChange.Completed);
    }

    private static readonly Guid Tournament = Guid.CreateVersion7();

    private static TournamentBroadcaster New() => new(NullLogger<TournamentBroadcaster>.Instance);
}
