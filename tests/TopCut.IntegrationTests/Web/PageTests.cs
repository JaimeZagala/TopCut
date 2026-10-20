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
[Collection(PostgresCollection.Name)]
public class PageTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Password = "Jugador-1234";

    private TopCutAppFactory _app = null!;

    public Task InitializeAsync()
    {
        _app = new TopCutAppFactory(postgres);
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
        Assert.Contains("Aquí no hay nada", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_privacy_notice_shows_the_version_that_is_being_accepted()
    {
        var html = await Browser().GetStringAsync("/privacidad");

        Assert.Contains("Aviso de protección de datos", html);
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
        Assert.Contains("aceptar el aviso de protección de datos", html);

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

        Assert.Contains("Todavía no has creado ningún torneo", html);
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

    [Fact]
    public async Task The_participants_screen_starts_empty_and_says_how_to_add_somebody()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Torneo con gente");

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/participantes");

        Assert.Contains("Todavía no hay nadie apuntado", html);
        Assert.Contains("Participantes", html);

        // The tabs are real links, so the details screen is reachable from here.
        Assert.Contains($"/tienda/torneos/{tournamentId}", html);
    }

    [Fact]
    public async Task The_participants_screen_shows_who_is_in_and_who_is_waiting()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Torneo pequeño", capacity: 2);

        await SignUp(storeId, tournamentId, "Ana", "Bruno", "Carla");

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/participantes");

        Assert.Contains("Ana", html);
        Assert.Contains("Lista de espera", html);
        Assert.Contains("Carla", html);
        Assert.Contains("No quedan plazas", html);
    }

    [Fact]
    public async Task The_desk_offers_the_first_round_once_the_tournament_is_published()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Torneo de mesa");

        await SignUp(storeId, tournamentId, "Ana", "Bruno", "Carla", "Diego");
        await Publish(storeId, tournamentId);

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/mesa");

        Assert.Contains("Lanzar la primera ronda", html);
        Assert.Contains("Todavía no se ha jugado ninguna ronda", html);
    }

    [Fact]
    public async Task The_desk_shows_the_tables_and_the_standings_once_a_round_is_under_way()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Torneo de mesa");

        await SignUp(storeId, tournamentId, "Ana", "Bruno", "Carla", "Diego");
        await Publish(storeId, tournamentId);
        await StartRound(storeId, tournamentId);

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/mesa");

        Assert.Contains("Ronda 1", html);
        Assert.Contains("Ana", html);
        Assert.Contains("Clasificación", html);

        // Two tables, both waiting on a result.
        Assert.Contains("2 sin resultado", html);
    }

    [Fact]
    public async Task A_draft_tournament_says_to_publish_it_before_playing()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Sin publicar");

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/mesa");

        Assert.Contains("Publica el torneo", html);
        Assert.DoesNotContain("Lanzar la primera ronda", html);
    }

    [Fact]
    public async Task The_tab_you_are_already_on_is_not_a_link_to_itself()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Con pestañas");

        var details = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}");

        // Navigating to the page you are already on reloads the form from the database and throws
        // away whatever was typed, so the current tab must not offer to do that.
        Assert.DoesNotContain($"href=\"/tienda/torneos/{tournamentId}\"", details);
        Assert.Contains($"href=\"/tienda/torneos/{tournamentId}/participantes\"", details);
        Assert.Contains($"href=\"/tienda/torneos/{tournamentId}/mesa\"", details);

        var roster = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/participantes");

        Assert.DoesNotContain($"href=\"/tienda/torneos/{tournamentId}/participantes\"", roster);
        Assert.Contains($"href=\"/tienda/torneos/{tournamentId}\"", roster);
    }

    [Fact]
    public async Task The_live_screen_shows_the_tables_and_the_round_clock()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Torneo proyectado");

        await SignUp(storeId, tournamentId, "Ana", "Bruno", "Carla", "Diego");
        await Publish(storeId, tournamentId);
        await StartRound(storeId, tournamentId);

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/en-vivo");

        Assert.Contains("Ronda 1", html);
        Assert.Contains("Ana", html);

        // The clock is rendered server-side first, so the room sees a time before the browser
        // takes over the ticking.
        Assert.Contains("topcut-clock", html);
        Assert.Contains("data-state=\"running\"", html);

        // No sidebar and no app bar: every pixel is for names read from the back of the room.
        Assert.DoesNotContain("Panel de la tienda", html);
        Assert.DoesNotContain("Mesa de control", html);
    }

    [Fact]
    public async Task Between_rounds_the_live_screen_shows_the_standings_instead()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");
        var tournamentId = await CreateTournament(storeId, "Entre rondas");

        await SignUp(storeId, tournamentId, "Ana", "Bruno");
        await Publish(storeId, tournamentId);

        var html = await browser.GetStringAsync($"/tienda/torneos/{tournamentId}/en-vivo");

        Assert.Contains("Esperando a que empiece la ronda", html);
        Assert.Contains("Clasificación", html);
    }

    // --- Cara publica (hito 5a) --------------------------------------------------------------

    [Fact]
    public async Task The_public_search_is_open_to_anybody_and_spends_no_live_connection()
    {
        var response = await Browser().GetAsync("/torneos");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The one page strangers land on. A circuit each would empty the free SignalR tier by
        // teatime, so there must be no interactive island here at all.
        Assert.DoesNotContain("<!--Blazor:", html);

        // A plain GET form: these are the names the page reads back off the query string, and the
        // reason the search survives a bookmark and the back button.
        Assert.Contains("method=\"get\"", html);

        foreach (var field in new[] { "texto", "ciudad", "juego", "desde", "hasta", "cuando" })
        {
            Assert.Contains($"name=\"{field}\"", html);
        }
    }

    [Fact]
    public async Task A_visitor_with_no_account_finds_a_published_tournament_from_a_store_they_do_not_know()
    {
        var store = Browser();
        var (email, storeId) = await RegisterApprovedStore(store);
        var city = NewCity();
        var tournamentId = await CreateTournament(storeId, "Prerelease de invierno", city: city);

        await Publish(storeId, tournamentId);

        // A different client with no cookie at all: nobody, from another store's point of view.
        var html = await Browser().GetStringAsync($"/torneos?ciudad={Uri.EscapeDataString(city)}");

        Assert.Contains("Prerelease de invierno", html);
        Assert.Contains("Kraken Cards", html);
        Assert.Contains($"/torneos/{tournamentId}", html);
    }

    [Fact]
    public async Task A_tournament_the_store_has_not_published_is_not_on_the_public_search()
    {
        var store = Browser();
        var (email, storeId) = await RegisterApprovedStore(store);
        var city = NewCity();

        await CreateTournament(storeId, "Todavia en borrador", city: city);

        var html = await Browser().GetStringAsync($"/torneos?ciudad={Uri.EscapeDataString(city)}");

        Assert.DoesNotContain("Todavia en borrador", html);
        Assert.Contains("No hay ningún torneo", html);
    }

    [Fact]
    public async Task The_tournament_sheet_says_where_when_and_under_which_rules()
    {
        var store = Browser();
        var (email, storeId) = await RegisterApprovedStore(store);
        var city = NewCity();
        var tournamentId = await CreateTournament(storeId, "Prerelease de invierno", city: city);

        await Publish(storeId, tournamentId);

        var html = await Browser().GetStringAsync($"/torneos/{tournamentId}");

        Assert.Contains("Prerelease de invierno", html);
        Assert.Contains("Kraken Cards", html);
        Assert.Contains("Calle Mayor 1", html);
        Assert.Contains(city, html);

        // The rules the tournament copied when it was created, which are the ones that will be
        // applied — not whatever the rulebook says by the time somebody reads this.
        Assert.Contains("Al mejor de tres", html);
        Assert.Contains("rondas de 50 min", html);
        Assert.Contains("Magic Tournament Rules", html);

        Assert.DoesNotContain("<!--Blazor:", html);
    }

    [Fact]
    public async Task A_link_to_a_tournament_that_is_not_public_answers_404_and_never_a_blank_page()
    {
        var store = Browser();
        var (email, storeId) = await RegisterApprovedStore(store);
        var draft = await CreateTournament(storeId, "Todavia en borrador", city: NewCity());

        // A real 404 and not a 200 with an apology: these addresses are public, and a search engine
        // follows them long before a person does. The body is the site's own not-found screen,
        // re-executed by the pipeline — the one thing it must not be is empty, which is what a
        // page that sets the status code and then renders its own markup would have produced.
        foreach (var url in new[] { $"/torneos/{Guid.CreateVersion7()}", $"/torneos/{draft}" })
        {
            var response = await Browser().GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("Aquí no hay nada", html);
        }
    }

    [Fact]
    public async Task A_league_has_no_public_address_until_one_of_its_tournaments_is_published()
    {
        var store = Browser();
        var (_, storeId) = await RegisterApprovedStore(store);
        var tournament = await CreateTournament(storeId, "Jornada de liga", city: NewCity());
        var league = await CreateLeague(storeId, tournament, "Liga de otoño");

        // Only a draft so far, so there is nothing to show and no address to stumble on.
        var hidden = await Browser().GetAsync($"/ligas/{league}");

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Contains("Aquí no hay nada", await hidden.Content.ReadAsStringAsync());

        await PublishTournament(storeId, tournament);

        var response = await Browser().GetAsync($"/ligas/{league}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Liga de otoño", html);
        Assert.Contains("Jornada de liga", html);
        Assert.Contains("Clasificación de la liga", html);

        // Anonymous and static: a league table nobody has to sign in for must not open a circuit
        // per visitor (ADR-0020), which is the whole reason it is served this way.
        Assert.DoesNotContain("<!--Blazor:", html);

        // It is the only public page that publishes people's names, so it is shareable by link and
        // out of the search engines (hito 6e). The sheet of a tournament, which names nobody, is not.
        Assert.Contains("name=\"robots\" content=\"noindex", html);
        Assert.DoesNotContain("noindex", await Browser().GetStringAsync($"/torneos/{tournament}"));
    }

    [Fact]
    public async Task My_leagues_is_not_for_anonymous_visitors()
    {
        var response = await Browser().GetAsync("/mis-ligas");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cuenta/acceso", Target(response));
    }

    [Fact]
    public async Task A_player_who_has_not_scored_in_a_league_yet_is_told_so_rather_than_shown_an_empty_table()
    {
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);

        var html = await player.GetStringAsync("/mis-ligas");

        Assert.Contains("Todavía no has puntuado en ninguna liga", html);
        Assert.DoesNotContain("<!--Blazor:", html);
    }

    [Fact]
    public async Task A_query_string_that_makes_no_sense_is_a_search_without_filters_and_not_an_error()
    {
        // Anybody can type into a public address, and a browser sends every empty box along too.
        var response = await Browser().GetAsync("/torneos?juego=&desde=&hasta=&cuando=quizas&texto=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var nonsense = await Browser().GetAsync("/torneos?juego=magic&desde=ayer&hasta=nunca");

        Assert.Equal(HttpStatusCode.OK, nonsense.StatusCode);
    }

    // --- Inscripcion del jugador (hito 5b) ----------------------------------------------------

    [Fact]
    public async Task A_visitor_who_is_not_signed_in_is_offered_a_way_back_to_this_very_sheet()
    {
        var tournamentId = await PublishedTournament();

        var html = await Browser().GetStringAsync($"/torneos/{tournamentId}");

        Assert.Contains("Para apuntarte necesitas una cuenta", html);

        // Signing in must land them back on the tournament they were looking at, not on the home
        // page with the sheet lost.
        Assert.Contains($"volver=%2Ftorneos%2F{tournamentId}", html);
        Assert.DoesNotContain("<!--Blazor:", html);
    }

    [Fact]
    public async Task A_player_signs_up_from_the_sheet_and_the_sheet_says_so()
    {
        var tournamentId = await PublishedTournament();
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);

        var html = await Join(player, tournamentId);

        Assert.Contains("Estás inscrito", html);
        Assert.Contains("Ana", html);

        // The seat count on the same page has to agree with it straight away.
        Assert.Contains("1 de 16 plazas", html);
    }

    [Fact]
    public async Task A_full_tournament_signs_the_player_into_the_queue_and_tells_them_their_number()
    {
        var (email, storeId) = await RegisterApprovedStore(Browser());
        var tournamentId = await CreateTournament(storeId, "Torneo pequeño", capacity: 2, city: NewCity());
        await SignUp(storeId, tournamentId, "Ana", "Bruno");
        await Publish(storeId, tournamentId);

        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Carla", consent: true);

        var html = await Join(player, tournamentId);

        Assert.Contains("Estás el 1.º en la lista de espera", html);
    }

    [Fact]
    public async Task A_player_can_take_themselves_back_out_and_then_change_their_mind_again()
    {
        var tournamentId = await PublishedTournament();
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);

        await Join(player, tournamentId);

        var left = await Submit(player, $"/torneos/{tournamentId}", "darse-de-baja");
        Assert.Contains("Te diste de baja", left);
        Assert.Contains("0 de 16 plazas", left);

        var back = await Join(player, tournamentId);
        Assert.Contains("Estás inscrito", back);
    }

    [Fact]
    public async Task My_tournaments_lists_what_the_player_signed_up_for_and_nothing_else()
    {
        var mine = await PublishedTournament("Al que voy");
        var theirs = await PublishedTournament("Al que no voy");

        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);
        await Join(player, mine);

        var html = await player.GetStringAsync("/mis-torneos");

        Assert.Contains("Al que voy", html);
        Assert.DoesNotContain("Al que no voy", html);
        Assert.DoesNotContain("<!--Blazor:", html);
    }

    [Fact]
    public async Task My_tournaments_is_not_for_anonymous_visitors()
    {
        var response = await Browser().GetAsync("/mis-torneos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cuenta/acceso", Target(response));
    }

    // --- La mesa del jugador (hito 5c) --------------------------------------------------------

    [Fact]
    public async Task My_table_is_not_for_anonymous_visitors()
    {
        var tournamentId = await PublishedTournament();

        var response = await Browser().GetAsync($"/torneos/{tournamentId}/mi-mesa");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cuenta/acceso", Target(response));
    }

    [Fact]
    public async Task Somebody_who_is_not_playing_is_pointed_at_the_tournaments_they_are_in()
    {
        var tournamentId = await PublishedTournament();
        var stranger = Browser();
        await SubmitRegistration(stranger, NewEmail(), "Nadie", consent: true);

        var html = await stranger.GetStringAsync($"/torneos/{tournamentId}/mi-mesa");

        Assert.Contains("No estás en este torneo", html);
        Assert.Contains("/mis-torneos", html);
    }

    [Fact]
    public async Task The_player_sees_their_table_their_opponent_and_the_clock_without_a_circuit()
    {
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);
        var tournamentId = await TournamentWithARoundUnderWay(player);

        var html = await player.GetStringAsync($"/torneos/{tournamentId}/mi-mesa");

        Assert.Contains("Mesa ", html);
        Assert.Contains("Contra ", html);
        Assert.Contains("Ronda 1", html);

        // The clock is rendered by the server first and then driven by the same module the
        // projector uses, started from the page because there is no circuit to call it (ADR-0033).
        Assert.Contains("topcut-clock", html);
        Assert.Contains("data-state=\"running\"", html);
        Assert.Contains("roundClock.js", html);

        // Sixteen phones on sixteen circuits would not fit in the free tier's five WebSockets.
        Assert.DoesNotContain("<!--Blazor:", html);
    }

    [Fact]
    public async Task The_buttons_are_worded_from_the_players_own_side()
    {
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);
        var tournamentId = await TournamentWithARoundUnderWay(player);

        var html = await player.GetStringAsync($"/torneos/{tournamentId}/mi-mesa");

        // Not "2-0", which would leave the player working out which side of the table they are.
        Assert.Contains("Gano 2-0", html);
        Assert.Contains("Gano 2-1", html);
        Assert.Contains("Pierdo 1-2", html);
        Assert.Contains("Pierdo 0-2", html);
    }

    [Fact]
    public async Task Reporting_from_the_phone_stores_the_result_and_closes_the_buttons()
    {
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);
        var tournamentId = await TournamentWithARoundUnderWay(player);
        var url = $"/torneos/{tournamentId}/mi-mesa";

        var after = await Submit(player, url, "reportar-2-0-r");

        Assert.Contains("Ganada por 2-0", after);
        Assert.Contains("díselo a la tienda", after);
        Assert.DoesNotContain("Gano 2-1", after);

        // And the desk sees the same match reported, whichever side of it the player sat on.
        var desk = Browser();
        var (storeEmail, _) = StoreOf(tournamentId);
        await SubmitSignIn(desk, storeEmail, "/tienda");

        Assert.Contains("1 sin resultado", await desk.GetStringAsync($"/tienda/torneos/{tournamentId}/mesa"));
    }

    [Fact]
    public async Task With_nothing_to_report_the_page_refreshes_itself_instead_of_holding_a_connection()
    {
        var player = Browser();
        await SubmitRegistration(player, NewEmail(), "Ana", consent: true);
        var tournamentId = await PublishedTournament();
        await Join(player, tournamentId);

        var html = await player.GetStringAsync($"/torneos/{tournamentId}/mi-mesa");

        Assert.Contains("El torneo todavía no ha empezado", html);
        Assert.Contains("http-equiv=\"refresh\"", html);
    }

    // --- Fichero de jugadores (hito 6a) -------------------------------------------------------

    [Fact]
    public async Task The_store_sees_who_has_played_there_and_how_often()
    {
        var browser = Browser();
        var (email, storeId) = await RegisterApprovedStore(browser);
        await SubmitSignIn(browser, email, "/tienda");

        var january = await CreateTournament(storeId, "Enero", city: NewCity());
        var february = await CreateTournament(storeId, "Febrero", city: NewCity());

        await SignUp(storeId, january, "Ana Pérez", "Bruno Gil");
        await SignUp(storeId, february, "Ana Pérez");

        var html = await browser.GetStringAsync("/tienda/jugadores");

        Assert.Contains("Ana Pérez", html);
        Assert.Contains("Bruno Gil", html);

        // The whole point of the book: the same person at two tournaments is one line, not two.
        Assert.Equal(1, Occurrences(html, ">Ana Pérez<"));
    }

    [Fact]
    public async Task Another_stores_players_are_not_in_your_book()
    {
        var owner = Browser();
        var (ownerEmail, ownerStore) = await RegisterApprovedStore(owner);
        await SubmitSignIn(owner, ownerEmail, "/tienda");
        await SignUp(ownerStore, await CreateTournament(ownerStore, "Suyo", city: NewCity()), "Solo mia");

        var intruder = Browser();
        var (intruderEmail, _) = await RegisterApprovedStore(intruder);
        await SubmitSignIn(intruder, intruderEmail, "/tienda");

        Assert.DoesNotContain("Solo mia", await intruder.GetStringAsync("/tienda/jugadores"));
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;

        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
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
    private async Task SignUp(Guid storeId, Guid tournamentId, params string[] names)
    {
        using var scope = _app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantScope>().Set(storeId);

        var players = scope.ServiceProvider.GetRequiredService<ParticipantService>();

        foreach (var name in names)
        {
            var outcome = await players.RegisterAsync(tournamentId, name);
            Assert.True(outcome.Succeeded, string.Join(" ", outcome.Errors));
        }
    }

    private async Task Publish(Guid storeId, Guid tournamentId)
    {
        using var scope = _app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantScope>().Set(storeId);

        var result = await scope.ServiceProvider
            .GetRequiredService<TournamentSetupService>()
            .PublishAsync(tournamentId);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    private async Task StartRound(Guid storeId, Guid tournamentId)
    {
        using var scope = _app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantScope>().Set(storeId);

        var result = await scope.ServiceProvider
            .GetRequiredService<TournamentService>()
            .StartNextRoundAsync(tournamentId);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    /// <summary>
    /// A four-player tournament with the first round paired and the given browser signed up in it,
    /// which is the only state where the player's screen has anything to do.
    /// </summary>
    private async Task<Guid> TournamentWithARoundUnderWay(HttpClient player)
    {
        var (email, storeId) = await RegisterApprovedStore(Browser());
        var tournamentId = await CreateTournament(storeId, "Torneo en marcha", city: NewCity());

        await SignUp(storeId, tournamentId, "Rival", "Otro", "Uno mas");
        await Publish(storeId, tournamentId);
        await Join(player, tournamentId);
        await StartRound(storeId, tournamentId);

        _stores[tournamentId] = email;
        return tournamentId;
    }

    /// <summary>The account of the store that organises a tournament these tests created.</summary>
    private (string Email, Guid TournamentId) StoreOf(Guid tournamentId) => (_stores[tournamentId], tournamentId);

    private readonly Dictionary<Guid, string> _stores = [];

    /// <summary>A published tournament of some store the player has nothing to do with.</summary>
    private async Task<Guid> PublishedTournament(string name = "Prerelease de invierno")
    {
        var (_, storeId) = await RegisterApprovedStore(Browser());
        var tournamentId = await CreateTournament(storeId, name, city: NewCity());

        await Publish(storeId, tournamentId);

        return tournamentId;
    }

    private Task<string> Join(HttpClient player, Guid tournamentId) =>
        Submit(player, $"/torneos/{tournamentId}", "apuntarse");

    /// <summary>Posts one of a page's Blazor forms the way a browser would, and returns what came back.</summary>
    private static async Task<string> Submit(HttpClient browser, string url, string formName)
    {
        var page = await browser.GetStringAsync(url);
        var fields = HtmlPage.HiddenFieldsOf(page, formName);

        var response = await browser.PostAsync(url, new FormUrlEncodedContent(fields));

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>A city nobody else's test is using, so a public search can be pinned to one store.</summary>
    private static string NewCity() => $"Villa {Guid.CreateVersion7():N}";

    private async Task<Guid> CreateTournament(
        Guid storeId,
        string name,
        int capacity = 16,
        string city = "Valencia")
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
            city,
            "Valencia",
            "46001",
            capacity,
            SwissRounds: 5,
            TopCutSize: 4));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        return created.TournamentId!.Value;
    }

    private async Task<Guid> CreateLeague(Guid storeId, Guid tournamentId, string name)
    {
        using var scope = _app.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantScope>().Set(storeId);

        var catalog = scope.ServiceProvider.GetRequiredService<ITcgCatalog>();
        var magic = (await catalog.ActiveAsync()).First(game => game.Code == "MTG");

        var leagues = scope.ServiceProvider.GetRequiredService<ILeagues>();

        var created = await leagues.CreateAsync(new LeagueDraft(
            magic.Id,
            name,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3))));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));

        var setup = scope.ServiceProvider.GetRequiredService<TournamentSetupService>();
        var joined = await setup.AssignToLeagueAsync(tournamentId, created.LeagueId!.Value);

        Assert.True(joined.Succeeded, string.Join(" ", joined.Errors));
        return created.LeagueId!.Value;
    }

    private async Task PublishTournament(Guid storeId, Guid tournamentId)
    {
        using var scope = _app.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantScope>().Set(storeId);

        var published = await scope.ServiceProvider
            .GetRequiredService<TournamentSetupService>()
            .PublishAsync(tournamentId);

        Assert.True(published.Succeeded, string.Join(" ", published.Errors));
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
