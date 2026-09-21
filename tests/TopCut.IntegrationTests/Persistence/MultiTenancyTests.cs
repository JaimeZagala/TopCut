using Microsoft.EntityFrameworkCore;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// The whole multi-tenancy design rests on one claim: a store never sees another store's data,
/// without any use case having to remember to filter. These are the tests that prove it.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MultiTenancyTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_store_sees_its_own_tournaments_and_not_the_other_stores()
    {
        var (storeA, storeB) = await TwoStoresWithATournamentEach();

        await using var asStoreA = fixture.CreateContext(storeA);
        var visible = await asStoreA.Tournaments.ToListAsync();

        Assert.All(visible, tournament => Assert.Equal(storeA, tournament.TenantId));
        Assert.DoesNotContain(visible, tournament => tournament.TenantId == storeB);
    }

    [Fact]
    public async Task Child_entities_are_filtered_too_and_not_just_the_aggregate_root()
    {
        var (storeA, storeB) = await TwoStoresWithATournamentEach();

        await using var asStoreA = fixture.CreateContext(storeA);

        Assert.All(await asStoreA.Participants.ToListAsync(), p => Assert.Equal(storeA, p.TenantId));
        Assert.Empty(await asStoreA.Participants.Where(p => p.TenantId == storeB).ToListAsync());
    }

    [Fact]
    public async Task With_no_tenant_in_scope_nothing_is_visible()
    {
        await TwoStoresWithATournamentEach();

        await using var unscoped = fixture.CreateContext(tenantId: null);

        // Fail closed: a bug that forgets to set the tenant shows nothing rather than everything.
        Assert.Empty(await unscoped.Tournaments.ToListAsync());
        Assert.Empty(await unscoped.Participants.ToListAsync());
    }

    [Fact]
    public async Task Crossing_the_boundary_takes_an_explicit_opt_out()
    {
        var (storeA, storeB) = await TwoStoresWithATournamentEach();

        await using var unscoped = fixture.CreateContext(tenantId: null);
        var all = await unscoped.Tournaments.IgnoreQueryFilters().ToListAsync();

        // What the public tournament search and the Superadmin panel will do, visibly.
        Assert.Contains(all, tournament => tournament.TenantId == storeA);
        Assert.Contains(all, tournament => tournament.TenantId == storeB);
    }

    [Fact]
    public async Task A_store_cannot_reach_another_stores_tournament_by_id()
    {
        var (storeA, storeB) = await TwoStoresWithATournamentEach();

        await using var asStoreB = fixture.CreateContext(storeB);
        var theirs = await asStoreB.Tournaments.IgnoreQueryFilters()
            .FirstAsync(tournament => tournament.TenantId == storeA);

        await using var alsoStoreB = fixture.CreateContext(storeB);
        var guessed = await alsoStoreB.Tournaments.FirstOrDefaultAsync(t => t.Id == theirs.Id);

        Assert.Null(guessed);
    }

    [Fact]
    public async Task Stores_themselves_are_not_filtered_because_a_store_is_the_tenant()
    {
        var (storeA, _) = await TwoStoresWithATournamentEach();

        await using var asStoreA = fixture.CreateContext(storeA);

        // Store is the tenant, not tenant-owned: the public search has to list them all.
        Assert.True(await asStoreA.Stores.CountAsync() >= 2);
    }

    private async Task<(Guid StoreA, Guid StoreB)> TwoStoresWithATournamentEach()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);

        var ruleset = await db.Rulesets.FirstAsync(r => r.Tcg.Code == SeedCatalog.MagicCode);

        var storeA = NewStore("Tienda A");
        var storeB = NewStore("Tienda B");
        db.Stores.AddRange(storeA, storeB);

        db.Tournaments.AddRange(
            TournamentFor(storeA.Id, ruleset, "Liga de los jueves"),
            TournamentFor(storeB.Id, ruleset, "Torneo del sabado"));

        await db.SaveChangesAsync();

        return (storeA.Id, storeB.Id);
    }

    private static Store NewStore(string name)
    {
        var store = new Store(name, new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));
        store.Approve();
        return store;
    }

    private static Tournament TournamentFor(Guid tenantId, Ruleset ruleset, string name)
    {
        var tournament = Tournament.Create(
            tenantId,
            ruleset,
            name,
            DateTimeOffset.UtcNow.AddDays(7),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            capacity: 8,
            swissRounds: 3);

        tournament.Publish();
        tournament.SignUp("Ana");
        tournament.SignUp("Bruno");

        return tournament;
    }
}
