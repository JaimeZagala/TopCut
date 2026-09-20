using Microsoft.AspNetCore.Http;
using TopCut.Api.Auth;
using TopCut.Application.Abstractions;

namespace TopCut.IntegrationTests.Web;

/// <summary>
/// The store scope in the two places it has to work. An interactive page runs inside a SignalR
/// circuit, where there is no <c>HttpContext</c> at all: without the explicit path the tenant would
/// resolve to <c>null</c>, the query filter would fail closed (ADR-0011), and the store would open
/// its own tournament list to find it empty (ADR-0024).
/// </summary>
public class TenantContextTests
{
    [Fact]
    public void Without_a_request_and_without_being_told_there_is_no_store_in_scope()
    {
        var tenant = new TenantContext(new HttpContextAccessor());

        // Fails closed: no scope means no data, never all data.
        Assert.Null(((ITenantContext)tenant).TenantId);
    }

    [Fact]
    public void A_circuit_can_set_the_store_it_is_working_for()
    {
        var tenant = new TenantContext(new HttpContextAccessor());
        var storeId = Guid.CreateVersion7();

        ((ITenantScope)tenant).Set(storeId);

        Assert.Equal(storeId, ((ITenantContext)tenant).TenantId);
    }

    [Fact]
    public void A_user_with_no_store_is_scoped_to_no_store_rather_than_to_every_store()
    {
        var tenant = new TenantContext(new HttpContextAccessor());

        ((ITenantScope)tenant).Set(null);

        Assert.Null(((ITenantContext)tenant).TenantId);
    }
}
