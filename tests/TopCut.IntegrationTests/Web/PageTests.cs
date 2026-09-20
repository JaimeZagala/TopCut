using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.IntegrationTests.Identity;
using TopCut.IntegrationTests.Persistence;

namespace TopCut.IntegrationTests.Web;

/// <summary>
/// The browser's view of the application: real HTTP against the real <c>Program</c>, following the
/// same path a person does. These are the tests that would catch a form that renders beautifully
/// and posts nothing, or a protected page that returns an empty screen instead of a redirect.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PageTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Jugador-1234";

    private TopCutAppFactory _app = null!;

    public Task InitializeAsync()
    {
        _app = new TopCutAppFactory(sqlServer);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _app.DisposeAsync().AsTask();

    // --- Cascara ---------------------------------------------------------------------------------

    [Fact]
    public async Task The_home_page_is_served_with_the_MudBlazor_stylesheet_and_nothing_left_of_bootstrap()
    {
        var html = await Browser().GetStringAsync("/");

        Assert.Contains("_content/MudBlazor/MudBlazor", html);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_pages_declare_spanish_so_the_browser_does_not_offer_to_translate_them()
    {
        var html = await Browser().GetStringAsync("/");

        Assert.Contains("<html lang=\"es\"", html);
    }

    [Fact]
    public async Task An_address_that_does_not_exist_shows_the_not_found_page()
    {
        var response = await Browser().GetAsync("/esto-no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Aqui no hay nada", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_privacy_notice_shows_the_version_that_is_being_accepted()
    {
        var html = await Browser().GetStringAsync("/privacidad");

        Assert.Contains("Aviso de proteccion de datos", html);
        Assert.Contains(TopCut.Application.Abstractions.PrivacyNotice.CurrentVersion, html);
    }

    [Fact]
    public async Task The_public_pages_do_not_open_a_live_connection()
    {
        var browser = Browser();

        // The free Azure SignalR tier allows about twenty concurrent connections in total, so a
        // page nobody interacts with must not spend one. An interactive island leaves a marker
        // comment in the HTML; these pages must have none.
        foreach (var url in new[] { "/", "/privacidad", "/cuenta/acceso", "/cuenta/registro" })
        {
            Assert.DoesNotContain("<!--Blazor:", await browser.GetStringAsync(url));
        }
    }

    [Fact]
    public async Task The_store_panel_does_open_one_because_its_screens_need_it()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");

        Assert.Contains("<!--Blazor:", await browser.GetStringAsync("/tienda"));
    }

    // --- Alta desde el navegador -------------------------------------------------------------------

    [Fact]
    public async Task The_registration_form_carries_the_field_names_the_server_binds()
    {
        var html = await Browser().GetStringAsync("/cuenta/registro");

        // Not cosmetic: these are the names the form binder looks for in the posted body.
        Assert.Contains("name=\"Input.DisplayName\"", html);
        Assert.Contains("name=\"Input.Email\"", html);
        Assert.Contains("name=\"Input.Password\"", html);
        Assert.Contains("name=\"Input.ConsentAccepted\"", html);
    }

    [Fact]
    public async Task A_player_can_register_from_the_browser_form_and_lands_signed_in()
    {
        var browser = Browser();
        var email = NewEmail();

        var response = await SubmitRegistration(browser, email, "Ana", consent: true);

        // The page signs them in and sends them to the home page.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", Target(response));

        // And the account really exists: the API can sign in with it.
        var login = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Registering_without_ticking_the_notice_is_refused_and_the_form_keeps_what_was_typed()
    {
        var browser = Browser();
        var email = NewEmail();

        var response = await SubmitRegistration(browser, email, "Ana", consent: false);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aceptar el aviso de proteccion de datos", html);

        // Retyping everything because of one unticked box would be maddening.
        Assert.Equal("Ana", HtmlPage.ValueOf(html, "Input.DisplayName"));
        Assert.Equal(email, HtmlPage.ValueOf(html, "Input.Email"));

        // And nothing was created.
        var login = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // --- Acceso y permisos ---------------------------------------------------------------------------

    [Fact]
    public async Task An_anonymous_visitor_to_the_store_panel_is_sent_to_the_sign_in_screen()
    {
        var response = await Browser().GetAsync("/tienda");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = Target(response);
        Assert.StartsWith("/cuenta/acceso", location);
        Assert.Contains("volver=%2Ftienda", location);
    }

    [Fact]
    public async Task A_player_with_no_store_is_told_why_the_store_panel_is_closed_to_them()
    {
        var browser = Browser();
        await SubmitRegistration(browser, NewEmail(), "Ana", consent: true);

        // The registration left a session cookie behind, so this is a signed-in player.
        var response = await browser.GetAsync("/tienda");

        // Signed in but not an approved store: refused, not sent round to the sign-in screen again.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Target(response);
        Assert.StartsWith("/sin-permiso", location);

        Assert.Contains("No tienes permiso", await browser.GetStringAsync(location));
    }

    [Fact]
    public async Task An_approved_store_reaches_its_panel()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);

        await SubmitSignIn(browser, email, "/tienda");
        var response = await browser.GetAsync("/tienda");

        // This one crashed before the store lookup got its own DbContext: the page's own
        // authorization and the AuthorizeView in the top bar both ran the policy at the same time
        // on the request's shared context.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Panel de la tienda", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Two_permission_checks_at_once_do_not_trip_over_each_other()
    {
        var browser = Browser();
        await RegisterApprovedStore(browser);

        using var scope = _app.Services.CreateScope();
        var stores = scope.ServiceProvider.GetRequiredService<IStoreDirectory>();
        var storeId = Guid.CreateVersion7();

        // Same scope, both at once: exactly what a page with several AuthorizeView does.
        var answers = await Task.WhenAll(
            stores.IsApprovedAsync(storeId),
            stores.IsApprovedAsync(storeId));

        Assert.All(answers, Assert.False);
    }

    [Fact]
    public async Task Signing_in_from_the_browser_returns_to_the_page_that_asked_for_it()
    {
        var browser = Browser();
        var email = NewEmail();
        await SubmitRegistration(browser, email, "Ana", consent: true);
        await browser.PostAsync("/cuenta/salir", await SignOutForm(browser));

        var response = await SubmitSignIn(browser, email, "/tienda");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/tienda", Target(response));
    }

    [Fact]
    public async Task A_return_address_pointing_off_the_site_is_ignored()
    {
        var browser = Browser();
        var email = NewEmail();
        await SubmitRegistration(browser, email, "Ana", consent: true);
        await browser.PostAsync("/cuenta/salir", await SignOutForm(browser));

        // A phishing link could put anything in that query string; only paths inside the site
        // are honoured, and everything else lands on the home page.
        var response = await SubmitSignIn(browser, email, "//evil.example/robo");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", Target(response));
    }

    // --- Pantallas de la tienda ------------------------------------------------------------------

    [Fact]
    public async Task The_tournament_list_starts_empty_and_offers_to_create_the_first_one()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");

        var html = await browser.GetStringAsync("/tienda/torneos");

        Assert.Contains("Todavia no has creado ningun torneo", html);
        Assert.Contains("/tienda/torneos/nuevo", html);
    }

    [Fact]
    public async Task A_created_tournament_shows_up_on_the_list_with_its_game_and_structure()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");

        await CreateTournament(storeId, "Prerelease de invierno");

        var html = await browser.GetStringAsync("/tienda/torneos");

        Assert.Contains("Prerelease de invierno", html);
        Assert.Contains("Magic: The Gathering", html);
        Assert.Contains("Borrador", html);
    }

    [Fact]
    public async Task The_new_tournament_form_arrives_filled_in_with_what_the_rulebook_recommends()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");

        var html = await browser.GetStringAsync("/tienda/torneos/nuevo");

        // ADR-0018: the screen opens on the official table rather than on an empty box.
        Assert.Contains("recomienda", html);
        Assert.Contains("rondas", html);
    }

    [Fact]
    public async Task One_stores_tournament_is_not_reachable_from_another_stores_screens()
    {
        var owner = Browser();
        var (ownerEmail, ownerStore) = await RegisterApprovedStore(owner);
        await SubmitSignIn(owner, ownerEmail, "/tienda");
        var tournamentId = await CreateTournament(ownerStore, "Solo mio");

        var intruder = Browser();
        var (intruderEmail, _) = await RegisterApprovedStore(intruder);
        await SubmitSignIn(intruder, intruderEmail, "/tienda");

        var theirList = await intruder.GetStringAsync("/tienda/torneos");
        var byGuessingTheId = await intruder.GetStringAsync($"/tienda/torneos/{tournamentId}");

        Assert.DoesNotContain("Solo mio", theirList);
        Assert.Contains("no existe", byGuessingTheId);
    }

    // --- Helpers ----------------------------------------------------------------------------------------

    /// <summary>Where a redirect points, as a path, whether the header is absolute or relative.</summary>
    private static string Target(HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private HttpClient Browser() =>
        _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string NewEmail() => $"jugador-{Guid.CreateVersion7():N}@topcut.test";

    private static async Task<HttpResponseMessage> SubmitRegistration(
        HttpClient browser,
        string email,
        string displayName,
        bool consent)
    {
        var page = await browser.GetStringAsync("/cuenta/registro");

        var fields = HtmlPage.HiddenFieldsOf(page, "registro");
        fields["Input.DisplayName"] = displayName;
        fields["Input.Email"] = email;
        fields["Input.Password"] = Password;

        // An unticked checkbox sends nothing at all, exactly as a browser would.
        if (consent)
        {
            fields["Input.ConsentAccepted"] = HtmlPage.TickedValueOf(page, "Input.ConsentAccepted");
        }

        return await browser.PostAsync("/cuenta/registro", new FormUrlEncodedContent(fields));
    }

    /// <summary>
    /// Creates a tournament through the application services, the way the screen does. The form
    /// itself is interactive, so driving it needs a circuit rather than an HTTP post; what these
    /// tests check is that the pages then render what the store actually owns.
    /// </summary>
    private async Task<Guid> CreateTournament(Guid storeId, string name)
    {
        using var scope = _app.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantScope>().Set(storeId);

        var catalog = scope.ServiceProvider.GetRequiredService<ITcgCatalog>();
        var magic = (await catalog.ActiveAsync()).First(game => game.Code == "MTG");

        var setup = scope.ServiceProvider.GetRequiredService<TournamentSetupService>();

        var created = await setup.CreateAsync(new TournamentDraft(
            magic.Id,
            name,
            DateTimeOffset.UtcNow.AddDays(7),
            "Calle Mayor 1",
            "Valencia",
            "Valencia",
            "46001",
            Capacity: 16,
            SwissRounds: 5,
            TopCutSize: 4));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        return created.TournamentId!.Value;
    }

    /// <summary>A store account that a Superadmin has already approved, signed up through the API.</summary>
    private async Task<(string Email, Guid StoreId)> RegisterApprovedStore(HttpClient browser)
    {
        var email = NewEmail();
        await SubmitRegistration(browser, email, "Kraken Cards", consent: true);

        var token = await BearerFor(browser, email);
        var applied = await Send(browser, HttpMethod.Post, "/api/v1/stores/apply", token, new
        {
            storeName = "Kraken Cards",
            street = "Calle Mayor 1",
            city = "Valencia",
            province = "Valencia",
            postalCode = "46001"
        });
        applied.EnsureSuccessStatusCode();

        // The tenant claim is only issued on sign-in, so the store id needs a fresh token.
        var me = await Send(browser, HttpMethod.Get, "/api/v1/auth/me", await BearerFor(browser, email), null);
        var storeId = (await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetString();

        var superadmin = await BearerFor(
            browser,
            TopCutAppFactory.SuperadminEmail,
            TopCutAppFactory.SuperadminPassword);

        var approved = await Send(browser, HttpMethod.Post, $"/api/v1/stores/{storeId}/approve", superadmin, new { });
        approved.EnsureSuccessStatusCode();

        return (email, Guid.Parse(storeId!));
    }

    private static async Task<string> BearerFor(HttpClient browser, string email, string? password = null)
    {
        var response = await browser.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = password ?? Password });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> Send(
        HttpClient browser,
        HttpMethod method,
        string url,
        string token,
        object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await browser.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SubmitSignIn(HttpClient browser, string email, string returnUrl)
    {
        var url = $"/cuenta/acceso?volver={Uri.EscapeDataString(returnUrl)}";
        var page = await browser.GetStringAsync(url);

        var fields = HtmlPage.HiddenFieldsOf(page, "acceso");
        fields["Input.Email"] = email;
        fields["Input.Password"] = Password;

        return await browser.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static async Task<FormUrlEncodedContent> SignOutForm(HttpClient browser)
    {
        var page = await browser.GetStringAsync("/");

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = HtmlPage.AntiforgeryToken(page)
        });
    }
}
