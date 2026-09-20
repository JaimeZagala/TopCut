using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TopCut.IntegrationTests.Persistence;

namespace TopCut.IntegrationTests.Identity;

[Collection(SqlServerCollection.Name)]
public class AuthenticationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TopCutAppFactory _app = null!;

    public Task InitializeAsync()
    {
        _app = new TopCutAppFactory(sqlServer);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _app.DisposeAsync().AsTask();

    // --- Registration and consent --------------------------------------------------------------

    [Fact]
    public async Task A_player_can_register_and_then_sign_in()
    {
        var client = _app.CreateClient();
        var email = NewEmail();

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Jugador-1234",
            displayName = "Ana",
            consentAccepted = true
        });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var token = await SignIn(client, email, "Jugador-1234");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Registration_without_accepting_the_privacy_notice_is_refused()
    {
        var client = _app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = NewEmail(),
            password = "Jugador-1234",
            displayName = "Ana",
            consentAccepted = false
        });

        // Checked on the server, so posting straight to the API cannot skip the checkbox.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("protección de datos", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_refused_registration_leaves_no_account_behind()
    {
        var client = _app.CreateClient();
        var email = NewEmail();

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Jugador-1234",
            displayName = "Ana",
            consentAccepted = false
        });

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Jugador-1234" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task A_new_account_gets_the_player_role_and_no_store()
    {
        var client = _app.CreateClient();
        var email = NewEmail();
        await Register(client, email, "Ana");

        var me = await GetMe(client, await SignIn(client, email, "Jugador-1234"));

        Assert.Equal("Ana", me.GetProperty("displayName").GetString());
        Assert.Contains("Player", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(JsonValueKind.Null, me.GetProperty("tenantId").ValueKind);
    }

    [Fact]
    public async Task Wrong_credentials_do_not_reveal_whether_the_account_exists()
    {
        var client = _app.CreateClient();
        var email = NewEmail();
        await Register(client, email, "Ana");

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "no-es-esta" });
        var noSuchAccount = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = NewEmail(),
            password = "Jugador-1234"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noSuchAccount.StatusCode);

        // Same message either way. Anything that distinguished them would let an attacker
        // enumerate which emails are registered. The traceId differs per request, so compare
        // the detail rather than the whole body.
        Assert.Equal(await Detail(wrongPassword), await Detail(noSuchAccount));
    }

    [Fact]
    public async Task An_anonymous_caller_gets_401_not_a_redirect_on_the_api()
    {
        var client = _app.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Store onboarding -------------------------------------------------------------------------

    [Fact]
    public async Task Applying_for_a_store_links_it_to_the_account_but_does_not_approve_it()
    {
        var client = _app.CreateClient();
        var email = NewEmail();
        await Register(client, email, "Tienda Kraken");
        var token = await SignIn(client, email, "Jugador-1234");

        var applied = await Post(client, token, "/api/v1/stores/apply", new
        {
            storeName = "Kraken Cards",
            street = "Calle Mayor 1",
            city = "Valencia",
            province = "Valencia",
            postalCode = "46001"
        });

        Assert.Equal(HttpStatusCode.Accepted, applied.StatusCode);

        // A fresh token, because the tenant claim is only issued on sign-in.
        var me = await GetMe(client, await SignIn(client, email, "Jugador-1234"));
        Assert.Contains("Store", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(me.GetProperty("tenantId").GetString()));
    }

    [Fact]
    public async Task A_store_cannot_approve_itself()
    {
        var client = _app.CreateClient();
        var (email, storeId) = await RegisterStore(client, "Kraken Cards");
        var token = await SignIn(client, email, "Jugador-1234");

        var response = await Post(client, token, $"/api/v1/stores/{storeId}/approve", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_approve_a_store()
    {
        var client = _app.CreateClient();
        var (_, storeId) = await RegisterStore(client, "Kraken Cards");

        var response = await client.PostAsJsonAsync($"/api/v1/stores/{storeId}/approve", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_superadmin_can_approve_and_revoke_a_store()
    {
        var client = _app.CreateClient();
        var (_, storeId) = await RegisterStore(client, "Kraken Cards");

        var superadmin = await SignIn(client, TopCutAppFactory.SuperadminEmail, TopCutAppFactory.SuperadminPassword);

        var approved = await Post(client, superadmin, $"/api/v1/stores/{storeId}/approve", new { });
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);

        var revoked = await Post(client, superadmin, $"/api/v1/stores/{storeId}/revoke", new { });
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
    }

    [Fact]
    public async Task The_superadmin_is_seeded_on_startup_so_the_first_store_can_be_approved()
    {
        var client = _app.CreateClient();

        var token = await SignIn(client, TopCutAppFactory.SuperadminEmail, TopCutAppFactory.SuperadminPassword);
        var me = await GetMe(client, token);

        Assert.Contains("Superadmin", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    private static string NewEmail() => $"jugador-{Guid.CreateVersion7():N}@topcut.test";

    private static async Task Register(HttpClient client, string email, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Jugador-1234",
            displayName,
            consentAccepted = true
        });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> SignIn(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> GetMe(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string token, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request);
    }

    private async Task<(string Email, Guid StoreId)> RegisterStore(HttpClient client, string storeName)
    {
        var email = NewEmail();
        await Register(client, email, storeName);
        var token = await SignIn(client, email, "Jugador-1234");

        var applied = await Post(client, token, "/api/v1/stores/apply", new
        {
            storeName,
            street = "Calle Mayor 1",
            city = "Valencia",
            province = "Valencia",
            postalCode = "46001"
        });
        applied.EnsureSuccessStatusCode();

        var me = await GetMe(client, await SignIn(client, email, "Jugador-1234"));
        return (email, Guid.Parse(me.GetProperty("tenantId").GetString()!));
    }
}
